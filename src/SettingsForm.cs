using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace StudyMonitor
{
    class SettingsForm : Form
    {
        public Settings Result;

        static readonly int[] AnnounceChoices = { 0, 1, 2, 5, 10, 15, 30 };

        readonly Settings s;
        readonly Speech speech;
        readonly string messagesPath;
        readonly float dpi;

        NumericUpDown goalH, goalM, breakEvery, nagAfter, nagEvery, awayAfter, autoStop;
        ComboBox announce, tone, voice, newDay;
        CheckBox water, examOn, startWin;
        DateTimePicker dayStart, bedtime, examDate;
        TextBox examName, hotkey;
        TrackBar volume, rate;

        public SettingsForm(Settings settings, Speech speech, string messagesPath, float dpi)
        {
            s = settings;
            this.speech = speech;
            this.messagesPath = messagesPath;
            this.dpi = dpi;

            Text = "Study Monitor - Settings";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Px(12));

            FlowLayoutPanel left = Column();
            FlowLayoutPanel right = Column();
            BuildGoal(Group(left, "Daily goal"));
            BuildExam(Group(left, "Exam countdown"));
            BuildOther(Group(left, "Other"));
            BuildStudying(Group(right, "While you study"));
            BuildSlacking(Group(right, "When you're not studying"));
            BuildVoice(Group(right, "Voice"));

            Button save = new Button();
            save.Text = "Save";
            save.AutoSize = true;
            save.Click += OnSave;
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.AutoSize = true;
            cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = save;
            CancelButton = cancel;
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.Dock = DockStyle.Fill;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);

            TableLayoutPanel root = new TableLayoutPanel();
            root.ColumnCount = 2;
            root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            root.Dock = DockStyle.Fill;
            root.Controls.Add(left, 0, 0);
            root.Controls.Add(right, 1, 0);
            root.Controls.Add(buttons, 0, 1);
            root.SetColumnSpan(buttons, 2);
            Controls.Add(root);
        }

        // ---------------------------------------------------------------- groups

        void BuildGoal(TableLayoutPanel t)
        {
            goalH = Num(0, 16, s.GoalMinutes / 60, 50);
            goalM = Num(0, 59, s.GoalMinutes % 60, 50);
            goalM.Increment = 5;
            Row(t, "Goal per day", Inline(goalH, Lbl("hours"), goalM, Lbl("min")));

            dayStart = TimePicker(s.DayStart);
            bedtime = TimePicker(s.Bedtime);
            Row(t, "My study day", Inline(dayStart, Lbl("to"), bedtime));
            Wide(t, Hint("Used to work out your pace. Bedtime is when today's goal runs out."));

            newDay = new ComboBox();
            newDay.DropDownStyle = ComboBoxStyle.DropDownList;
            newDay.Width = Px(100);
            for (int h = 0; h <= 12; h++) newDay.Items.Add(Fmt.TimeOfDay(h * 60));
            newDay.SelectedIndex = Math.Max(0, Math.Min(12, s.NewDayHour));
            Row(t, "New day starts at", newDay);
            Wide(t, Hint("Late-night studying counts for the day before."));
        }

        void BuildExam(TableLayoutPanel t)
        {
            examOn = Check("Count down to an exam or deadline", s.ExamOn);
            Wide(t, examOn);
            examDate = new DateTimePicker();
            examDate.Format = DateTimePickerFormat.Long;
            examDate.Width = Px(200);
            examDate.Value = s.ExamDate < examDate.MinDate ? DateTime.Today : s.ExamDate;
            Row(t, "Date", examDate);
            examName = new TextBox();
            examName.Width = Px(140);
            examName.Text = s.ExamName;
            Row(t, "Called", examName);
            Wide(t, Hint("Said out loud, like \"your finals are in 12 days\"."));
        }

        void BuildOther(TableLayoutPanel t)
        {
            hotkey = new TextBox();
            hotkey.Width = Px(120);
            hotkey.Text = s.Hotkey;
            Row(t, "Start/stop hotkey", hotkey);
            Wide(t, Hint("Works anywhere, even in games. Like Ctrl+Alt+S. Empty = none."));
            startWin = Check("Start with Windows (runs in the tray)", s.StartWithWindows);
            Wide(t, startWin);
            Button edit = new Button();
            edit.Text = "Edit what it says...";
            edit.AutoSize = true;
            edit.Click += delegate
            {
                try { Process.Start("notepad.exe", "\"" + messagesPath + "\""); }
                catch (Exception ex) { Program.LogError(ex); }
            };
            Wide(t, edit);
        }

        void BuildStudying(TableLayoutPanel t)
        {
            announce = new ComboBox();
            announce.DropDownStyle = ComboBoxStyle.DropDownList;
            announce.Width = Px(100);
            int pick = 0;
            for (int i = 0; i < AnnounceChoices.Length; i++)
            {
                int m = AnnounceChoices[i];
                announce.Items.Add(m == 0 ? "Never" : Fmt.Plural(m, "minute"));
                if (m == s.AnnounceEvery) pick = i;
            }
            announce.SelectedIndex = pick;
            Row(t, "Say the time every", announce);

            breakEvery = Num(0, 180, s.BreakEvery, 55);
            Row(t, "Stand-up reminder", Inline(breakEvery, Lbl("min in (0 = off)")));
            water = Check("Remind me to drink water (at random)", s.Water);
            Wide(t, water);
            autoStop = Num(0, 600, s.AutoStop, 55);
            Row(t, "Forgot to stop?", Inline(autoStop, Lbl("min idle ends it (0 = never)")));
        }

        void BuildSlacking(TableLayoutPanel t)
        {
            nagAfter = Num(0, 600, s.NagAfter, 55);
            Row(t, "First nag after", Inline(nagAfter, Lbl("min on the laptop (0 = off)")));
            nagEvery = Num(5, 240, s.NagEvery, 55);
            Row(t, "Then about every", Inline(nagEvery, Lbl("min (randomized)")));
            awayAfter = Num(1, 60, s.AwayAfter, 55);
            Row(t, "I'm away after", Inline(awayAfter, Lbl("min without keyboard/mouse")));
            tone = new ComboBox();
            tone.DropDownStyle = ComboBoxStyle.DropDownList;
            tone.Width = Px(100);
            tone.Items.AddRange(new object[] { "Gentle", "Normal", "Brutal" });
            tone.SelectedIndex = (int)s.Tone;
            Row(t, "Tone", tone);
        }

        void BuildVoice(TableLayoutPanel t)
        {
            voice = new ComboBox();
            voice.DropDownStyle = ComboBoxStyle.DropDownList;
            voice.Width = Px(230);
            voice.Items.Add("Automatic (female if there is one)");
            int pick = 0;
            List<string> names = speech.VoiceNames();
            foreach (string n in names)
            {
                voice.Items.Add(n);
                if (string.Equals(n, s.Voice, StringComparison.OrdinalIgnoreCase)) pick = voice.Items.Count - 1;
            }
            voice.SelectedIndex = pick;
            Row(t, "Voice", voice);

            volume = Slider(0, 100, s.Volume, 10);
            Row(t, "Volume", volume);
            rate = Slider(-5, 5, s.Rate, 1);
            Row(t, "Speed", rate);

            Button test = new Button();
            test.Text = "Test voice";
            test.AutoSize = true;
            test.Click += delegate
            {
                speech.Use(SelectedVoice());
                speech.Configure(volume.Value, rate.Value);
                speech.Speak("Hi. This is how I'll sound. Now get back to work.", true);
            };
            Wide(t, test);
            if (!speech.Available) Wide(t, Hint("Text-to-speech isn't available on this PC."));
        }

        string SelectedVoice()
        {
            return voice.SelectedIndex <= 0 ? "" : voice.SelectedItem.ToString();
        }

        // ---------------------------------------------------------------- save

        void OnSave(object sender, EventArgs e)
        {
            string hk = hotkey.Text.Trim();
            uint mods, key;
            if (hk.Length > 0 && !Hotkeys.TryParse(hk, out mods, out key))
            {
                MessageBox.Show(this, "The hotkey should look like Ctrl+Alt+S (or leave it empty for none).", "Study Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                hotkey.Focus();
                return;
            }

            int newDayHour = Math.Max(0, newDay.SelectedIndex);
            int start = dayStart.Value.Hour * 60 + dayStart.Value.Minute;
            int bed = bedtime.Value.Hour * 60 + bedtime.Value.Minute;
            int logicalStart = (start - newDayHour * 60 + 1440) % 1440;
            int logicalBed = (bed - newDayHour * 60 + 1440) % 1440;
            if (logicalBed <= logicalStart)
            {
                MessageBox.Show(this, "Bedtime has to come after the start of your study day, and before the new day starts (" + Fmt.TimeOfDay(newDayHour * 60) + ").",
                    "Study Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Settings r = s.Clone();
            r.GoalMinutes = (int)goalH.Value * 60 + (int)goalM.Value;
            r.DayStart = start;
            r.Bedtime = bed;
            r.NewDayHour = newDayHour;
            r.ExamOn = examOn.Checked;
            r.ExamDate = examDate.Value.Date;
            r.ExamName = examName.Text.Trim().Length > 0 ? examName.Text.Trim() : "exam";
            r.Hotkey = hk;
            r.StartWithWindows = startWin.Checked;
            r.AnnounceEvery = AnnounceChoices[Math.Max(0, announce.SelectedIndex)];
            r.BreakEvery = (int)breakEvery.Value;
            r.Water = water.Checked;
            r.AutoStop = (int)autoStop.Value;
            r.NagAfter = (int)nagAfter.Value;
            r.NagEvery = (int)nagEvery.Value;
            r.AwayAfter = (int)awayAfter.Value;
            r.Tone = (Tone)Math.Max(0, tone.SelectedIndex);
            r.Voice = SelectedVoice();
            r.Volume = volume.Value;
            r.Rate = rate.Value;
            Result = r;
            DialogResult = DialogResult.OK;
        }

        // ---------------------------------------------------------------- little builders

        int Px(int px)
        {
            return (int)Math.Round(px * dpi);
        }

        FlowLayoutPanel Column()
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.FlowDirection = FlowDirection.TopDown;
            f.WrapContents = false;
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            f.Margin = new Padding(0);
            return f;
        }

        TableLayoutPanel Group(FlowLayoutPanel column, string title)
        {
            GroupBox g = new GroupBox();
            g.Text = title;
            g.AutoSize = true;
            g.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            g.Padding = new Padding(Px(10), Px(6), Px(10), Px(8));
            g.Margin = new Padding(0, 0, Px(10), Px(10));
            g.MinimumSize = new Size(Px(360), 0);
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 2;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Dock = DockStyle.Fill;
            g.Controls.Add(t);
            column.Controls.Add(g);
            return t;
        }

        void Row(TableLayoutPanel t, string label, Control c)
        {
            Label l = new Label();
            l.Text = label;
            l.AutoSize = true;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, Px(4), Px(10), Px(4));
            c.Anchor = AnchorStyles.Left;
            t.Controls.Add(l);
            t.Controls.Add(c);
        }

        void Wide(TableLayoutPanel t, Control c)
        {
            c.Anchor = AnchorStyles.Left;
            t.Controls.Add(c);
            t.SetColumnSpan(c, 2);
        }

        FlowLayoutPanel Inline(params Control[] controls)
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            f.WrapContents = false;
            f.Margin = new Padding(0, Px(2), 0, Px(2));
            foreach (Control c in controls)
            {
                if (!(c is Label)) c.Margin = new Padding(0, 0, Px(4), 0);
                f.Controls.Add(c);
            }
            return f;
        }

        Label Lbl(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(0, Px(4), Px(6), 0);
            return l;
        }

        Label Hint(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.MaximumSize = new Size(Px(340), 0);
            l.ForeColor = Color.FromArgb(108, 114, 122);
            l.Font = new Font("Segoe UI", 8f);
            l.Margin = new Padding(0, 0, 0, Px(6));
            return l;
        }

        CheckBox Check(string text, bool value)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Checked = value;
            c.Margin = new Padding(0, Px(4), 0, Px(4));
            return c;
        }

        NumericUpDown Num(int min, int max, int value, int width)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, value));
            n.Width = Px(width);
            return n;
        }

        DateTimePicker TimePicker(int minutes)
        {
            DateTimePicker p = new DateTimePicker();
            p.Format = DateTimePickerFormat.Custom;
            p.CustomFormat = "h:mm tt";
            p.ShowUpDown = true;
            p.Width = Px(90);
            p.Value = DateTime.Today.AddMinutes(minutes);
            return p;
        }

        TrackBar Slider(int min, int max, int value, int tick)
        {
            TrackBar b = new TrackBar();
            b.Minimum = min;
            b.Maximum = max;
            b.Value = Math.Max(min, Math.Min(max, value));
            b.TickFrequency = tick;
            b.Width = Px(200);
            b.AutoSize = false;
            b.Height = Px(32);
            return b;
        }
    }
}
