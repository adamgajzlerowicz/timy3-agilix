using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace NewTimyConnector
{
    public class TimyWebSocketServer
    {
        private WebSocketServer? server;
        private int port;
        private bool isRunning = false;
        private Action<string, string>? onMessageReceived;
        
        public TimyWebSocketServer(int port = 8080, Action<string, string>? onMessageReceived = null)
        {
            this.port = port;
            this.onMessageReceived = onMessageReceived;
        }
        
        public bool Start()
        {
            try
            {
                if (isRunning)
                    return true;
                    
                server = new WebSocketServer(port);
                
                // Add the Timy3 behavior
                server.AddWebSocketService<Timy3WebSocketBehavior>("/timy3", 
                    behavior => behavior.SetMessageReceivedCallback(onMessageReceived));
                
                server.Start();
                isRunning = true;
                
                Console.WriteLine($"WebSocket server started on port {port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting WebSocket server: {ex.Message}");
                return false;
            }
        }
        
        public void Stop()
        {
            if (!isRunning || server == null)
                return;
                
            try
            {
                server.Stop();
                isRunning = false;
                Console.WriteLine("WebSocket server stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping WebSocket server: {ex.Message}");
            }
        }
        
        public void Broadcast(string message)
        {
            if (!isRunning || server == null)
                return;
                
            try
            {
                var sessions = server.WebSocketServices["/timy3"].Sessions;
                sessions.Broadcast(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting message: {ex.Message}");
            }
        }
        
        public void BroadcastJson(object data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data);
                Broadcast(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error serializing JSON: {ex.Message}");
            }
        }
        
        public bool IsRunning => isRunning;
    }
    
    public class Timy3WebSocketBehavior : WebSocketBehavior
    {
        private Action<string, string>? onMessageReceived;
        
        public void SetMessageReceivedCallback(Action<string, string>? callback)
        {
            onMessageReceived = callback;
        }
        
        protected override void OnOpen()
        {
            base.OnOpen();
            Console.WriteLine($"WebSocket client connected: {ID}");
        }
        
        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            Console.WriteLine($"WebSocket client disconnected: {ID}. Code: {e.Code}, Reason: {e.Reason}");
        }
        
        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                string message = e.Data;
                Console.WriteLine($"Message received from client {ID}: {message}");
                
                // Pass the message to the callback
                onMessageReceived?.Invoke(ID, message);
                
                // Echo the message back as acknowledgement
                Send($"{{\"status\": \"received\", \"message\": \"{message}\"}}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
        
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            base.OnError(e);
            Console.WriteLine($"WebSocket error for client {ID}: {e.Message}");
        }
    }
} 