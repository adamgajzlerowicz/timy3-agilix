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
        private string activeStartTimeString; // Stores the actual start time sent to WebSocket clients
        private bool isRunning = false; // Tracks if timing is currently active
        private System.Windows.Forms.Timer runningStatusTimer; // Timer for sending running status updates

        public Form1()
        {
            InitializeComponent();

            // Register the click event handler for the listbox
            listBox1.Click += new EventHandler(listBox1_Click);

            // Initialize the running status timer
            runningStatusTimer = new System.Windows.Forms.Timer();
            runningStatusTimer.Interval = 1000; // 1 second
            runningStatusTimer.Tick += RunningStatusTimer_Tick;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            AddLogLine("Form loading...");
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

                AddLogLine("Form loaded successfully");
            }
            catch (Exception ex)
            {
                AddLogLine("Error in Form_Load: " + ex.Message);
                AddLogLine("Stack trace: " + ex.StackTrace);
            }
        }

        private void StartWebSocketServer()
        {
            try
            {
                webSocketCancellation = new CancellationTokenSource();
                httpListener = new HttpListener();

                // Only listen on localhost to reduce startup time and security issues
                httpListener.Prefixes.Add("http://localhost:8087/");

                httpListener.Start();

                this.BeginInvoke(new Action(() => {
                    AddLogLine("WebSocket server started at ws://localhost:8087/timy3");
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
                            string responseString = "<html><body><h1>Timy3 WebSocket Server</h1><p>This is a WebSocket server endpoint. Connect to ws://localhost:8087/timy3</p></body></html>";
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

                // If timing is active, send start signal to new client
                if (!string.IsNullOrEmpty(activeStartTimeString))
                {
                    string startMessage = $"{{\"event\":\"start\",\"time\":\"{activeStartTimeString}\"}}";
                    _ = Task.Run(async () => {
                        try
                        {
                            var messageBytes = Encoding.UTF8.GetBytes(startMessage);
                            var messageSegment = new ArraySegment<byte>(messageBytes);
                            await webSocket.SendAsync(messageSegment, WebSocketMessageType.Text, true, CancellationToken.None);

                            this.BeginInvoke(new Action(() => {
                                AddLogLine($"Sent active start signal to new client: {activeStartTimeString}");
                            }));
                        }
                        catch (Exception ex)
                        {
                            this.BeginInvoke(new Action(() => {
                                AddLogLine($"Failed to send start signal to new client: {ex.Message}");
                            }));
                        }
                    });
                }

                // Send current running status to new client
                string runningMessage = $"{{\"event\":\"running\",\"value\":{(isRunning ? "true" : "false")}}}";
                _ = Task.Run(async () => {
                    try
                    {
                        var messageBytes = Encoding.UTF8.GetBytes(runningMessage);
                        var messageSegment = new ArraySegment<byte>(messageBytes);
                        await webSocket.SendAsync(messageSegment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() => {
                            AddLogLine($"Failed to send running status to new client: {ex.Message}");
                        }));
                    }
                });

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
            if (chkRaw.Checked)
                AddLogLine("Device " + e.Device.Id + " Raw: " + e.Data);
        }

        void timyUsb_LineReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            // Log the raw signal with a special prefix to make it easier to identify
            AddLogLine("RAW SIGNAL: " + e.Data);

            // Also log the standard format
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
                AddLogLine($"PARSING: {data}");

                if (string.IsNullOrEmpty(data))
                {
                    AddLogLine("EMPTY DATA - SKIPPING");
                    return;
                }

                // Simple string-based detection without arrays
                string cleanData = data.Replace(",", " ").Trim();
                AddLogLine($"CLEAN DATA: {cleanData}");

                // Check for start signal (contains "c0")
                if (cleanData.ToLower().Contains(" c0 "))
                {
                    AddLogLine("FOUND C0 - START SIGNAL");

                    // Extract time value after "c0" for logging purposes
                    string[] parts = cleanData.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string deviceTimeValue = null;

                    // Find c0 and get the next part
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].ToLower() == "c0" && i + 1 < parts.Length)
                        {
                            deviceTimeValue = parts[i + 1];
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(deviceTimeValue))
                    {
                        AddLogLine($"DEVICE START TIME: {deviceTimeValue}");

                        lastStartTimeString = deviceTimeValue;
                        startTime = DateTime.Now;
                        isRunning = true; // Set running state to true

                        // Use PC real time instead of device time for start signal
                        string pcTimeString = startTime.Value.ToString("HH:mm:ss.fff");
                        activeStartTimeString = pcTimeString; // Store the active start time
                        string startMessage = $"{{\"event\":\"start\",\"time\":\"{pcTimeString}\"}}";
                        Task.Run(() => BroadcastToWebSocketClientsAsync(startMessage));

                        // Broadcast running status
                        string runningMessage = $"{{\"event\":\"running\",\"value\":true}}";
                        Task.Run(() => BroadcastToWebSocketClientsAsync(runningMessage));

                        // Start the timer to send running status every second
                        runningStatusTimer.Start();

                        AddLogLine($"START SIGNAL SENT TO WEBSOCKET WITH PC TIME: {pcTimeString} (Device time was: {deviceTimeValue})");
                    }
                    else
                    {
                        AddLogLine("NO TIME VALUE FOUND FOR START");
                    }
                }
                // Check for finish signal (contains "c1")
                else if (cleanData.ToLower().Contains("c1"))
                {
                    AddLogLine("FOUND C1 - FINISH SIGNAL");

                    // Extract time value - support both old and new formats
                    string[] parts = cleanData.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string timeValue = null;

                    // First try old format: look for c1 and get the next part
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].ToLower() == "c1" && i + 1 < parts.Length)
                        {
                            timeValue = parts[i + 1];
                            break;
                        }
                    }

                    // If old format didn't work, try new format: look for time pattern anywhere
                    if (string.IsNullOrEmpty(timeValue))
                    {
                        foreach (string part in parts)
                        {
                            if (part.Contains(":") && part.Contains("."))
                            {
                                timeValue = part;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(timeValue))
                    {
                        AddLogLine($"FINISH TIME: {timeValue}");

                        // Send to WebSocket
                        string finishMessage = $"{{\"event\":\"finish\",\"time\":\"{timeValue}\"}}";
                        Task.Run(() => BroadcastToWebSocketClientsAsync(finishMessage));

                        AddLogLine("FINISH SIGNAL SENT TO WEBSOCKET");

                        // Reset start time and clear active start time
                        startTime = null;
                        activeStartTimeString = null;
                        isRunning = false; // Set running state to false

                        // Stop the running status timer
                        runningStatusTimer.Stop();

                        // Broadcast running status
                        string runningMessage = $"{{\"event\":\"running\",\"value\":false}}";
                        Task.Run(() => BroadcastToWebSocketClientsAsync(runningMessage));
                    }
                    else
                    {
                        AddLogLine("NO TIME VALUE FOUND FOR FINISH");
                    }
                }
                else
                {
                    AddLogLine("NO C0 OR C1 FOUND - IGNORING SIGNAL");
                }
            }
            catch (Exception ex)
            {
                AddLogLine($"ERROR IN PROCESS TIMING: {ex.Message}");
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

            // Stop and dispose the timer
            if (runningStatusTimer != null)
            {
                runningStatusTimer.Stop();
                runningStatusTimer.Dispose();
            }
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

        // Add a single-click handler to copy to clipboard
        private void listBox1_Click(object sender, EventArgs e)
        {
            var str = listBox1.SelectedItem;
            if (str != null)
            {
                Clipboard.SetText(str.ToString());
            }
        }

        // Timer tick event handler for sending running status updates
        private void RunningStatusTimer_Tick(object sender, EventArgs e)
        {
            if (isRunning)
            {
                string runningMessage = $"{{\"event\":\"running\",\"value\":true}}";
                Task.Run(() => BroadcastToWebSocketClientsAsync(runningMessage));
            }
        }

        // Clear log button click event handler
        private void btnClearLog_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }


    }
}
