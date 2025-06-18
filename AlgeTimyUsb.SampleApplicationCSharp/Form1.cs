using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace AlgeTimyUsb.SampleApplication
{
    public partial class Form1 : Form
    {

        Alge.TimyUsb timyUsb;
        private HttpListener httpListener;
        private CancellationTokenSource webSocketCancellation;
        private List<WebSocket> connectedClients = new List<WebSocket>();
        private object clientsLock = new object();
        private DateTime? startTime;
        private string lastStartTimeString;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            AddLogLine("Process is " + (IntPtr.Size == 8 ? "x64" : "x86"));
            
            try
            {
                // Start WebSocket server in a separate task to avoid blocking UI
                Task.Run(() => StartWebSocketServer());
                
                // Initialize TimyUsb
                timyUsb = new Alge.TimyUsb(this);
                
                // Set up event handlers
                timyUsb.DeviceConnected += new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceConnected);
                timyUsb.DeviceDisconnected += new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceDisconnected);
                timyUsb.LineReceived += new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_LineReceived);
                timyUsb.BytesReceived += timyUsb_BytesReceived;
                timyUsb.RawReceived += new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_RawReceived);
                timyUsb.PnPDeviceAttached += new EventHandler(timyUsb_PnPDeviceAttached);
                timyUsb.PnPDeviceDetached += new EventHandler(timyUsb_PnPDeviceDetached);
                timyUsb.HeartbeatReceived += new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);
                
                // Start TimyUsb
                timyUsb.Start();
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                
                // Check for already connected devices with a small delay
                this.BeginInvoke(new Action(() => {
                    int connectedCount = timyUsb.ConnectedDevicesCount;
                    if (connectedCount > 0)
                    {
                        AddLogLine("Found " + connectedCount + " device(s) already connected at startup");
                        Send("PROG");
                    }
                    else
                    {
                        AddLogLine("No devices connected at startup");
                    }
                }));
            }
            catch (Exception ex)
            {
                AddLogLine("Error initializing TimyUsb: " + ex.Message);
                btnStart.Enabled = false;
                btnStop.Enabled = false;
            }
        }

        private void StartWebSocketServer()
        {
            try
            {
                webSocketCancellation = new CancellationTokenSource();
                httpListener = new HttpListener();
                
                // Only listen on localhost to reduce startup time and security issues
                httpListener.Prefixes.Add("http://localhost:8080/");
                
                httpListener.Start();
                
                this.BeginInvoke(new Action(() => {
                    AddLogLine("WebSocket server started at ws://localhost:8080/timy3");
                }));
                
                AcceptWebSocketClientsAsync(webSocketCancellation.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() => {
                    AddLogLine("Error starting WebSocket server: " + ex.Message);
                }));
            }
        }

        private async Task AcceptWebSocketClientsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    HttpListenerContext context = await httpListener.GetContextAsync();
                    
                    if (context.Request.IsWebSocketRequest && context.Request.Url.AbsolutePath == "/timy3")
                    {
                        ProcessWebSocketRequest(context, cancellationToken);
                    }
                    else
                    {
                        // Return a simple HTML page for non-WebSocket requests
                        using (var response = context.Response)
                        {
                            response.StatusCode = 200;
                            response.ContentType = "text/html";
                            string responseString = "<html><body><h1>Timy3 WebSocket Server</h1><p>This is a WebSocket server endpoint. Connect to ws://localhost:8080/timy3</p></body></html>";
                            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    this.BeginInvoke(new Action(() => {
                        AddLogLine("WebSocket server error: " + ex.Message);
                    }));
                }
            }
        }
        
        private async void ProcessWebSocketRequest(HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                WebSocket webSocket = webSocketContext.WebSocket;
                
                this.BeginInvoke(new Action(() => {
                    AddLogLine("WebSocket client connected");
                }));
                
                lock (clientsLock)
                {
                    connectedClients.Add(webSocket);
                }
                
                // Handle client in separate task
                _ = HandleWebSocketClientAsync(webSocket, cancellationToken);
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() => {
                    AddLogLine("Error accepting WebSocket connection: " + ex.Message);
                }));
            }
        }

        private async Task HandleWebSocketClientAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            
            try
            {
                while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellationToken);
                        
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            string.Empty,
                            cancellationToken);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // We could handle client commands here if needed
                    }
                }
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() => {
                    AddLogLine("WebSocket client error: " + ex.Message);
                }));
            }
            finally
            {
                lock (clientsLock)
                {
                    connectedClients.Remove(webSocket);
                }
                
                if (webSocket.State != WebSocketState.Closed)
                {
                    try
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection closed",
                            CancellationToken.None);
                    }
                    catch { }
                }
                
                this.BeginInvoke(new Action(() => {
                    AddLogLine("WebSocket client disconnected");
                }));
            }
        }

        private async Task BroadcastToWebSocketClientsAsync(string message)
        {
            List<WebSocket> clientsCopy;
            List<WebSocket> clientsToRemove = new List<WebSocket>();
            
            lock (clientsLock)
            {
                clientsCopy = new List<WebSocket>(connectedClients);
            }
            
            if (clientsCopy.Count == 0)
                return;
                
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var messageSegment = new ArraySegment<byte>(messageBytes);
            
            foreach (var client in clientsCopy)
            {
                if (client.State == WebSocketState.Open)
                {
                    try
                    {
                        await client.SendAsync(
                            messageSegment,
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
                    }
                    catch (Exception)
                    {
                        // Mark client for removal if sending fails
                        clientsToRemove.Add(client);
                    }
                }
                else
                {
                    // Mark client for removal if not open
                    clientsToRemove.Add(client);
                }
            }
            
            // Remove any failed clients
            if (clientsToRemove.Count > 0)
            {
                lock (clientsLock)
                {
                    foreach (var client in clientsToRemove)
                    {
                        connectedClients.Remove(client);
                    }
                }
                
                this.BeginInvoke(new Action(() => {
                    AddLogLine($"Removed {clientsToRemove.Count} disconnected WebSocket clients");
                }));
            }
        }

        void timyUsb_BytesReceived(object sender, Alge.BytesReceivedEventArgs e)
        {
            if (chkBytes.Checked)
                AddLogLine("Device " + e.Device.Id + " Bytes: " + e.Data.Length);
        }

        void timyUsb_HeartbeatReceived(object sender, Alge.HeartbeatReceivedEventArgs e)
        {
            if (chkHeartbeat.Checked)
                AddLogLine("Heartbeat: " + e.Time.ToString());
        }

        void timyUsb_PnPDeviceDetached(object sender, EventArgs e)
        {
            AddLogLine("Device detached");
        }

        void timyUsb_PnPDeviceAttached(object sender, EventArgs e)
        {
            AddLogLine("Device attached");
        }

        void timyUsb_RawReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            if ((!e.Data.StartsWith("TIMY:") || chkHeartbeat.Checked) && chkRaw.Checked)
                AddLogLine("Device " + e.Device.Id + " Raw: " + e.Data);
        }

        void timyUsb_LineReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            AddLogLine("Device " + e.Device.Id + " Line: " + e.Data);

            if (e.Data.StartsWith("PROG: "))
            {
                ProcessProgramResponse(e.Data);
            }
            else
            {
                // Process timing data in a separate task to avoid blocking the main thread
                Task.Run(() => ProcessTimingData(e.Data));
            }
        }

        private void ProcessTimingData(string data)
        {
            try
            {
                // Based on the README.md and the actual signals received
                // Start signal format: "Device 1 Line: 0007 C0M 10:54:11:31 01"
                // Finish signal format: "Device 1 Line: 0003 c1M 00005.22 01"
                
                string[] parts = data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Debug: Log all parts to help troubleshoot
                this.BeginInvoke(new Action(() => {
                    AddLogLine($"Signal parts: {string.Join(", ", parts)}");
                }));
                
                if (parts.Length >= 4)
                {
                    // Extract the channel code (e.g., "C0M", "c1M")
                    string channelCode = null;
                    int channelCodeIndex = -1;
                    
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string part = parts[i];
                        // Look for typical channel codes
                        if (part.Length >= 3 && 
                            (part.StartsWith("C", StringComparison.OrdinalIgnoreCase) || 
                             part.StartsWith("c", StringComparison.OrdinalIgnoreCase)) && 
                            part.EndsWith("M", StringComparison.OrdinalIgnoreCase))
                        {
                            channelCode = part;
                            channelCodeIndex = i;
                            break;
                        }
                    }
                    
                    this.BeginInvoke(new Action(() => {
                        AddLogLine($"Channel code detected: {channelCode ?? "none"} at position {channelCodeIndex}");
                    }));
                    
                    // Check for start signal (C0M/c0M)
                    bool isStartSignal = channelCode != null && 
                                        (channelCode.Equals("C0M", StringComparison.OrdinalIgnoreCase) || 
                                         channelCode.Equals("COM", StringComparison.OrdinalIgnoreCase));
                    
                    // Check for finish signal (C1M/c1M)
                    bool isFinishSignal = channelCode != null && 
                                         channelCode.Equals("C1M", StringComparison.OrdinalIgnoreCase);
                    
                    this.BeginInvoke(new Action(() => {
                        AddLogLine($"Signal analysis: isStart={isStartSignal}, isFinish={isFinishSignal}");
                    }));
                    
                    if (isStartSignal && channelCodeIndex >= 0 && channelCodeIndex + 1 < parts.Length)
                    {
                        // Get the time value from the position after the channel code
                        string timeStr = parts[channelCodeIndex + 1];
                        
                        if (!string.IsNullOrEmpty(timeStr))
                        {
                            lastStartTimeString = timeStr;
                            startTime = DateTime.Now; // We'll use this for calculating elapsed time
                            
                            // Send start signal to WebSocket clients
                            string startMessage = $"{{\"event\":\"start\",\"time\":\"{timeStr}\"}}";
                            BroadcastToWebSocketClientsAsync(startMessage).ConfigureAwait(false);
                            
                            this.BeginInvoke(new Action(() => {
                                AddLogLine($"Start signal detected: {timeStr}");
                            }));
                        }
                        else
                        {
                            this.BeginInvoke(new Action(() => {
                                AddLogLine("Start signal detected but could not find time value");
                            }));
                        }
                    }
                    else if (isFinishSignal && channelCodeIndex >= 0 && channelCodeIndex + 1 < parts.Length)
                    {
                        // Get the time value from the position after the channel code
                        string timeStr = parts[channelCodeIndex + 1];
                        
                        if (!string.IsNullOrEmpty(timeStr))
                        {
                            // Send finish signal to WebSocket clients with time
                            string finishMessage = $"{{\"event\":\"finish\",\"time\":\"{timeStr}\"}}";
                            BroadcastToWebSocketClientsAsync(finishMessage).ConfigureAwait(false);
                            
                            this.BeginInvoke(new Action(() => {
                                AddLogLine($"Finish signal detected: {timeStr}");
                            }));
                            
                            // Reset start time
                            startTime = null;
                        }
                        else
                        {
                            this.BeginInvoke(new Action(() => {
                                AddLogLine("Finish signal detected but could not find time value");
                            }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() => {
                    AddLogLine($"Error processing timing data: {ex.Message}");
                }));
            }
        }

        private void ProcessProgramResponse(string line)
        {
            String programString = line.Substring(6);
            AddLogLine("Active program: '" + programString + "'");
        }

        void timyUsb_DeviceDisconnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            AddLogLine("Device " + e.Device.Id + " disconnected, " + timyUsb.ConnectedDevicesCount + " total connected");
        }

        void timyUsb_DeviceConnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            AddLogLine("Device " + e.Device.Id + " connected, " + timyUsb.ConnectedDevicesCount + " total connected");
        }

        void AddLogLine(String str)
        {
            if (InvokeRequired)
                Invoke(new Action(() => { AddLogLine(str); }));
            else
                listBox1.Items.Insert(0, str);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop WebSocket server
            if (webSocketCancellation != null)
            {
                webSocketCancellation.Cancel();
                httpListener?.Stop();
                
                // Close all WebSocket connections
                lock (clientsLock)
                {
                    foreach (var client in connectedClients)
                    {
                        try
                        {
                            if (client.State == WebSocketState.Open)
                                client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait(1000);
                        }
                        catch { }
                    }
                    connectedClients.Clear();
                }
            }
            
            // Unregister TimyUsb event handlers
            timyUsb.DeviceConnected -= new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceConnected);
            timyUsb.DeviceDisconnected -= new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceDisconnected);
            timyUsb.LineReceived -= new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_LineReceived);
            timyUsb.BytesReceived -= timyUsb_BytesReceived;
            timyUsb.RawReceived -= new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_RawReceived);
            timyUsb.PnPDeviceAttached -= new EventHandler(timyUsb_PnPDeviceAttached);
            timyUsb.PnPDeviceDetached -= new EventHandler(timyUsb_PnPDeviceDetached);
            timyUsb.HeartbeatReceived -= new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Send(txtCommand.Text);
        }

        private void txtCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                Send(txtCommand.Text);
        }

        private void Send(string command)
        {
            timyUsb.Send(command + "\r");

        }

        private void button2_Click(object sender, EventArgs e)
        {
            timyUsb.Start();
            btnStart.Enabled = false;
            btnStop.Enabled = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            timyUsb.Stop();
            btnStart.Enabled = true;
            btnStop.Enabled = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SendBytes(txtBytes.Text);
        }

        private void txtBytes_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                SendBytes(txtCommand.Text);
        }

        private void SendBytes(string p)
        {
            var bytes = new List<Byte>();

            foreach (var str in txtBytes.Text.Split(new String[] { ",", " ", "\t", "-" }, StringSplitOptions.RemoveEmptyEntries))
            {
                byte b = 0;
                if (byte.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out b))
                    bytes.Add(b);
            }

            timyUsb.Send(bytes.ToArray());
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            var str = listBox1.SelectedItem;
            if (str != null)
                Clipboard.SetText(str.ToString());
        }

   
    }
}
