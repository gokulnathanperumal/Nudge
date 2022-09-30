#region Imports
using CSCore.CoreAudioAPI;
using System;
using System.Drawing;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
#endregion

namespace Nudge
{
    public partial class Form1 : Form
    {
        #region Read Me
        //1.Create Windows Forms App 'Nudge' and add lable1 and notifyIcon1
        //2.Download and place following files in Nudge\Resources folder (Right click File > Properties > Build Action > Embedded resource)
        //  a.Alarm04.wav (C:\Windows\Media\Alarm04.wav)
        //  b.bell-alert-on.ico (icon-icons.com/icon/alarm-alert-bell-internet-notice-notification-security/127089)
        //  c.bell-alert-off.ico (icon-icons.com/icon/alert-attention-danger-error-internet-security-warning/127090)
        //3.Add them to Project Resources as well (Right click Project > Properties > Resources > Add Existing File)
        //4.Choose bell-alert-on.ico as Project Icon (Right click Project > Properties > Application > Icon > Browse)
        //5.Install CSCore by Florian R (1.2.1.2) in NuGet Package Manager
        //6.Create and place Nudge.exe shortcut in C:\Users\<username>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup
        #endregion

        #region Windows API
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOMOVE = 0x0002;
        private const UInt32 TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;
        #endregion

        #region Fields
        SoundPlayer nudge;
        string speaker;
        float peakValue;
        bool isNudging;
        bool isListening;
        #endregion

        #region Constructor, Events and Methods
        public Form1()
        {
            InitializeComponent();

            //WindowState Events
            this.notifyIcon1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseClick);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);

            this.label1.AutoSize = false;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Text = "Loading...";
            this.label1.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label1.Click += new System.EventHandler(StopNudging);
            this.label1.DoubleClick += new System.EventHandler(StartStopListener);

            this.BackColor = System.Drawing.Color.Black;
            this.ForeColor = System.Drawing.Color.Green;

            nudge = new SoundPlayer();
            nudge.Stream = Properties.Resources.Alarm04;

            Timer timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += TimerTick;
            timer.Start();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            MMDevice device = GetDefaultRenderDevice();
            speaker = device.FriendlyName;

            if (IsAudioPlaying(device) && isListening && !isNudging)
            {
                nudge.PlayLooping();
                isNudging = true;

                WindowNormal();
            }

            DisplayLabel();
        }

        private void StopNudging(object sender, EventArgs e)
        {
            if (isListening && isNudging)
            {
                nudge.Stop();
                isNudging = false;
            }
        }

        private void StartStopListener(object sender, EventArgs e)
        {
            if (isListening)
            {
                isListening = false;
                this.notifyIcon1.Text = "Not Listening...";
                this.notifyIcon1.Icon = Properties.Resources.bell_alert_off;

                if (isNudging)
                {
                    nudge.Stop();
                    isNudging = false;
                }
            }
            else
            {
                isListening = true;
                this.notifyIcon1.Text = "Listening...";
                this.notifyIcon1.Icon = Properties.Resources.bell_alert_on;
            }

            DisplayLabel();
        }

        private MMDevice GetDefaultRenderDevice()
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            }
        }

        private bool IsAudioPlaying(MMDevice device)
        {
            using (var meter = AudioMeterInformation.FromDevice(device))
            {
                peakValue = meter.PeakValue;
                return peakValue >= 0.00001 && peakValue <= 1;
            }
        }

        private void DisplayLabel()
        {
            string displayText;

            if (isListening)
            {
                displayText = "Listening..." + "\n\nDouble Click to Stop Listening"
                     + "\n\n" + speaker + "\n\nPeak Value: " + peakValue;

                if (isNudging)
                {
                    displayText = "Nudging..." + "\n\nSingle Click to Stop Nudging" + "\n\nDouble Click to Stop Listening"
                        + "\n\n" + speaker + "\n\nPeak Value: " + peakValue;
                }
            }
            else
            {
                displayText = "Not Listening..." + "\n\nDouble Click to Start Listening"
                     + "\n\n" + speaker + "\n\nPeak Value: " + peakValue;
            }

            this.label1.Text = displayText;
        }
        #endregion

        #region WindowState Events and Methods
        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = "Nudge";
            this.Icon = Properties.Resources.bell_alert_on;
            this.notifyIcon1.Text = "Not Listening...";
            this.notifyIcon1.Icon = Properties.Resources.bell_alert_off;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;

            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);

            WindowMinimize();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
                e.Cancel = true;

                StopNudging(this, new EventArgs());
            }
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                WindowNormal();
            }
            else
            {
                WindowMinimize();
            }
        }

        private void WindowNormal()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;

            Graphics graphics = this.CreateGraphics();
            float scalingFactorX = graphics.DpiX / 96;
            float scalingFactorY = graphics.DpiY / 96;

            Screen screen = Screen.FromPoint(this.Location);
            this.Width = (int)(250 * scalingFactorX);
            this.Height = (int)(300 * scalingFactorY);
            this.Location = new Point(screen.WorkingArea.Right - this.Width, screen.WorkingArea.Bottom - this.Height);
        }

        private void WindowMinimize()
        {
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
        }
        #endregion
    }
}