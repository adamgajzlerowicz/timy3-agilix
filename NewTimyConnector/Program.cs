using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NewTimyConnector
{
    class Program
    {
        private static Alge.TimyUsb? timy;
        private static bool isRunning = true;
        private static ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        private static ConcurrentDictionary<string, List<string>> deviceMessages = new ConcurrentDictionary<string, List<string>>();
        private static TimyWebSocketServer? webSocketServer;
        private static int webSocketPort = 8080;
        
        static void Main(string[] args)
        {
            Console.WriteLine("Timy3 Connector Application");
            Console.WriteLine("---------------------------");
            
            try
            {
                // Initialize the Timy3 device
                Console.WriteLine("Initializing Timy3 device...");
                timy = new Alge.TimyUsb(null);
                
                // Setup event handlers
                timy.DeviceConnected += Timy_DeviceConnected;
                timy.DeviceDisconnected += Timy_DeviceDisconnected;
                timy.LineReceived += Timy_LineReceived;
                timy.RawReceived += Timy_RawReceived;
                timy.PnPDeviceAttached += Timy_PnPDeviceAttached;
                timy.PnPDeviceDetached += Timy_PnPDeviceDetached;
                
                Console.WriteLine("Starting Timy USB service...");
                timy.Start();
                
                Console.WriteLine($"Process is {(IntPtr.Size == 8 ? "x64" : "x86")}");
                Console.WriteLine("Waiting for device connections...");
                
                // Initialize the WebSocket server
                InitializeWebSocketServer();
                
                // Start a background task to process messages
                Task.Run(() => ProcessMessageQueue());
                
                // Command processing loop
                ShowHelp();
                while (isRunning)
                {
                    string command = Console.ReadLine().Trim();
                    ProcessCommand(command);
                }
                
                Console.WriteLine("Stopping Timy USB service...");
                timy.Stop();
                
                // Stop the WebSocket server
                webSocketServer?.Stop();
                
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

        private static void InitializeWebSocketServer()
        {
            try
            {
                webSocketServer = new TimyWebSocketServer(webSocketPort, OnWebSocketMessageReceived);
                if (webSocketServer.Start())
                {
                    messageQueue.Enqueue($"WebSocket server started on port {webSocketPort}");
                    messageQueue.Enqueue($"Connect to ws://localhost:{webSocketPort}/timy3");
                }
                else
                {
                    messageQueue.Enqueue("Failed to start WebSocket server");
                }
            }
            catch (Exception ex)
            {
                messageQueue.Enqueue($"Error initializing WebSocket server: {ex.Message}");
            }
        }

        private static void OnWebSocketMessageReceived(string clientId, string message)
        {
            try
            {
                messageQueue.Enqueue($"WebSocket message from {clientId}: {message}");
                
                // Process the received message
                // This could be command to the device or a request for data
                if (message.StartsWith("send:"))
                {
                    string deviceCommand = message.Substring(5);
                    SendToDevice(deviceCommand);
                }
                else if (message == "getDevices")
                {
                    SendDeviceListToWebSocket();
                }
                else if (message == "getStatus")
                {
                    SendStatusToWebSocket();
                }
            }
            catch (Exception ex)
            {
                messageQueue.Enqueue($"Error processing WebSocket message: {ex.Message}");
            }
        }

        private static void SendDeviceListToWebSocket()
        {
            var deviceList = new
            {
                type = "deviceList",
                count = timy?.ConnectedDevicesCount ?? 0,
                devices = deviceMessages.Keys
            };
            
            webSocketServer?.BroadcastJson(deviceList);
        }
        
        private static void SendStatusToWebSocket()
        {
            var status = new
            {
                type = "status",
                running = (timy != null),
                deviceCount = timy?.ConnectedDevicesCount ?? 0,
                messageCount = GetTotalMessageCount()
            };
            
            webSocketServer?.BroadcastJson(status);
        }
        
        private static void ProcessMessageQueue()
        {
            while (isRunning)
            {
                if (messageQueue.TryDequeue(out string message))
                {
                    Console.WriteLine(message);
                }
                Thread.Sleep(10);
            }
        }

        private static void ProcessCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return;

            switch (command.ToLower())
            {
                case "exit":
                case "quit":
                    isRunning = false;
                    break;
                    
                case "help":
                    ShowHelp();
                    break;
                    
                case "devices":
                    ShowConnectedDevices();
                    break;
                    
                case "clear":
                    Console.Clear();
                    break;
                    
                case "status":
                    ShowStatus();
                    break;
                    
                case "websocket start":
                    if (webSocketServer == null || !webSocketServer.IsRunning)
                    {
                        InitializeWebSocketServer();
                    }
                    else
                    {
                        messageQueue.Enqueue("WebSocket server is already running");
                    }
                    break;
                    
                case "websocket stop":
                    if (webSocketServer != null && webSocketServer.IsRunning)
                    {
                        webSocketServer.Stop();
                        messageQueue.Enqueue("WebSocket server stopped");
                    }
                    else
                    {
                        messageQueue.Enqueue("WebSocket server is not running");
                    }
                    break;
                    
                case "websocket status":
                    if (webSocketServer != null)
                    {
                        messageQueue.Enqueue($"WebSocket server is {(webSocketServer.IsRunning ? "running" : "stopped")} on port {webSocketPort}");
                    }
                    else
                    {
                        messageQueue.Enqueue("WebSocket server is not initialized");
                    }
                    break;
                    
                default:
                    if (command.StartsWith("send "))
                    {
                        string message = command.Substring(5);
                        SendToDevice(message);
                    }
                    else if (command.StartsWith("websocket port "))
                    {
                        if (int.TryParse(command.Substring(15), out int port))
                        {
                            if (port > 0 && port < 65536)
                            {
                                bool needsRestart = webSocketServer != null && webSocketServer.IsRunning;
                                
                                if (needsRestart)
                                {
                                    webSocketServer.Stop();
                                }
                                
                                webSocketPort = port;
                                messageQueue.Enqueue($"WebSocket port set to {webSocketPort}");
                                
                                if (needsRestart)
                                {
                                    InitializeWebSocketServer();
                                }
                            }
                            else
                            {
                                messageQueue.Enqueue("Invalid port number. Must be between 1 and 65535.");
                            }
                        }
                        else
                        {
                            messageQueue.Enqueue("Invalid port number format");
                        }
                    }
                    else
                    {
                        messageQueue.Enqueue($"Unknown command: {command}. Type 'help' for available commands.");
                    }
                    break;
            }
        }

        private static void ShowHelp()
        {
            messageQueue.Enqueue("Available commands:");
            messageQueue.Enqueue("  help                 - Show this help");
            messageQueue.Enqueue("  devices              - Show connected devices");
            messageQueue.Enqueue("  status               - Show current status");
            messageQueue.Enqueue("  send <msg>           - Send a message to all connected devices");
            messageQueue.Enqueue("  clear                - Clear the console");
            messageQueue.Enqueue("  websocket start      - Start the WebSocket server");
            messageQueue.Enqueue("  websocket stop       - Stop the WebSocket server");
            messageQueue.Enqueue("  websocket status     - Show WebSocket server status");
            messageQueue.Enqueue("  websocket port <num> - Set WebSocket server port");
            messageQueue.Enqueue("  exit/quit            - Exit the application");
        }

        private static void ShowConnectedDevices()
        {
            int count = timy?.ConnectedDevicesCount ?? 0;
            messageQueue.Enqueue($"Connected devices: {count}");
            
            foreach (var device in deviceMessages.Keys)
            {
                messageQueue.Enqueue($"  - Device ID: {device}");
            }
        }

        private static void ShowStatus()
        {
            messageQueue.Enqueue($"Timy USB service running: {(timy != null)}");
            messageQueue.Enqueue($"Connected devices: {timy?.ConnectedDevicesCount ?? 0}");
            messageQueue.Enqueue($"Total messages received: {GetTotalMessageCount()}");
            
            if (webSocketServer != null)
            {
                messageQueue.Enqueue($"WebSocket server: {(webSocketServer.IsRunning ? "Running" : "Stopped")} on port {webSocketPort}");
            }
            else
            {
                messageQueue.Enqueue("WebSocket server: Not initialized");
            }
        }

        private static int GetTotalMessageCount()
        {
            int count = 0;
            foreach (var messages in deviceMessages.Values)
            {
                count += messages.Count;
            }
            return count;
        }

        private static void SendToDevice(string message)
        {
            try
            {
                if (timy != null)
                {
                    timy.Send(message + "\r");
                    messageQueue.Enqueue($"Sent message: {message}");
                }
                else
                {
                    messageQueue.Enqueue("Error: Timy USB service is not running");
                }
            }
            catch (Exception ex)
            {
                messageQueue.Enqueue($"Error sending message: {ex.Message}");
            }
        }

        private static void Timy_PnPDeviceDetached(object sender, EventArgs e)
        {
            messageQueue.Enqueue("Device detached from system");
        }

        private static void Timy_PnPDeviceAttached(object sender, EventArgs e)
        {
            messageQueue.Enqueue("Device attached to system");
        }

        private static void Timy_RawReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            if (!e.Data.StartsWith("TIMY:"))
            {
                string deviceId = e.Device.Id.ToString();
                string message = $"Device {deviceId} Raw: {e.Data}";
                messageQueue.Enqueue(message);
                StoreDeviceMessage(deviceId, message);
                
                // Forward to WebSocket clients
                if (webSocketServer != null && webSocketServer.IsRunning)
                {
                    var data = new
                    {
                        type = "raw",
                        deviceId = deviceId,
                        message = e.Data,
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    };
                    
                    webSocketServer.BroadcastJson(data);
                }
            }
        }

        private static void Timy_LineReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            string deviceId = e.Device.Id.ToString();
            string message = $"Device {deviceId} Line: {e.Data}";
            messageQueue.Enqueue(message);
            StoreDeviceMessage(deviceId, message);
            
            // Forward to WebSocket clients
            if (webSocketServer != null && webSocketServer.IsRunning)
            {
                var data = new
                {
                    type = "line",
                    deviceId = deviceId,
                    message = e.Data,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
                
                webSocketServer.BroadcastJson(data);
            }
        }

        private static void Timy_DeviceDisconnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            string deviceId = e.Device.Id.ToString();
            string message = $"Device {deviceId} disconnected";
            messageQueue.Enqueue(message);
            
            // Notify WebSocket clients
            if (webSocketServer != null && webSocketServer.IsRunning)
            {
                var data = new
                {
                    type = "deviceDisconnected",
                    deviceId = deviceId,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
                
                webSocketServer.BroadcastJson(data);
            }
        }

        private static void Timy_DeviceConnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            string deviceId = e.Device.Id.ToString();
            string message = $"Device {deviceId} connected";
            messageQueue.Enqueue(message);
            
            // Initialize message storage for this device
            deviceMessages.TryAdd(deviceId, new List<string>());
            
            // Notify WebSocket clients
            if (webSocketServer != null && webSocketServer.IsRunning)
            {
                var data = new
                {
                    type = "deviceConnected",
                    deviceId = deviceId,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                };
                
                webSocketServer.BroadcastJson(data);
            }
        }
        
        private static void StoreDeviceMessage(string deviceId, string message)
        {
            if (deviceMessages.TryGetValue(deviceId, out var messages))
            {
                messages.Add(message);
                
                // Limit the number of stored messages to avoid memory issues
                if (messages.Count > 1000)
                {
                    messages.RemoveAt(0);
                }
            }
        }
    }
}
