using Newtonsoft.Json;
using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleCommanderApp
{
    class Program
    {
        static SerialPort serialPort;

        static string CTRL_A = "\x01"; // raw repl
        static string CTRL_B = "\x02"; // exit raw repl
        static string CTRL_C = "\x03"; // ctrl-c
        static string CTRL_D = "\x04"; // reset (ctrl-d)
        static string CTRL_E = "\x05"; // paste mode (ctrl-e)
        static string CTRL_F = "\x06"; // safe boot (ctrl-f)


        static async Task  Main(string[] args)
        {
            Console.WriteLine("Hello Serial!");

            string raspiSerialPortName = string.Empty;

            var overriddenSerialPort = Environment.GetEnvironmentVariable("forcedSerialPort");
            overriddenSerialPort = "COM2";



            if (!string.IsNullOrEmpty(overriddenSerialPort))
            {
                Console.WriteLine($"Serial port is provided as Eng Variable: {overriddenSerialPort}");

                raspiSerialPortName = overriddenSerialPort;
            }
            else
            {            
                Console.WriteLine("Getting the list of Serial Port...");

                var serialPorts = SerialPort.GetPortNames();

                foreach (var serialPortName in serialPorts)
                {
                    bool contains_ttyACM = serialPortName.Contains("ttyACM");

                    if (contains_ttyACM)
                    {
                        Console.WriteLine($"{serialPortName} - potential raspi pico {contains_ttyACM}");

                        raspiSerialPortName = serialPortName;
                    }
                }
            }

            if (string.IsNullOrEmpty(raspiSerialPortName))
            {
                Console.WriteLine($"Not found any potential Serial Port");
            }
            else
            {
                Console.WriteLine($"Opening Serial Port {raspiSerialPortName}");

                serialPort = new SerialPort(raspiSerialPortName, 115200, Parity.None, 8, StopBits.One);
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

                serialPort.DiscardInBuffer();

                await Task.Delay(300);

                Random random = new Random();
                var nextPan = random.Next(0, 180);

                string payload = $"pan({nextPan})";

                Console.WriteLine($"Sending a command to the Raspi PI Pico over Serial Port: '{payload}'");
                               

                byte[] sendBuffer = Encoding.UTF8.GetBytes(payload + "\r\f");

                //using the serial port link send a call to the remote Rasbperry PI Pico Python code
                serialPort.Write(sendBuffer, 0, sendBuffer.Length);

                var echoed = serialPort.ReadExisting();
                Console.WriteLine($"{echoed}");

                Console.WriteLine("Receiving the response from the serial...");

                //receive the response of the call
                var raspiResponseString = serialPort.ReadLine();


                Console.WriteLine("Deserializing the response....");

                PanTilt panTiltResponse = PanTilt.FromJSON(raspiResponseString);

                Console.WriteLine($"{panTiltResponse.ToString()}");
            }
            
            while (true)
            {
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// Pan and Tilt entity
        /// </summary>
        public class PanTilt
        {
            public int pan { get; set; }
            public int tilt { get; set; }

            public double ax { get; set; }
            public double ay { get; set; }
            public double az { get; set; }


            public double mx { get; set; }
            public double my { get; set; }
            public double mz { get; set; }



            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }

            /// <summary>
            /// From JSON constructor
            /// </summary>
            /// <param name="json"></param>
            /// <returns></returns>
            public static PanTilt FromJSON(string json)
            {
                PanTilt panTilt = JsonConvert.DeserializeObject<PanTilt>(json);

                return panTilt;
            }
        }
    }
}
