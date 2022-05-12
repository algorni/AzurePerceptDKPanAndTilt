using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PanAndTilt.Common
{
    public class COMPortDetector
    {
        public static async Task<SerialPort> OpenPanTiltSerial(ILogger logger)
        {
            string serialPortName = Environment.GetEnvironmentVariable("RASPI_SERIAL_DATA");
            
            if ( string.IsNullOrEmpty(serialPortName))
            {
                Console.WriteLine("Missing RASPI_SERIAL_DATA env variable value!");

                return null;
            }

            logger.LogInformation($"Opening Serial Port {serialPortName}.");

            SerialPort serialPort = new SerialPort(serialPortName, 9600, Parity.None, 8, StopBits.One);
            serialPort.Handshake = Handshake.None;

            try
            {
                serialPort.ReadTimeout = 10000;
                serialPort.WriteTimeout = 10000;
                
                serialPort.Open();

                serialPort.DtrEnable = true;

                logger.LogInformation($"Serial Port {serialPortName} open!");                                
            }
            catch (Exception ex)
            {
                logger.LogError($"Error while opening Serial Port {serialPortName}.\n{ex.Message}");

                if (serialPort.IsOpen)
                    serialPort.Close();

                serialPort.Dispose();

                return null;
            }

            return serialPort;
        }



        public static async Task<SerialPort> TryFindPanTiltControlBoard(CancellationToken cancellationToken)
        {
            Console.WriteLine("\nTry Find PanTilt Control Board\n");

            SerialPort selectedSerialPort = null;

            //the idea here is to detect the Serial port listening to the incoming messages.
            //if the messages reppresent the expected messages received from the board that's the port!

            var serialPorts = SerialPort.GetPortNames();

            Console.WriteLine($"Found the following com ports:");

            foreach (var serialPort in serialPorts)
            {
                Console.WriteLine($"   {serialPort}");
            }

            Console.WriteLine();

            Dictionary<string, SerialPort> ports = new Dictionary<string, SerialPort>();

            while (selectedSerialPort == null && !cancellationToken.IsCancellationRequested)
            {
                foreach (var serialPortName in serialPorts)
                {
                    if (!serialPortName.Contains("ttyACM"))
                    {
                        //not an expected port name
                        Console.WriteLine($"{serialPortName} is not an expected Serial Port Name for Raspberry PI Pico. Skip this one.");
                        continue;
                    }

                    Console.WriteLine($"Opening Serial Port {serialPortName} and waiting for a message from the PanTilt Control Board.");

                    SerialPort serialPort = null;

                    if (ports.ContainsKey(serialPortName))
                    {
                        serialPort = ports[serialPortName];
                    }
                    else
                    {
                        serialPort = new SerialPort(serialPortName,9600);
                        serialPort.ReadTimeout = 5000;
                        ports.Add(serialPortName, serialPort);
                    }

                    try
                    {
                        if (!serialPort.IsOpen)
                        {
                            serialPort.Open();
                            //serialPort.DiscardOutBuffer();
                            //serialPort.DiscardInBuffer();
                        }

                        //Wait to receive a line, or timeout...
                        var line = serialPort.ReadLine();

                        var parsedMessage = MessageBase.ParseJson(line);

                        selectedSerialPort = serialPort;

                        Console.WriteLine($"Yea!!! Serial Port {serialPortName} sounds the right one!\n{line}");

                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"No no, Serial Port {serialPortName} doesn't seems the one for the data exchange between Pan and tilt control board and Percept.\n{ex.Message}");

                        continue;
                    }
                }

                if (selectedSerialPort == null)
                {
                    Console.WriteLine("Serial port not found, retrying in a second...");
                    await Task.Delay(1000);
                }
            }

            //clean up a bit....
            IEnumerable<SerialPort> serialPortsToDispose = null;

            if (selectedSerialPort != null)
            {
                serialPortsToDispose = ports.Values.Where(p => p.PortName != selectedSerialPort.PortName);
            }
            else
            {
                serialPortsToDispose = ports.Values;
            }

            foreach (var serialPortToDispose in serialPortsToDispose)
            {
                if (serialPortToDispose.IsOpen)
                    serialPortToDispose.Close();

                serialPortToDispose.Dispose();
            }

            return selectedSerialPort;        
        }
    }
}
