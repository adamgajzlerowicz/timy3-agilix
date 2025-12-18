using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AlgeTimyUsb.SampleApplication
{
    public partial class Form1 : Form
    {
        private Alge.TimyUsb timyUsb;
        private HttpListener httpListener;
        private CancellationTokenSource webSocketCancellation;
        private readonly List<WebSocket> connectedClients = new List<WebSocket>();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            AddLog("Starting...");

            // Start WebSocket server
            Task.Run(() => StartWebSocketServer());

            // Initialize Timy USB
            Task.Run(async () =>
            {
                await Task.Delay(500);
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        timyUsb = new Alge.TimyUsb(this);
                        timyUsb.DeviceConnected += (s, ev) => AddLog($"Device {ev.Device.Id} connected");
                        timyUsb.DeviceDisconnected += (s, ev) => AddLog($"Device {ev.Device.Id} disconnected");
                        timyUsb.LineReceived += TimyUsb_LineReceived;
                        timyUsb.Start();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"Error: {ex.Message}");
                    }
                }));
            });
        }

        private async Task StartWebSocketServer()
        {
            try
            {
                webSocketCancellation = new CancellationTokenSource();
                httpListener = new HttpListener();
                httpListener.Prefixes.Add("http://localhost:8087/");
                httpListener.Start();

                BeginInvoke(new Action(() => AddLog("WebSocket server started at ws://localhost:8087/timy3")));

                while (!webSocketCancellation.Token.IsCancellationRequested)
                {
                    try
                    {
                        var context = await httpListener.GetContextAsync();

                        if (context.Request.IsWebSocketRequest && context.Request.Url.AbsolutePath == "/timy3")
                        {
                            var wsContext = await context.AcceptWebSocketAsync(null);
                            var webSocket = wsContext.WebSocket;

                            lock (connectedClients)
                            {
                                connectedClients.Add(webSocket);
                            }

                            BeginInvoke(new Action(() => AddLog($"Client connected. Total: {connectedClients.Count}")));

                            _ = Task.Run(() => HandleClient(webSocket));
                        }
                        else
                        {
                            context.Response.StatusCode = 200;
                            context.Response.Close();
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => AddLog($"WebSocket error: {ex.Message}")));
            }
        }

        private async Task HandleClient(WebSocket webSocket)
        {
            var buffer = new byte[1024];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    // Handle ping/pong
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (message == "ping")
                        {
                            await SendToClient(webSocket, "pong");
                        }
                    }
                }
            }
            catch { }
            finally
            {
                lock (connectedClients)
                {
                    connectedClients.Remove(webSocket);
                }

                BeginInvoke(new Action(() => AddLog($"Client disconnected. Total: {connectedClients.Count}")));

                if (webSocket.State == WebSocketState.Open)
                {
                    try { await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                    catch { }
                }
                webSocket.Dispose();
            }
        }

        private async Task SendToClient(WebSocket webSocket, string message)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                try
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch { }
            }
        }

        private async Task BroadcastToClients(string message)
        {
            AddLog($"BROADCASTING TO CLIENTS: [{message}]");

            List<WebSocket> clients;
            lock (connectedClients)
            {
                clients = connectedClients.ToList();
            }

            var tasks = clients.Select(client => SendToClient(client, message));
            await Task.WhenAll(tasks);
        }

        private void TimyUsb_LineReceived(object sender, Alge.DataReceivedEventArgs e)
        {
            AddLog($"Device {e.Device.Id}: {e.Data}");
            ProcessTimingData(e.Data);
        }

        private void ProcessTimingData(string data)
        {
            try
            {
                string cleanData = data.Replace(",", " ").Trim();
                while (cleanData.Contains("  "))
                    cleanData = cleanData.Replace("  ", " ");

                // Check for finish signal (c1) only
                if (cleanData.ToLower().Contains(" c1 ") || cleanData.ToLower().Contains("c1"))
                {
                    AddLog("FINISH SIGNAL");

                    // Find time value in format HH:mm:ss.fff or HH:mm:ss:hh
                    string[] parts = cleanData.Split(' ');
                    string timeValue = parts.FirstOrDefault(p => p.Contains(":") && (p.Contains(".") || p.Count(c => c == ':') >= 3));

                    if (!string.IsNullOrEmpty(timeValue))
                    {
                        Task.Run(async () =>
                        {
                            await BroadcastToClients($"{{\"event\":\"finish\",\"time\":\"{timeValue}\"}}");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error processing: {ex.Message}");
            }
        }

        private void AddLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AddLog(message)));
                return;
            }

            listBox1.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");

            // Keep log size manageable
            while (listBox1.Items.Count > 500)
                listBox1.Items.RemoveAt(listBox1.Items.Count - 1);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            webSocketCancellation?.Cancel();

            lock (connectedClients)
            {
                foreach (var client in connectedClients)
                {
                    try { client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(1000); }
                    catch { }
                    client.Dispose();
                }
                connectedClients.Clear();
            }

            httpListener?.Stop();
            httpListener?.Close();

            if (timyUsb != null)
            {
                timyUsb.Stop();
            }
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }
    }
}
