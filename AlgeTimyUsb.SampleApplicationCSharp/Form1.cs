using System;
using System.Collections.Concurrent;
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
        private Alge.TimyUsb timyUsb;
        private HttpListener httpListener;
        private CancellationTokenSource webSocketCancellation;
        private readonly ConcurrentDictionary<Guid, ClientConnection> connectedClients = new ConcurrentDictionary<Guid, ClientConnection>();
        private DateTime? startTime;
        private string lastStartTimeString;
        private string activeStartTimeString;
        private bool isRunning = false;
        private System.Windows.Forms.Timer runningStatusTimer;
        private System.Windows.Forms.Timer healthCheckTimer;
        private readonly SemaphoreSlim timy3SendSemaphore = new SemaphoreSlim(1, 1);

        // Client connection wrapper for better state management
        private class ClientConnection
        {
            public Guid Id { get; }
            public WebSocket Socket { get; }
            public CancellationTokenSource CancellationTokenSource { get; }
            public DateTime LastActivity { get; set; }
            private readonly SemaphoreSlim sendSemaphore = new SemaphoreSlim(1, 1);
            private readonly ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
            private bool isProcessing = false;

            public ClientConnection(WebSocket socket)
            {
                Id = Guid.NewGuid();
                Socket = socket;
                CancellationTokenSource = new CancellationTokenSource();
                LastActivity = DateTime.Now;
            }

            public async Task SendAsync(string message)
            {
                if (Socket.State != WebSocketState.Open)
                    return;

                messageQueue.Enqueue(message);
                await ProcessQueueAsync();
            }

            private async Task ProcessQueueAsync()
            {
                if (isProcessing)
                    return;

                await sendSemaphore.WaitAsync();
                try
                {
                    if (isProcessing)
                        return;

                    isProcessing = true;

                    while (messageQueue.TryDequeue(out string message))
                    {
                        if (Socket.State != WebSocketState.Open)
                            break;

                        try
                        {
                            var messageBytes = Encoding.UTF8.GetBytes(message);
                            var messageSegment = new ArraySegment<byte>(messageBytes);

                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                            {
                                await Socket.SendAsync(messageSegment, WebSocketMessageType.Text, true, cts.Token);
                                LastActivity = DateTime.Now;
                            }
                        }
                        catch (Exception)
                        {
                            // Failed to send, stop processing this client
                            break;
                        }
                    }
                }
                finally
                {
                    isProcessing = false;
                    sendSemaphore.Release();
                }
            }

            public void Dispose()
            {
                try
                {
                    CancellationTokenSource?.Cancel();
                    CancellationTokenSource?.Dispose();
                    sendSemaphore?.Dispose();

                    if (Socket?.State == WebSocketState.Open)
                    {
                        Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait(1000);
                    }
                    Socket?.Dispose();
                }
                catch { }
            }
        }

        public Form1()
        {
            InitializeComponent();

            listBox1.Click += new EventHandler(listBox1_Click);

            // Initialize timers
            runningStatusTimer = new System.Windows.Forms.Timer();
            runningStatusTimer.Interval = 1000;
            runningStatusTimer.Tick += RunningStatusTimer_Tick;

            healthCheckTimer = new System.Windows.Forms.Timer();
            healthCheckTimer.Interval = 10000; // 10 seconds
            healthCheckTimer.Tick += HealthCheckTimer_Tick;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            AddLogLine("Form loading...");
            AddLogLine("Process is " + (IntPtr.Size == 8 ? "x64" : "x86"));

            try
            {
                // Start WebSocket server
                Task.Run(() => StartWebSocketServerAsync());

                // Initialize and start TimyUsb with delay to ensure proper initialization
                Task.Run(async () =>
                {
                    await Task.Delay(500);
                    InitializeTimyUsb();
                });

                healthCheckTimer.Start();
                AddLogLine("Form loaded successfully");
            }
            catch (Exception ex)
            {
                AddLogLine("Error in Form_Load: " + ex.Message);
            }
        }

        private void InitializeTimyUsb()
        {
            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    timyUsb = new Alge.TimyUsb(this);

                    timyUsb.DeviceConnected += timyUsb_DeviceConnected;
                    timyUsb.DeviceDisconnected += timyUsb_DeviceDisconnected;
                    timyUsb.LineReceived += timyUsb_LineReceived;
                    timyUsb.BytesReceived += timyUsb_BytesReceived;
                    timyUsb.RawReceived += timyUsb_RawReceived;
                    timyUsb.PnPDeviceAttached += timyUsb_PnPDeviceAttached;
                    timyUsb.PnPDeviceDetached += timyUsb_PnPDeviceDetached;

                    timyUsb.Start();
                    btnStart.Enabled = false;
                    btnStop.Enabled = true;

                    // Check for connected devices
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        this.BeginInvoke(new Action(() =>
                        {
                            int connectedCount = timyUsb.ConnectedDevicesCount;
                            if (connectedCount > 0)
                            {
                                AddLogLine($"Found {connectedCount} device(s) connected");
                                Send("PROG");
                            }
                            else
                            {
                                AddLogLine("No devices connected at startup");
                            }
                        }));
                    });
                }));
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() =>
                {
                    AddLogLine("Error initializing TimyUsb: " + ex.Message);
                }));
            }
        }

        private async Task StartWebSocketServerAsync()
        {
            try
            {
                webSocketCancellation = new CancellationTokenSource();
                httpListener = new HttpListener();
                httpListener.Prefixes.Add("http://localhost:8087/");
                httpListener.Start();

                this.BeginInvoke(new Action(() =>
                {
                    AddLogLine("WebSocket server started at ws://localhost:8087/timy3");
                }));

                await AcceptWebSocketClientsAsync(webSocketCancellation.Token);
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() =>
                {
                    AddLogLine("Error starting WebSocket server: " + ex.Message);
                }));
            }
        }

        private async Task AcceptWebSocketClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var contextTask = httpListener.GetContextAsync();
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        cts.CancelAfter(TimeSpan.FromSeconds(30));
                        var context = await contextTask;

                        if (context.Request.IsWebSocketRequest && context.Request.Url.AbsolutePath == "/timy3")
                        {
                            _ = Task.Run(() => ProcessWebSocketRequestAsync(context), cancellationToken);
                        }
                        else
                        {
                            context.Response.StatusCode = 200;
                            context.Response.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
        }

        private async Task ProcessWebSocketRequestAsync(HttpListenerContext context)
        {
            ClientConnection client = null;
            try
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                var webSocket = webSocketContext.WebSocket;

                client = new ClientConnection(webSocket);

                if (!connectedClients.TryAdd(client.Id, client))
                {
                    client.Dispose();
                    return;
                }

                this.BeginInvoke(new Action(() =>
                {
                    AddLogLine($"WebSocket client connected (ID: {client.Id})");
                }));

                // Send initial state to new client
                await SendInitialStateToClient(client);

                // Handle client messages
                await HandleWebSocketClientAsync(client);
            }
            catch (Exception ex)
            {
                this.BeginInvoke(new Action(() =>
                {
                    AddLogLine($"Error processing WebSocket: {ex.Message}");
                }));
            }
            finally
            {
                if (client != null)
                {
                    connectedClients.TryRemove(client.Id, out _);
                    client.Dispose();

                    this.BeginInvoke(new Action(() =>
                    {
                        AddLogLine($"WebSocket client disconnected (ID: {client.Id})");
                    }));
                }
            }
        }

        private async Task SendInitialStateToClient(ClientConnection client)
        {
            try
            {
                if (!string.IsNullOrEmpty(activeStartTimeString))
                {
                    await client.SendAsync($"{{\"event\":\"start\",\"time\":\"{activeStartTimeString}\"}}");
                }

                await client.SendAsync($"{{\"event\":\"running\",\"value\":{(isRunning ? "true" : "false")}}}");
            }
            catch (Exception ex)
            {
                AddLogLine($"Error sending initial state: {ex.Message}");
            }
        }

        private async Task HandleWebSocketClientAsync(ClientConnection client)
        {
            var buffer = new byte[1024];

            while (!client.CancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(client.CancellationTokenSource.Token))
                    {
                        cts.CancelAfter(TimeSpan.FromSeconds(30));

                        var result = await client.Socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        client.LastActivity = DateTime.Now;
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        private async Task BroadcastToWebSocketClientsAsync(string message)
        {
            var tasks = new List<Task>();
            var clientsToRemove = new List<Guid>();

            foreach (var kvp in connectedClients)
            {
                var client = kvp.Value;

                if (client.Socket.State != WebSocketState.Open)
                {
                    clientsToRemove.Add(kvp.Key);
                    continue;
                }

                tasks.Add(client.SendAsync(message));
            }

            // Remove disconnected clients
            foreach (var id in clientsToRemove)
            {
                if (connectedClients.TryRemove(id, out var client))
                {
                    client.Dispose();
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        private void HealthCheckTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var staleClients = connectedClients
                .Where(kvp => (now - kvp.Value.LastActivity).TotalMinutes > 5 ||
                             kvp.Value.Socket.State != WebSocketState.Open)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in staleClients)
            {
                if (connectedClients.TryRemove(id, out var client))
                {
                    client.Dispose();
                    AddLogLine($"Removed stale client: {id}");
                }
            }

            // Check Timy3 connection
            if (timyUsb != null && timyUsb.ConnectedDevicesCount == 0)
            {
                AddLogLine("Warning: No Timy3 devices connected");
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

            // Auto-reconnect logic
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                this.BeginInvoke(new Action(() =>
                {
                    if (timyUsb.ConnectedDevicesCount > 0)
                    {
                        Send("PROG");
                    }
                }));
            });
        }

        void timyUsb_RawReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            if (chkRaw.Checked)
                AddLogLine("Device " + e.Device.Id + " Raw: " + e.Data);
        }

        void timyUsb_LineReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            AddLogLine("RAW SIGNAL: " + e.Data);
            AddLogLine("Device " + e.Device.Id + " Line: " + e.Data);

            if (e.Data.StartsWith("PROG: "))
            {
                ProcessProgramResponse(e.Data);
            }
            else
            {
                // Process timing data synchronously to maintain order
                ProcessTimingData(e.Data);
            }
        }

        private void ProcessTimingData(string data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data))
                    return;

                string cleanData = data.Replace(",", " ").Trim();
                while (cleanData.Contains("  "))
                {
                    cleanData = cleanData.Replace("  ", " ");
                }

                AddLogLine($"CLEAN DATA: {cleanData}");

                // Check for start signal (c0)
                if (cleanData.ToLower().Contains(" c0 "))
                {
                    ProcessStartSignal(cleanData);
                }
                // Check for finish signal (c1)
                else if (cleanData.ToLower().Contains(" c1 ") || cleanData.ToLower().Contains("c1"))
                {
                    ProcessFinishSignal(cleanData);
                }
            }
            catch (Exception ex)
            {
                AddLogLine($"ERROR IN PROCESS TIMING: {ex.Message}");
            }
        }

        private void ProcessStartSignal(string cleanData)
        {
            AddLogLine("FOUND C0 - START SIGNAL");

            string[] parts = cleanData.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string deviceTimeValue = null;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].ToLower() == "c0")
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
                isRunning = true;

                string pcTimeString = startTime.Value.ToString("HH:mm:ss.fff");
                activeStartTimeString = pcTimeString;

                // Broadcast start signal
                Task.Run(async () =>
                {
                    await BroadcastToWebSocketClientsAsync($"{{\"event\":\"start\",\"time\":\"{pcTimeString}\"}}");
                    await BroadcastToWebSocketClientsAsync($"{{\"event\":\"running\",\"value\":true}}");
                });

                runningStatusTimer.Start();

                AddLogLine($"START SIGNAL SENT (PC Time: {pcTimeString}, Device: {deviceTimeValue})");
            }
        }

        private void ProcessFinishSignal(string cleanData)
        {
            AddLogLine("FOUND C1 - FINISH SIGNAL");

            string[] parts = cleanData.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string timeValue = null;

            // Find time value (format HH:mm:ss.fff)
            foreach (string part in parts)
            {
                if (part.Contains(":") && part.Contains("."))
                {
                    timeValue = part;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(timeValue))
            {
                AddLogLine($"FINISH TIME: {timeValue}");

                // Broadcast finish signal
                Task.Run(async () =>
                {
                    await BroadcastToWebSocketClientsAsync($"{{\"event\":\"finish\",\"time\":\"{timeValue}\"}}");
                    await BroadcastToWebSocketClientsAsync($"{{\"event\":\"running\",\"value\":false}}");
                });

                // Reset state
                startTime = null;
                activeStartTimeString = null;
                isRunning = false;
                runningStatusTimer.Stop();

                AddLogLine("FINISH SIGNAL SENT");
            }
        }

        private void ProcessProgramResponse(string line)
        {
            String programString = line.Substring(6);
            AddLogLine("Active program: '" + programString + "'");
        }

        void timyUsb_DeviceDisconnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            AddLogLine($"Device {e.Device.Id} disconnected, {timyUsb.ConnectedDevicesCount} total connected");
        }

        void timyUsb_DeviceConnected(object sender, Alge.DeviceChangedEventArgs e)
        {
            AddLogLine($"Device {e.Device.Id} connected, {timyUsb.ConnectedDevicesCount} total connected");
        }

        void AddLogLine(String str)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AddLogLine(str)));
            }
            else
            {
                listBox1.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss.fff}] {str}");

                // Keep log size manageable
                while (listBox1.Items.Count > 1000)
                {
                    listBox1.Items.RemoveAt(listBox1.Items.Count - 1);
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop timers
            runningStatusTimer?.Stop();
            runningStatusTimer?.Dispose();

            healthCheckTimer?.Stop();
            healthCheckTimer?.Dispose();

            // Stop WebSocket server
            webSocketCancellation?.Cancel();

            // Close all clients
            foreach (var kvp in connectedClients)
            {
                kvp.Value.Dispose();
            }
            connectedClients.Clear();

            httpListener?.Stop();
            httpListener?.Close();

            // Stop TimyUsb
            if (timyUsb != null)
            {
                timyUsb.DeviceConnected -= timyUsb_DeviceConnected;
                timyUsb.DeviceDisconnected -= timyUsb_DeviceDisconnected;
                timyUsb.LineReceived -= timyUsb_LineReceived;
                timyUsb.BytesReceived -= timyUsb_BytesReceived;
                timyUsb.RawReceived -= timyUsb_RawReceived;
                timyUsb.PnPDeviceAttached -= timyUsb_PnPDeviceAttached;
                timyUsb.PnPDeviceDetached -= timyUsb_PnPDeviceDetached;

                timyUsb.Stop();
            }

            timy3SendSemaphore?.Dispose();
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

        private async void Send(string command)
        {
            try
            {
                await timy3SendSemaphore.WaitAsync();
                timyUsb?.Send(command + "\r");
            }
            finally
            {
                timy3SendSemaphore.Release();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            timyUsb?.Start();
            btnStart.Enabled = false;
            btnStop.Enabled = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            timyUsb?.Stop();
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
                SendBytes(txtBytes.Text);
        }

        private async void SendBytes(string p)
        {
            var bytes = new List<Byte>();

            foreach (var str in p.Split(new[] { ",", " ", "\t", "-" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (byte.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    bytes.Add(b);
            }

            if (bytes.Count > 0)
            {
                try
                {
                    await timy3SendSemaphore.WaitAsync();
                    timyUsb?.Send(bytes.ToArray());
                }
                finally
                {
                    timy3SendSemaphore.Release();
                }
            }
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            var str = listBox1.SelectedItem;
            if (str != null)
                Clipboard.SetText(str.ToString());
        }

        private void listBox1_Click(object sender, EventArgs e)
        {
            var str = listBox1.SelectedItem;
            if (str != null)
            {
                Clipboard.SetText(str.ToString());
            }
        }

        private void RunningStatusTimer_Tick(object sender, EventArgs e)
        {
            if (isRunning)
            {
                Task.Run(() => BroadcastToWebSocketClientsAsync($"{{\"event\":\"running\",\"value\":true}}"));
            }
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }
    }
}
