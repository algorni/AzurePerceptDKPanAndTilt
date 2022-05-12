using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PanAndTilt.Common;
using percept2pantilt.Percept;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace percept2pantilt
{
    internal class Program
    {
        //the "delta" in the position of the baricenter of the detected picture has a range +/- 1 from the center of the image.
        //acceptance zone is a kind of deadzone which the tracker algorithm didn't responde and consider as acceptable area.
        private static double deltaThresholdX = 0.5;
        private static double deltaThresholdY = 0.5;

        //the acceptable threashold of the found image
        private static double labelConfidenceThreashold = 0.5;

        //the label to track
        private static string trackedLabel;

        //last time when the tracked label is detected
        private static DateTime lasdetection = DateTime.MaxValue;

        //last pan and tilt position (reported by the pan and tilt commander module)
        private static ReportDirectionMessage lastReportDirectionMessage = null;


        private static ILogger logger = null;

        private static ModuleClient moduleClient ;
        
        public static async Task Main(string[] args)
        {
            string logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL");

            if (!string.IsNullOrEmpty(logLevel))
            {
                if (Microsoft.Azure.Devices.Edge.Util.Logger.LogLevelDictionary.ContainsKey(logLevel))
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

            logger = Microsoft.Azure.Devices.Edge.Util.Logger.Factory.CreateLogger<Program>();

            const string SdkEventProviderPrefix = "Microsoft-Azure-";
            // Instantiating this seems to do all we need for outputting SDK events to our console log
            _ = new ConsoleEventListener(SdkEventProviderPrefix, logger);


            bool initDoneProperly = await Init();

            if (initDoneProperly)
            {
                logger.LogInformation("Initialization completed, now running in background.");
            }
            else
            {
                logger.LogInformation("Initialization completed but with some error.");
            }
                       
            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }


        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task<bool> Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await moduleClient.OpenAsync();
            logger.LogInformation("IoT Hub module client initialized.");


            trackedLabel = Environment.GetEnvironmentVariable("TRACKED_LABEL");

            if (!string.IsNullOrEmpty(trackedLabel))
            {
                logger.LogInformation($"Found TRACKED_LABEL value:{trackedLabel}");
            }
            else
            {
                logger.LogError("TRACKED_LABEL env variable not found, not sure what you want to track!");

                return false;
            }

            var labelConfidenceThreasholdstr = Environment.GetEnvironmentVariable("TRACKED_LABEL_THRESHOLD");

            if (!string.IsNullOrEmpty(labelConfidenceThreasholdstr))
            {
                logger.LogInformation($"Found TRACKED_LABEL_THRESHOLD value:{labelConfidenceThreasholdstr}");

                var parsed = double.TryParse(labelConfidenceThreasholdstr, out labelConfidenceThreashold);

                if (!parsed)
                {
                    logger.LogError($"TRACKED_LABEL_THRESHOLD value {labelConfidenceThreasholdstr} is not as float!");
                }
                else
                {
                    if ((labelConfidenceThreashold < 0) || (labelConfidenceThreashold > 1.0))
                    {
                        logger.LogError($"TRACKED_LABEL_THRESHOLD value {labelConfidenceThreasholdstr} is out of range 0..1 use standard value 0.5");
                        labelConfidenceThreashold = 0.5;
                    }
                }
            }
            else
            {
                logger.LogError("TRACKED_LABEL_THRESHOLD env variable not found, use default value of 0.5");
                labelConfidenceThreashold = 0.5;
            }



            var deltaThresholdXstr = Environment.GetEnvironmentVariable("DELTA_THRESHOLD_X");

            if (!string.IsNullOrEmpty(deltaThresholdXstr))
            {
                logger.LogInformation($"Found DELTA_THRESHOLD_X value:{deltaThresholdXstr}");

                var parsed = double.TryParse(labelConfidenceThreasholdstr, out deltaThresholdX);

                if (!parsed)
                {
                    logger.LogError($"DELTA_THRESHOLD_X value {deltaThresholdXstr} is not as float!");
                }
                else
                {
                    if ((deltaThresholdX < 0) || (deltaThresholdX > 1.0))
                    {
                        logger.LogError($"DELTA_THRESHOLD_X value {deltaThresholdXstr} is out of range 0..1 use standard value 0.5");
                        deltaThresholdX = 0.5;
                    }
                }
            }
            else
            {
                logger.LogError("DELTA_THRESHOLD_X env variable not found, use default value of 0.5");
                deltaThresholdX = 0.5;
            }


            var deltaThresholdYstr = Environment.GetEnvironmentVariable("DELTA_THRESHOLD_Y");

            if (!string.IsNullOrEmpty(deltaThresholdYstr))
            {
                logger.LogInformation($"Found DELTA_THRESHOLD_Y value:{deltaThresholdYstr}");

                var parsed = double.TryParse(labelConfidenceThreasholdstr, out deltaThresholdY);

                if (!parsed)
                {
                    logger.LogError($"DELTA_THRESHOLD_Y value {deltaThresholdYstr} is not as float!");
                }
                else
                {
                    if ((deltaThresholdY < 0) || (deltaThresholdY > 1.0))
                    {
                        logger.LogError($"DELTA_THRESHOLD_Y value {deltaThresholdYstr} is out of range 0..1 use standard value 0.5");
                        deltaThresholdY = 0.5;
                    }
                }
            }
            else
            {
                logger.LogError("DELTA_THRESHOLD_Y env variable not found, use default value of 0.5");
                deltaThresholdY = 0.5;
            }



            // Register callback to be called when a message is received by the module
            await moduleClient.SetInputMessageHandlerAsync("perceptAIMessagesInput", ProcessPerceptMessage, moduleClient);

            // Register callback to be called when a message is received by the module
            await moduleClient.SetInputMessageHandlerAsync("reportedDirectionInput", ProcessReportedDirectionMessage, moduleClient);


            return true; 
        }

        static async Task<MessageResponse> ProcessReportedDirectionMessage(Message message, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            logger.LogTrace($"Received message from pan & tilt engine - Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                ReportDirectionMessage reportDirectionMessage = MessageBase.ParseJson(messageString) as ReportDirectionMessage;

                if (reportDirectionMessage != null)
                {
                    logger.LogTrace("Parsed a ReportDirectionMessage properly!");

                    lastReportDirectionMessage = reportDirectionMessage;
                }
                else
                {
                    logger.LogWarning($"Weird message in input: {messageString}");
                }
            }


            return MessageResponse.Completed;
        }

        



        static async Task<MessageResponse> ProcessPerceptMessage(Message message, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            logger.LogDebug($"Received message from AI Module - Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                var perceptAIMessages = JsonConvert.DeserializeObject<PerceptAIMessages>(messageString);

                var aiTrackedItem = perceptAIMessages.NEURAL_NETWORK.FirstOrDefault(ai => ai.label == trackedLabel);

                if (aiTrackedItem != null)
                {
                    double confidence = 0;

                    double.TryParse(aiTrackedItem.confidence, out confidence);

                    //just move in case confidence is above a certain treshold
                    if (confidence > labelConfidenceThreashold)
                    {
                        //this is the delta respect of the center of the picture of the baricenter of the detected item bounding box
                        var delta = aiTrackedItem.GetDelta();

                        //now calculate the new pan & tilt position
                        SetDirectionMessage setDirectionMessage = calculatePosition(delta);

                        if (setDirectionMessage != null)
                        {
                            //emit into the output port of the module the current direction!
                            var stringBody = setDirectionMessage.ToJson();
                            var outputMessageBytes = Encoding.UTF8.GetBytes(stringBody);

                            using (var pipeMessage = new Message(outputMessageBytes))
                            {
                                foreach (var prop in message.Properties)
                                {
                                    pipeMessage.Properties.Add(prop.Key, prop.Value);
                                }

                                pipeMessage.Properties.Add("content/type", "application/json");
                                pipeMessage.Properties.Add("MessageType", "ReportDirectionMessage");

                                await moduleClient.SendEventAsync("positionCommandOutput", pipeMessage);

                                logger.LogInformation($"PipeMessage - - - - sent edgeHub output message over commandMessageOutput port: {stringBody}");
                            }
                        }                        
                    }
                    else
                    {
                        logger.LogDebug($"Confidence below treshold {confidence}. Minimum value expected is {labelConfidenceThreashold}");
                    }
                }
                else
                {
                    logger.LogDebug($"No tracked label found.  Expected {trackedLabel}.");
                }
            }

            return MessageResponse.Completed;
        }

        /// <summary>
        /// calculate pan tilt direction 
        /// </summary>
        /// <param name="delta"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private static SetDirectionMessage calculatePosition(DeltaPos delta)
        {
            logger.LogDebug($"  CalculatePosition - Delta x:{delta.deltaXFromCenter}, Delta y:{delta.deltaYFromCenter}");

            SetDirectionMessage setDirectionMessage = null;

            if (lastReportDirectionMessage != null)
            {
                logger.LogDebug($"  CalculatePosition - Current Pan: {lastReportDirectionMessage.expectedPan}, Current Tilt:{lastReportDirectionMessage.expectedTilt}");

                //send a command just if the object detected is not in the central zone...
                if ( (Math.Abs(delta.deltaXFromCenter) > deltaThresholdX) || (Math.Abs(delta.deltaYFromCenter) > deltaThresholdY) )
                {
                    setDirectionMessage = new SetDirectionMessage();

                    setDirectionMessage.expectedPan = calculatePanFromDelta(lastReportDirectionMessage.expectedPan, delta.deltaXFromCenter);
                    setDirectionMessage.expectedTilt = calculateTiltFromDelta(lastReportDirectionMessage.expectedTilt, delta.deltaYFromCenter);

                    logger.LogDebug($"  Set Direction: Calculated Pan:{setDirectionMessage.expectedPan}, Calculated Tilt:{setDirectionMessage.expectedTilt}");
                }
                else
                {
                    logger.LogDebug("  Object already centered, not moving.");
                }
            }
            else
            {
                logger.LogWarning($"  LastReportDirectionMessage is null. Impossible estimate the new direction right now!");
            }
            
            return setDirectionMessage;
        }

        private static int calculatePanFromDelta(int currentDegree, double deltaFromCenter)
        {
            double absDegree = Math.Pow(45.0, Math.Abs(deltaFromCenter));

            int degree = currentDegree + (int)( (deltaFromCenter / Math.Abs(deltaFromCenter)) * absDegree);

            if (degree < 0)
                return 0;

            if (degree > 180)
                return 180;

            return degree;
        }


        private static int calculateTiltFromDelta(int currentDegree, double deltaFromCenter)
        {
            //tilt is less sensible to movement
            double absDegree = Math.Pow(20.0, Math.Abs(deltaFromCenter));

            int degree = currentDegree + (int)((deltaFromCenter / Math.Abs(deltaFromCenter)) * absDegree);

            if (degree < 0)
                return 0;

            if (degree > 180)
                return 180;

            return degree;
        }
    }
}
