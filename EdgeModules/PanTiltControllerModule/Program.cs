namespace PanTiltControllerModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;

    using Microsoft.Azure.Devices.Logging;
    using Microsoft.Extensions.Logging;

    using System.IO.Ports;
    using PanAndTilt.Common;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.Extensions.Logging;

    public class Program
    {
        private static ModuleClient moduleClient;

        private static SerialPort serialPort;

        private static Twin twins;

        private static DateTime? lastReportedTwinTime;
        private static TimeSpan twinReportingInterval = TimeSpan.FromSeconds(300);


        private static DateTime? lastDirectionOutputReportingTime;
        private static TimeSpan directionOutputReportingInterval = TimeSpan.FromSeconds(1);


        private static ReportDirectionMessage lastReportDirectionMessage;


        private static bool _continueReadFromSerial;

        private static Thread readThread = new Thread(readMethod);

        private static ILogger logger = null;

        public static async Task Main(string[] args)
        {
            string logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL");

            if (!string.IsNullOrEmpty(logLevel))
            {
                if ( Microsoft.Azure.Devices.Edge.Util.Logger.LogLevelDictionary.ContainsKey(logLevel) )
                {
                    Console.WriteLine($"Setting Log Level to {logLevel}");
                    Microsoft.Azure.Devices.Edge.Util.Logger.SetLogLevel(logLevel);
                }
                else
                {
                    Console.WriteLine($"Setting Log Level to info as {logLevel} is an unrecognized log level");
                    Microsoft.Azure.Devices.Edge.Util.Logger.SetLogLevel("info");
                }               
            }
            else
            {
                Console.WriteLine("Set Log Level to info.");
                Microsoft.Azure.Devices.Edge.Util.Logger.SetLogLevel("info");
            }

            Microsoft.Azure.Devices.Edge.Util.Logger.SetLogLevel("info");

            logger = Microsoft.Azure.Devices.Edge.Util.Logger.Factory.CreateLogger<Program>();
                        
            const string SdkEventProviderPrefix = "Microsoft-Azure-";
            // Instantiating this seems to do all we need for outputting SDK events to our console log
            _ = new ConsoleEventListener(SdkEventProviderPrefix, logger);


            bool initDoneProperly = await Init();

            if ( initDoneProperly)
            {
                logger.LogInformation("Initialization completed, now running in background.");
            }
            else
            {
                logger.LogInformation("Initialization completed but unfortunately can't find the Serial Port connected to the board.");
            }
            
            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            await WhenCancelled(cts.Token);

            _continueReadFromSerial = false;

            if (serialPort != null && serialPort.IsOpen)
                serialPort.Close();

            logger.LogInformation("App closing..."); 
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        private static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        private static async Task<bool> Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await moduleClient.OpenAsync();
            logger.LogInformation("IoT Hub module client initialized.");

            string reportingIntervalMs = Environment.GetEnvironmentVariable("REPORTING_INTERVAL_MILLISECONDS");

            if (!string.IsNullOrEmpty(reportingIntervalMs))
            {
                logger.LogInformation($"Found REPORTING_INTERVAL_MILLISECONDS Env Variable: {reportingIntervalMs}");

                int reportingIntervalMsInt;

                var parsed = int.TryParse(reportingIntervalMs, out reportingIntervalMsInt);

                if (!parsed)
                {
                    logger.LogError("REPORTING_INTERVAL_MILLISECONDS not a number.  Using 1 second reportin interval.");
                }
                else
                {
                    directionOutputReportingInterval = TimeSpan.FromMilliseconds(reportingIntervalMsInt);
                }
            }
            else
            {
                logger.LogInformation("Using 1 second reporting interval.");
            }

      
            //open the serial port...
            serialPort = await COMPortDetector.OpenPanTiltSerial(logger);

            if (serialPort == null)
            {
                logger.LogError("No valid Serial Port found connected to the control board.");
                return false;
            }


            logger.LogInformation("Starting Serial port listener thread.");
            _continueReadFromSerial = true;
            readThread.Start();


            //Get Twins at the startup.
            logger.LogInformation("Read Twins at Module startup");                       
            twins = await moduleClient.GetTwinAsync();
            processInitialTwin(twins);

            // Register a callback to the Desired Property Update event
            logger.LogInformation("Registering Desired Properties Update");
            await moduleClient.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback, moduleClient);
            

            // Register callback to be called when a message is received by the module
            await moduleClient.SetInputMessageHandlerAsync("positionCommandsInput", positionCommandsCallback, moduleClient);

            return true;
        }


        /// <summary>
        /// Serial Line read thread
        /// </summary>
        public static void readMethod()
        {
            logger.LogInformation("readMethod - Serial line receiving background Thread is started.");

            while (_continueReadFromSerial)
            {
                try
                {
                    logger.LogDebug(" readMethod - - before serialPort.ReadLine()");
                    string line = serialPort.ReadLine();
                    logger.LogDebug(" readMethod - - after serialPort.ReadLine()");
                    logger.LogDebug(line);

                    PanAndTilt.Common.MessageBase raspiPiMessage = PanAndTilt.Common.MessageBase.ParseJson(line);

                    if (raspiPiMessage is ReportDirectionMessage)
                    {
                        logger.LogDebug(" readMethod - - - message is ReportDirectionMessage");

                        logger.LogDebug($" readMethod - - - lastReportedTwinTime: {lastReportedTwinTime.ToString()}");

                        //store the last reported message
                        lastReportDirectionMessage = raspiPiMessage as ReportDirectionMessage;

                        if ((lastReportedTwinTime == null) || ((lastReportedTwinTime != null) && ((DateTime.UtcNow - lastReportedTwinTime) > twinReportingInterval)))
                        {
                            lastReportedTwinTime = DateTime.UtcNow;

                            try
                            {
                                logger.LogDebug("readMethod - - Reporting Twin Update...");
                                //report status 
                                TwinCollection reportedProperties = new TwinCollection();

                                reportedProperties["pan"] = lastReportDirectionMessage.expectedPan;
                                reportedProperties["tilt"] = lastReportDirectionMessage.expectedTilt;

                                reportedProperties["a"] = new TwinCollection();
                                reportedProperties["a"]["x"] = lastReportDirectionMessage.acc[0];
                                reportedProperties["a"]["y"] = lastReportDirectionMessage.acc[1];
                                reportedProperties["a"]["z"] = lastReportDirectionMessage.acc[2];

                                reportedProperties["m"] = new TwinCollection();
                                reportedProperties["m"]["x"] = lastReportDirectionMessage.mag[0];
                                reportedProperties["m"]["y"] = lastReportDirectionMessage.mag[1];
                                reportedProperties["m"]["z"] = lastReportDirectionMessage.mag[2];

                                moduleClient.UpdateReportedPropertiesAsync(reportedProperties).Wait();

                                logger.LogInformation($"readMethod - - - - twin updated reported: {reportedProperties.ToJson()}");
                            }
                            catch (Exception ex)
                            {
                                logger.LogError($"readMethod - - - - Error while reporting direction as Twin: {ex.Message}");
                            }
                        }

                        if ((lastDirectionOutputReportingTime == null) || ((lastDirectionOutputReportingTime != null) && ((DateTime.UtcNow - lastDirectionOutputReportingTime) > directionOutputReportingInterval)))
                        {
                            lastDirectionOutputReportingTime = DateTime.UtcNow;

                            //emit into the output port of the module the current direction!
                            var stringBody = lastReportDirectionMessage.ToJson();
                            var messageBytes = Encoding.UTF8.GetBytes(stringBody);

                            using (var reportDirectionMessage = new Message(messageBytes))
                            {
                                reportDirectionMessage.Properties.Add("content/type", "application/json");
                                reportDirectionMessage.Properties.Add("MessageType", ReportDirectionMessage.MessageType);

                                moduleClient.SendEventAsync("reportDirectionOutput", reportDirectionMessage).Wait();

                                logger.LogDebug($"readMethod - - - - sent edgeHub output message over reportDirectionOutput port: {stringBody}");
                            }
                        }
                    }

                    if (raspiPiMessage is ReportPiError)
                    {
                        logger.LogError($" readMethod - - - PiPico report an error: {(raspiPiMessage as ReportPiError).errorMessage}");
                    }
                }
                catch(Exception ex)
                {
                    logger.LogError($"readMethod - - Error while listening from serial data: {ex.Message}");
                }
            }

            logger.LogWarning("readMethod - Serial line receiving background Thread exited!");
        }




        private static void processInitialTwin(Twin twins)
        {
            logger.LogInformation("processInitialTwin");

            //desired Pan
            var newPan = twins.Properties.Desired["pan"];
            //desired Tilt
            var newTilt = twins.Properties.Desired["tilt"];
            
            logger.LogInformation($"processInitialTwin - Sending SetDirectionMessage to the Rasbperry pico board. Pan: {newPan} Tilt: {newTilt}");

            var setDirection = new SetDirectionMessage() { expectedPan = newPan, expectedTilt = newTilt };
            var messagePayload = $"{setDirection.ToJson()}";

            logger.LogDebug($"processInitialTwin - JSON: {messagePayload}");

            //Send over the Serial Port
            serialPort.WriteLine(messagePayload);

            //this is going to force the reporting of the position on the next loop!
            lastReportedTwinTime = DateTime.MinValue;

            logger.LogDebug("processInitialTwin - Commanded a new position");
        }



        private static async Task desiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            logger.LogDebug("desiredPropertyUpdateCallback");

            //desired Pan
            var newPan = desiredProperties["pan"];
            //desired Tilt
            var newTilt = desiredProperties["tilt"];
            
            logger.LogInformation($"desiredPropertyUpdateCallback - Sending SetDirectionMessage to the Rasbperry pico board. Pan: {newPan} Tilt: {newTilt}");

            var setDirection = new SetDirectionMessage() { expectedPan = newPan, expectedTilt = newTilt };
            var messagePayload = $"{setDirection.ToJson()}";

            logger.LogDebug($"desiredPropertyUpdateCallback - JSON: {messagePayload}");

            //Send over the Serial Port
            serialPort.WriteLine(messagePayload);

            //this is going to force the reporting of the position on the next loop!
            lastReportedTwinTime = DateTime.MinValue;

            logger.LogDebug("desiredPropertyUpdateCallback - Commanded a new position");

            await Task.Delay(1);
        }



        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub on that port         
        /// </summary>
        private static async Task<MessageResponse> positionCommandsCallback(Message message, object userContext)
        {
            logger.LogInformation($"positionCommandsCallback");

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            var messageBodyBytes = message.GetBytes();
            var messageBody = System.Text.Encoding.Default.GetString(messageBodyBytes);

            logger.LogInformation($"Message Body: {messageBody}");

            try
            {
                PanAndTilt.Common.MessageBase parsedMessage = PanAndTilt.Common.MessageBase.ParseJson(messageBody);

                if (parsedMessage is SetDirectionMessage)
                {
                    SetDirectionMessage setDirectionMessage = (SetDirectionMessage)parsedMessage;

                    var newPan = setDirectionMessage.expectedPan;
                    var newTilt = setDirectionMessage.expectedTilt;

                    logger.LogInformation($"positionCommandsCallback - Sending SetDirectionMessage to the Rasbperry pico board. Pan: {newPan} Tilt: {newTilt}");

                    var setDirection = new SetDirectionMessage() { expectedPan = newPan, expectedTilt = newTilt };
                    var messagePayload = $"{setDirection.ToJson()}";
                                       
                    //Send over the Serial Port
                    serialPort.WriteLine(messagePayload);

                    if ((message.Properties.ContainsKey("updateTwin")) && (message.Properties["updateTwin"] == "true"))
                    {
                        //update the Twin just on demand otherwise let's do the reporting as usual based on time
                        lastReportedTwinTime = DateTime.MinValue;
                    }

                    logger.LogDebug("positionCommandsCallback - Commanded a new position");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error while parsing and processing the message: {ex.Message}");
            }
            
            logger.LogDebug("positionCommandsCallback - exiting");

            await Task.Delay(1);

            return MessageResponse.Completed;
        }
    }
}
