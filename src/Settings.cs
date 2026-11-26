using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StudyMonitor
{
    enum Tone { Gentle, Normal, Brutal }

    // Everything you can change in the Settings window. Saved to data\settings.ini.
    class Settings
    {
        public int GoalMinutes = 180;     // daily goal
        public int AnnounceEvery = 5;     // say the time every N minutes while studying, 0 = off
        public int BreakEvery = 30;       // "stand up" after N minutes in one session, 0 = off
        public bool Water = true;         // random "drink water" reminders
        public int NagAfter = 60;         // first nag after N minutes on the laptop without studying, 0 = off
        public int NagEvery = 35;         // then roughly every N minutes (randomized)
        public Tone Tone = Tone.Normal;
        public int DayStart = 9 * 60;     // when your study day starts (minutes after midnight)
        public int Bedtime = 0;           // bedtime (minutes after midnight, 0 = midnight)
        public int NewDayHour = 4;        // "today" resets at this hour, so late nights count for the day before
        public bool ExamOn = false;
        public DateTime ExamDate = DateTime.Today.AddDays(30);
        public string ExamName = "exam";
        public string Voice = "";         // empty = first female English voice
        public int Volume = 100;          // 0..100
        public int Rate = 0;              // -5..5
        public int AwayAfter = 10;        // no keyboard/mouse for N minutes = you're away
        public int AutoStop = 120;        // stop a session after N minutes with no input, 0 = never
        public string Hotkey = "Ctrl+Alt+S";
        public bool StartWithWindows = true;

        public Settings Clone()
        {
            return (Settings)MemberwiseClone();
        }

        public static Settings Load(string path)
        {
            Settings s = new Settings();
            Dictionary<string, string> d = Ini.ToDict(Ini.ReadPairs(path));
            s.GoalMinutes = Ini.Int(d, "goal_minutes", s.GoalMinutes, 0, 24 * 60);
            s.AnnounceEvery = Ini.Int(d, "announce_every", s.AnnounceEvery, 0, 240);
            s.BreakEvery = Ini.Int(d, "break_every", s.BreakEvery, 0, 600);
            s.Water = Ini.Bool(d, "water", s.Water);
            s.NagAfter = Ini.Int(d, "nag_after", s.NagAfter, 0, 24 * 60);
            s.NagEvery = Ini.Int(d, "nag_every", s.NagEvery, 5, 24 * 60);
            string tone = Ini.Str(d, "tone", "normal").ToLowerInvariant();
            s.Tone = tone == "gentle" ? Tone.Gentle : tone == "brutal" ? Tone.Brutal : Tone.Normal;
            s.DayStart = Ini.Time(d, "day_start", s.DayStart);
            s.Bedtime = Ini.Time(d, "bedtime", s.Bedtime);
            s.NewDayHour = Ini.Int(d, "new_day_hour", s.NewDayHour, 0, 12);
            s.ExamOn = Ini.Bool(d, "exam_on", s.ExamOn);
            DateTime exam;
            if (Ini.TryDay(Ini.Str(d, "exam_date", ""), out exam)) s.ExamDate = exam;
            s.ExamName = Ini.Str(d, "exam_name", s.ExamName);
            if (s.ExamName.Length == 0) s.ExamName = "exam";
            s.Voice = Ini.Str(d, "voice", s.Voice);
            s.Volume = Ini.Int(d, "volume", s.Volume, 0, 100);
            s.Rate = Ini.Int(d, "rate", s.Rate, -5, 5);
            s.AwayAfter = Ini.Int(d, "away_after", s.AwayAfter, 1, 120);
            s.AutoStop = Ini.Int(d, "auto_stop", s.AutoStop, 0, 24 * 60);
            s.Hotkey = Ini.Str(d, "hotkey", s.Hotkey);
            s.StartWithWindows = Ini.Bool(d, "start_with_windows", s.StartWithWindows);
            return s;
        }

        public void Save(string path)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("# Study Monitor settings. Easier to change in the app (Settings button).");
            b.AppendLine("goal_minutes=" + GoalMinutes);
            b.AppendLine("announce_every=" + AnnounceEvery);
            b.AppendLine("break_every=" + BreakEvery);
            b.AppendLine("water=" + (Water ? "true" : "false"));
            b.AppendLine("nag_after=" + NagAfter);
            b.AppendLine("nag_every=" + NagEvery);
            b.AppendLine("tone=" + Tone.ToString().ToLowerInvariant());
            b.AppendLine("day_start=" + Fmt.Hm(DayStart));
            b.AppendLine("bedtime=" + Fmt.Hm(Bedtime));
            b.AppendLine("new_day_hour=" + NewDayHour);
            b.AppendLine("exam_on=" + (ExamOn ? "true" : "false"));
            b.AppendLine("exam_date=" + Ini.Day(ExamDate));
            b.AppendLine("exam_name=" + ExamName);
            b.AppendLine("voice=" + Voice);
            b.AppendLine("volume=" + Volume);
            b.AppendLine("rate=" + Rate);
            b.AppendLine("away_after=" + AwayAfter);
            b.AppendLine("auto_stop=" + AutoStop);
            b.AppendLine("hotkey=" + Hotkey);
            b.AppendLine("start_with_windows=" + (StartWithWindows ? "true" : "false"));
            Ini.WriteAtomic(path, b.ToString());
        }
    }
}
