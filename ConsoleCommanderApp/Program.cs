using Newtonsoft.Json;
using System;
using System.IO.Ports;

namespace ConsoleCommanderApp
{
    class Program
    {
        static SerialPort serialPort;


        static void Main(string[] args)
        {
            Console.WriteLine("Hello Serial!");


            Console.WriteLine("Getting the list of Serial Port...");

            var serialPorts = SerialPort.GetPortNames();

            string raspiSerialPortName = string.Empty;

            foreach(var serialPortName in serialPorts)
            {
                bool contains_ttyACM = serialPortName.Contains("ttyACM");

                Console.WriteLine($"{serialPortName} - potential raspi pico {contains_ttyACM}");

                if (contains_ttyACM)
                {
                    raspiSerialPortName = serialPortName;
                }
            }


            Console.WriteLine($"Opening Serial Port {raspiSerialPortName}");

            serialPort = new SerialPort(raspiSerialPortName, 9600, Parity.None, 8, StopBits.One);
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


            Console.WriteLine("Sending a command to the Raspi PI Pico over Serial Port...");

            //using the serial port link send a call to the remote Rasbperry PI Pico Python code
            serialPort.WriteLine($"pan({0})");

            Console.WriteLine("Receiving the response from the serial...");

            //receive the response of the call
            var raspiResponseString = serialPort.ReadLine();


            Console.WriteLine("Deserializing the response....");

            PanTilt panTiltResponse = PanTilt.FromJSON(raspiResponseString);

            Console.WriteLine($"{panTiltResponse.ToString()}");
        }

        /// <summary>
        /// Pan and Tilt entity
        /// </summary>
        public class PanTilt
        {
            public int Pan { get; set; }
            public int Tilt { get; set; }


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
