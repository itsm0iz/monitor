using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StudyMonitor
{
    // The window, the tray icon and the hotkey. The decisions live in Engine.
    class MainForm : Form
    {
        public static readonly uint WM_SHOWME = Native.RegisterWindowMessage("StudyMonitor.ShowWindow.v1");
        const int HotkeyId = 1;

        static readonly Color Green = Color.FromArgb(38, 140, 80);
        static readonly Color Red = Color.FromArgb(196, 64, 52);
        static readonly Color Gray = Color.FromArgb(108, 114, 122);
        static readonly Color Ink = Color.FromArgb(30, 32, 36);

        readonly string dataDir;
        readonly float dpi;
        readonly Speech speech;
        readonly Engine engine;
        readonly NotifyIcon tray;
        readonly MenuItem menuToggle;
        readonly Timer timer;
        readonly Icon iconIdle, iconStudy, iconDone;
        Settings settings;
        bool allowShow, quitting, hotkeyOk, settingsOpen;
        string lastTip = "", listKey = "";
        int ticks;

        Label lblStatus, lblTimer, lblCaption, lblHotkey, lblToday, lblStats, lblExam, lblSaid, lblProblem;
        Button btnToggle;
        ProgressBar bar;
        BarChart chart;
        ListBox list;

        string SettingsPath { get { return Path.Combine(dataDir, "settings.ini"); } }
        string MessagesPath { get { return Path.Combine(dataDir, "messages.txt"); } }
        string CsvPath { get { return Path.Combine(dataDir, "sessions.csv"); } }

        public MainForm(string dataDir, bool startHidden)
        {
            this.dataDir = dataDir;
            allowShow = !startHidden;
            dpi = ScreenScale();
            settings = Settings.Load(SettingsPath);
            if (!File.Exists(SettingsPath)) SaveSettings();

            iconIdle = MakeIcon(Gray);
            iconStudy = MakeIcon(Green);
            iconDone = MakeIcon(Color.FromArgb(214, 160, 30));

            Text = "Study Monitor";
            Icon = iconStudy;
            Font = new Font("Segoe UI", 9f);
            BackColor = SystemColors.Window;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Px(16), Px(10), Px(16), Px(14));
            BuildUi();

            speech = new Speech();
            ApplyVoice();

            engine = new Engine(settings, dataDir, DateTime.Now, new Random());
            engine.Say = delegate(string text, bool interrupt) { speech.Speak(text, interrupt); };

            menuToggle = new MenuItem("Start studying", delegate { Toggle(); });
            tray = new NotifyIcon();
            tray.Icon = iconIdle;
            tray.Text = "Study Monitor";
            tray.ContextMenu = new ContextMenu(new MenuItem[]
            {
                menuToggle,
                new MenuItem("Open", delegate { ShowMain(); }),
                new MenuItem("Settings...", delegate { OpenSettings(); }),
                new MenuItem("-"),
                new MenuItem("Quit", delegate { Quit(); })
            });
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowMain(); };
            tray.Visible = true;

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            Autostart.Apply(settings.StartWithWindows);

            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += delegate { OnTick(); };
            timer.Start();
            OnTick();
        }

        // ---------------------------------------------------------------- layout

        void BuildUi()
        {
            int w = Px(330);
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 1;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Dock = DockStyle.Fill;

            lblStatus = MakeLabel(9.5f, FontStyle.Bold, Gray, w);
            lblTimer = MakeLabel(30f, FontStyle.Regular, Ink, w);
            lblTimer.Margin = new Padding(0);
            lblCaption = MakeLabel(8.5f, FontStyle.Regular, Gray, w);

            btnToggle = new Button();
            btnToggle.Size = new Size(w, Px(46));
            btnToggle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            btnToggle.Margin = new Padding(0, Px(10), 0, Px(2));
            btnToggle.Click += delegate { Toggle(); };
            lblHotkey = MakeLabel(8f, FontStyle.Regular, Gray, w);

            lblToday = MakeLabel(10f, FontStyle.Bold, Ink, w);
            lblToday.Margin = new Padding(0, Px(12), 0, Px(4));
            bar = new ProgressBar();
            bar.Size = new Size(w, Px(12));
            bar.Maximum = 1000;
            bar.Margin = new Padding(0, 0, 0, Px(6));
            lblStats = MakeLabel(9f, FontStyle.Regular, Ink, w);
            lblExam = MakeLabel(9f, FontStyle.Bold, Red, w);

            chart = new BarChart();
            chart.Size = new Size(w, Px(96));
            chart.Font = new Font("Segoe UI", 8f);
            chart.ForeColor = Gray;
            chart.BackColor = SystemColors.Window;
            chart.Margin = new Padding(0, 0, 0, Px(4));

            list = new ListBox();
            list.Size = new Size(w, Px(80));
            list.IntegralHeight = false;
            list.SelectionMode = SelectionMode.None;
            list.BorderStyle = BorderStyle.FixedSingle;

            lblSaid = MakeLabel(8.5f, FontStyle.Italic, Gray, w);
            lblSaid.Margin = new Padding(0, Px(8), 0, 0);
            lblProblem = MakeLabel(8.5f, FontStyle.Regular, Red, w);
            lblProblem.Visible = false;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0, Px(10), 0, 0);
            buttons.Controls.Add(MakeButton("Settings", delegate { OpenSettings(); }));
            buttons.Controls.Add(MakeButton("History", delegate { OpenHistory(); }));
            buttons.Controls.Add(MakeButton("Data folder", delegate { OpenDataFolder(); }));

            Control[] rows =
            {
                lblStatus, lblTimer, lblCaption, btnToggle, lblHotkey,
                lblToday, bar, lblStats, lblExam,
                Header("Last 7 days"), chart,
                Header("Today's sessions"), list,
                lblSaid, lblProblem, buttons
            };
            foreach (Control c in rows) t.Controls.Add(c);
            Controls.Add(t);
        }

        Label MakeLabel(float size, FontStyle style, Color color, int maxWidth)
        {
            Label l = new Label();
            l.AutoSize = true;
            l.Font = new Font("Segoe UI", size, style);
            l.ForeColor = color;
            l.MaximumSize = new Size(maxWidth, 0);
            l.Margin = new Padding(0, Px(2), 0, Px(2));
            return l;
        }

        Label Header(string text)
        {
            Label l = MakeLabel(8.5f, FontStyle.Bold, Gray, Px(330));
            l.Text = text.ToUpper(CultureInfo.CurrentCulture);
            l.Margin = new Padding(0, Px(10), 0, Px(4));
            return l;
        }

        Button MakeButton(string text, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.Padding = new Padding(Px(6), Px(1), Px(6), Px(1));
            b.Margin = new Padding(0, 0, Px(6), 0);
            b.Click += onClick;
            return b;
        }

        static float ScreenScale()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
            }
            catch
            {
                return 1f;
            }
        }

        int Px(int px)
        {
            return (int)Math.Round(px * dpi);
        }

        // A round clock-face icon, drawn at runtime: green = studying, gray = not, gold = goal done.
        static Icon MakeIcon(Color color)
        {
            using (Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (SolidBrush b = new SolidBrush(color)) g.FillEllipse(b, 1, 1, 30, 30);
                    using (Pen p = new Pen(Color.White, 3.4f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        g.DrawLine(p, 16, 16, 16, 7);
                        g.DrawLine(p, 16, 16, 22, 20);
                    }
                }
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(h)) return (Icon)temp.Clone();
                }
                finally
                {
                    Native.DestroyIcon(h);
                }
            }
        }

        // ---------------------------------------------------------------- refresh

        void OnTick()
        {
            DateTime now = DateTime.Now;
            try
            {
                engine.Tick(now, Native.IdleSeconds());
                UpdateTray(now);
                if (Visible && WindowState != FormWindowState.Minimized) RefreshUi(now);
                // While hidden in the tray, give startup memory back to Windows, then every 30 minutes.
                ticks++;
                if (!Visible && (ticks == 5 || ticks % 1800 == 0)) Native.TrimMemory();
            }
            catch (Exception ex)
            {
                Program.LogError(ex);
            }
        }

        void UpdateTray(DateTime now)
        {
            double done = engine.DoneToday(now);
            int goal = settings.GoalMinutes;
            Icon icon = engine.Studying ? iconStudy : goal > 0 && done >= goal ? iconDone : iconIdle;
            if (tray.Icon != icon) tray.Icon = icon;

            string tip = engine.Studying ? "Studying " + Fmt.HoursMinutes(now - engine.SessionStart) : "Not studying";
            tip += "  |  Today " + Fmt.Short(done) + (goal > 0 ? " of " + Fmt.Short(goal) : "");
            if (tip.Length > 63) tip = tip.Substring(0, 63);
            if (tip != lastTip)
            {
                tray.Text = tip;
                lastTip = tip;
            }
            menuToggle.Text = engine.Studying ? "Stop studying" : "Start studying";
        }

        void RefreshUi(DateTime now)
        {
            double done = engine.DoneToday(now);
            int goal = settings.GoalMinutes;

            if (engine.Studying)
            {
                SetLabel(lblStatus, "STUDYING", Green);
                SetLabel(lblTimer, Fmt.Clock(now - engine.SessionStart), Ink);
                SetLabel(lblCaption, "this session", Gray);
                btnToggle.Text = "Stop";
            }
            else
            {
                bool away = engine.Away;
                SetLabel(lblStatus, away ? "AWAY" : "NOT STUDYING", away ? Gray : Red);
                SetLabel(lblTimer, Fmt.Clock(TimeSpan.FromSeconds(engine.OffTaskSec)), engine.OffTaskSec >= 3600 ? Red : Gray);
                SetLabel(lblCaption, away ? "on the laptop since you last studied (paused while you're away)" : "on the laptop since you last studied", Gray);
                btnToggle.Text = "Start studying";
            }

            if (string.IsNullOrEmpty(settings.Hotkey)) lblHotkey.Text = "";
            else if (hotkeyOk) lblHotkey.Text = "or press " + settings.Hotkey + " anywhere";
            else lblHotkey.Text = settings.Hotkey + " is taken by another app. Pick a different hotkey in Settings.";

            if (goal > 0)
            {
                lblToday.Text = "Today: " + Fmt.Short(done) + " of " + Fmt.Short(goal) + "   (" + (int)Math.Floor(done / goal * 100) + "%)";
                bar.Value = (int)Math.Max(0, Math.Min(1000, done / goal * 1000));
            }
            else
            {
                lblToday.Text = "Today: " + Fmt.Short(done);
                bar.Value = 0;
            }

            double? avg = engine.Average(now);
            lblStats.Text = "Streak: " + Fmt.Plural(engine.Streak(now), "day") + "        7-day average: " + (avg.HasValue ? Fmt.Short(avg.Value) : "-");

            int exam = engine.ExamDays(now);
            lblExam.Visible = exam >= 0;
            if (exam >= 0)
            {
                string name = settings.ExamName.Length > 0 ? char.ToUpper(settings.ExamName[0]) + settings.ExamName.Substring(1) : "Exam";
                lblExam.Text = name + (exam == 0 ? " is today" : exam == 1 ? " is tomorrow" : " in " + exam + " days");
            }

            DateTime today = engine.History.DayOf(now);
            string[] labels = new string[7];
            for (int i = 0; i < 7; i++) labels[i] = i == 6 ? "Today" : today.AddDays(i - 6).ToString("ddd", CultureInfo.CurrentCulture);
            chart.SetData(engine.Last7(now), labels, goal);

            RefreshList(now);

            lblSaid.Text = string.IsNullOrEmpty(engine.LastSaid) ? "" : "Last said: “" + engine.LastSaid + "”";
            lblProblem.Visible = !string.IsNullOrEmpty(engine.Problem);
            lblProblem.Text = engine.Problem ?? "";
        }

        static void SetLabel(Label l, string text, Color color)
        {
            l.Text = text;
            if (l.ForeColor != color) l.ForeColor = color;
        }

        void RefreshList(DateTime now)
        {
            List<Session> sessions = engine.TodaySessions(now);
            string key = sessions.Count + "|" + (engine.Studying ? Ini.Stamp(engine.SessionStart) + "|" + (int)(now - engine.SessionStart).TotalMinutes : "-");
            if (key == listKey) return;
            listKey = key;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (Session s in sessions)
            {
                list.Items.Add(s.Start.ToShortTimeString() + "  –  " + s.End.ToShortTimeString() + "      " + Fmt.Short(s.Minutes));
            }
            if (engine.Studying)
            {
                list.Items.Add(engine.SessionStart.ToShortTimeString() + "  –  now          " + Fmt.Short((now - engine.SessionStart).TotalMinutes));
            }
            if (list.Items.Count == 0) list.Items.Add("Nothing yet today.");
            list.EndUpdate();
            list.TopIndex = Math.Max(0, list.Items.Count - 1);
        }

        // ---------------------------------------------------------------- actions

        void Toggle()
        {
            DateTime now = DateTime.Now;
            engine.Toggle(now);
            UpdateTray(now);
            if (Visible) RefreshUi(now);
        }

        void ShowMain()
        {
            allowShow = true;
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            RefreshUi(DateTime.Now);
            Activate();
            Native.SetForegroundWindow(Handle);
        }

        void HideToTray()
        {
            Hide();
            if (!engine.TrayHintShown)
            {
                engine.TrayHintShown = true;
                tray.ShowBalloonTip(5000, "Study Monitor is still running",
                    "It keeps tracking from here. Right-click this icon to quit.", ToolTipIcon.Info);
            }
            Native.TrimMemory();
        }

        void Quit()
        {
            quitting = true;
            Close();
        }

        void OpenSettings()
        {
            if (settingsOpen) return;
            settingsOpen = true;
            try
            {
                ShowMain();
                using (SettingsForm f = new SettingsForm(settings.Clone(), speech, MessagesPath, dpi))
                {
                    if (f.ShowDialog(this) == DialogResult.OK)
                    {
                        settings = f.Result;
                        SaveSettings();
                        engine.UpdateSettings(settings, DateTime.Now);
                        RegisterHotkey();
                        Autostart.Apply(settings.StartWithWindows);
                    }
                }
                ApplyVoice();   // the Test button may have tried another voice
                RefreshUi(DateTime.Now);
            }
            finally
            {
                settingsOpen = false;
            }
        }

        void OpenHistory()
        {
            if (!File.Exists(CsvPath))
            {
                MessageBox.Show(this, "No sessions yet. Start one first.", "Study Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                // Open a copy, so Excel can't lock the real file while the app needs to write to it.
                string copy = Path.Combine(Path.GetTempPath(), "Study sessions " + DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture) + ".csv");
                File.Copy(CsvPath, copy, true);
                Process.Start(copy);
            }
            catch (Exception ex)
            {
                Program.LogError(ex);
                try { Process.Start("notepad.exe", "\"" + CsvPath + "\""); }
                catch { }
            }
        }

        void OpenDataFolder()
        {
            try { Process.Start("explorer.exe", "\"" + dataDir + "\""); }
            catch (Exception ex) { Program.LogError(ex); }
        }

        void ApplyVoice()
        {
            speech.Use(settings.Voice);
            speech.Configure(settings.Volume, settings.Rate);
        }

        void SaveSettings()
        {
            try { settings.Save(SettingsPath); }
            catch (Exception ex) { Program.LogError(ex); }
        }

        void RegisterHotkey()
        {
            if (!IsHandleCreated) return;
            Native.UnregisterHotKey(Handle, HotkeyId);
            hotkeyOk = false;
            uint modifiers, key;
            if (Hotkeys.TryParse(settings.Hotkey, out modifiers, out key))
            {
                hotkeyOk = Native.RegisterHotKey(Handle, HotkeyId, modifiers | Native.MOD_NOREPEAT, key);
            }
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend) engine.Suspend(DateTime.Now);
            else if (e.Mode == PowerModes.Resume) OnTick();
        }

        // ---------------------------------------------------------------- window plumbing

        // Started with /tray: create the window (for the hotkey) but keep it hidden.
        protected override void SetVisibleCore(bool value)
        {
            if (!allowShow)
            {
                value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkey();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RefreshUi(DateTime.Now);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && Visible) HideToTray();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt64() == HotkeyId)
            {
                Toggle();
                return;
            }
            if (WM_SHOWME != 0 && m.Msg == unchecked((int)WM_SHOWME))
            {
                ShowMain();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!quitting && e.CloseReason == CloseReason.UserClosing)
            {
                // The X button only hides the window; it keeps tracking from the tray.
                e.Cancel = true;
                HideToTray();
                return;
            }
            engine.Shutdown(DateTime.Now);
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            if (IsHandleCreated) Native.UnregisterHotKey(Handle, HotkeyId);
            tray.Visible = false;
            tray.Dispose();
            base.OnFormClosed(e);
        }
    }
}
