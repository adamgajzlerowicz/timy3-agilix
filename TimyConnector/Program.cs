using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace TimyConnector
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Timy3 Connector Application");
            Console.WriteLine("---------------------------");
            
            try
            {
                // Initialize the Timy3 device
                Console.WriteLine("Initializing Timy3 device...");
                var timy = new Alge.TimyUsb(null);
                
                // Setup event handlers
                timy.DeviceConnected += Timy_DeviceConnected;
                timy.DeviceDisconnected += Timy_DeviceDisconnected;
                timy.LineReceived += Timy_LineReceived;
                timy.RawReceived += Timy_RawReceived;
                timy.PnPDeviceAttached += Timy_PnPDeviceAttached;
                timy.PnPDeviceDetached += Timy_PnPDeviceDetached;
                
                Console.WriteLine("Starting Timy USB service...");
                timy.Start();
                
                Console.WriteLine("Process is " + (IntPtr.Size == 8 ? "x64" : "x86"));
                Console.WriteLine("Waiting for device connections...");
                Console.WriteLine("Press Enter to exit.");
                
                Console.ReadLine();
                
                Console.WriteLine("Stopping Timy USB service...");
                timy.Stop();
                
                // Cleanup event handlers
                timy.DeviceConnected -= Timy_DeviceConnected;
                timy.DeviceDisconnected -= Timy_DeviceDisconnected;
                timy.LineReceived -= Timy_LineReceived;
                timy.RawReceived -= Timy_RawReceived;
                timy.PnPDeviceAttached -= Timy_PnPDeviceAttached;
                timy.PnPDeviceDetached -= Timy_PnPDeviceDetached;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void Timy_PnPDeviceDetached(object sender, EventArgs e)
        {
            Console.WriteLine("Device detached from system");
        }

        private static void Timy_PnPDeviceAttached(object sender, EventArgs e)
        {
            Console.WriteLine("Device attached to system");
        }

        private static void Timy_RawReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            if (!e.Data.StartsWith("TIMY:"))
                Console.WriteLine($"Device {e.Device.Id} Raw: {e.Data}");
        }

        private static void Timy_LineReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            Console.WriteLine($"Device {e.Device.Id} Line: {e.Data}");
        }

        private static void Timy_DeviceDisconnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            Console.WriteLine($"Device {e.Device.Id} disconnected");
        }

        private static void Timy_DeviceConnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            Console.WriteLine($"Device {e.Device.Id} connected");
        }
    }
} 