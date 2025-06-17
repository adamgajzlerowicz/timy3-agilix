using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AlgeTimyUsb.SampleApplication
{
    public partial class Form1 : Form
    {

        Alge.TimyUsb timyUsb;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timyUsb = new Alge.TimyUsb(this);
            timyUsb.Start();
            btnStart.Enabled = false;
            btnStop.Enabled = true;



            timyUsb.DeviceConnected += new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceConnected);
            timyUsb.DeviceDisconnected += new EventHandler<Alge.DeviceChangedEventArgs>(timyUsb_DeviceDisconnected);
            timyUsb.LineReceived += new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_LineReceived);
            timyUsb.BytesReceived += timyUsb_BytesReceived;
            timyUsb.RawReceived += new EventHandler<Alge.DataReceivedEventArgs>(timyUsb_RawReceived);
            timyUsb.PnPDeviceAttached += new EventHandler(timyUsb_PnPDeviceAttached);
            timyUsb.PnPDeviceDetached += new EventHandler(timyUsb_PnPDeviceDetached);
            timyUsb.HeartbeatReceived += new EventHandler<Alge.HeartbeatReceivedEventArgs>(timyUsb_HeartbeatReceived);

            AddLogLine("Process is " + (IntPtr.Size == 8 ? "x64" : "x86"  ));
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
                ProcessProgramResponse(e.Data);
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
