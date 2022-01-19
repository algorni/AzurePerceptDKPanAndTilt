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

    using System.IO.Ports;

    class Program
    {
        static ModuleClient moduleClient;

        static SerialPort serialPort;

        static void Main(string[] args)
        {
            Init().Wait();

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
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
             moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await moduleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            Console.WriteLine("Opening Serial Port...");

            serialPort = new SerialPort("/dev/ttyAM0", 9600, Parity.None, 8, StopBits.One);
            serialPort.Handshake = Handshake.None;

            try
            {
                serialPort.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while opening the Serial Port\n{ex.ToString()}");

                return;
            }

            Console.WriteLine("Read Twins at Module startup");

            //Get Twins at the startup.
            Twin twins = await moduleClient.GetTwinAsync();



            Console.WriteLine("Registering Desired Properties Update");

            // Register a callback to the Desired Property Update event
            await moduleClient.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback, moduleClient);

            // Register callback to be called when a message is received by the module
            //await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync("input1", PipeMessage, moduleClient);
        }

 

        static async Task desiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            //desired Pan
            var desiredPan = desiredProperties["pan"];
            //desired Tilt
            var desiredTilt = desiredProperties["tilt"];


            //using the serial port link send a call to the remote Rasbperry PI Pico Python code
            serialPort.WriteLine("");


            //receive the response of the call
            var raspiResponseString = "";

            PanTilt panTiltResponse = PanTilt.FromJSON(raspiResponseString);

            //report status 
            TwinCollection reportedProperties = new TwinCollection();

            reportedProperties["pan"] = panTiltResponse.Pan;
            reportedProperties["tilt"] = panTiltResponse.Tilt;

            await moduleClient.UpdateReportedPropertiesAsync(reportedProperties);            
        }


        /*
                /// <summary>
                /// This method is called whenever the module is sent a message from the EdgeHub. 
                /// It just pipe the messages without any change.
                /// It prints all the incoming messages.
                /// </summary>
                static async Task<MessageResponse> PipeMessage(Message message, object userContext)
                {
                    int counterValue = Interlocked.Increment(ref counter);

                    var moduleClient = userContext as ModuleClient;
                    if (moduleClient == null)
                    {
                        throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
                    }

                    byte[] messageBytes = message.GetBytes();
                    string messageString = Encoding.UTF8.GetString(messageBytes);
                    Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

                    if (!string.IsNullOrEmpty(messageString))
                    {
                        using (var pipeMessage = new Message(messageBytes))
                        {
                            foreach (var prop in message.Properties)
                            {
                                pipeMessage.Properties.Add(prop.Key, prop.Value);
                            }
                            await moduleClient.SendEventAsync("output1", pipeMessage);

                            Console.WriteLine("Received message sent");
                        }
                    }
                    return MessageResponse.Completed;
                }

                */


    }
}
