using System;
using System.Collections.Generic;
using System.IO;

namespace StudyMonitor
{
    // Simulates hours of use with a fake clock and fake keyboard/mouse idle times.
    // Run it with tests\run-tests.bat (Windows) - it builds a small console exe from the app's logic files.
    static class Tests
    {
        static int passed, failed;
        static readonly List<string> dirs = new List<string>();

        static int Main()
        {
            Run("says the time, the goal milestones and the stand-up reminder", Announcements);
            Run("stopping after 12 minutes while behind gets the 'going to fail' line", ShortStopBehind);
            Run("picks the right kind of line when you stop", StopKinds);
            Run("nags only while you're at the laptop, randomized and escalating", Nags);
            Run("doesn't nag while you're not touching the keyboard or mouse", NagWaitsForYou);
            Run("late evening: 'goal is out of reach' nag, after bedtime: 'go to sleep' nag", LateAndLostNags);
            Run("going away pauses the off-task clock", AwayPauses);
            Run("laptop sleep ends the session when it fell asleep", SleepGap);
            Run("sleep event ends the session and mentions it after waking", SuspendEvent);
            Run("quick restart resumes the session", ResumeAfterRestart);
            Run("crash logs the session up to the last heartbeat", RecoverAfterCrash);
            Run("2 hours without keyboard or mouse stops a forgotten session", AutoStop);
            Run("streaks, 7-day average and the 'at this rate' numbers", StreakAndAverage);
            Run("late-night sessions count for the day before (4 AM reset)", Rollover);
            Run("tone: gentle never says harsh lines, brutal only harsh ones", ToneFilter);
            Run("lines whose placeholders have no value are skipped", Placeholders);
            Run("lines don't repeat until the section is used up", NoRepeats);
            Run("sessions survive sessions.csv being locked (open in Excel)", LockedCsv);
            Run("settings round-trip through settings.ini", SettingsRoundTrip);
            Run("spoken durations and times", Formatting);

            foreach (string d in dirs)
            {
                try { Directory.Delete(d, true); }
                catch { }
            }
            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- harness

        static void Run(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine("PASS  " + name);
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine("FAIL  " + name);
                Console.WriteLine("      " + ex.Message);
            }
        }

        static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception(what);
        }

        static string NewDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "studymonitor-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            dirs.Add(d);
            return d;
        }

        // Defaults with the random extras off, so tests only hear what they're checking.
        static Settings Quiet()
        {
            Settings s = new Settings();
            s.Water = false;
            s.AnnounceEvery = 0;
            s.BreakEvery = 0;
            s.NagAfter = 0;
            s.AutoStop = 0;
            return s;
        }

        class Rig
        {
            public Engine E;
            public string Dir;
            public DateTime Now;
            public readonly List<string> Sections = new List<string>();
            public readonly List<string> Spoken = new List<string>();

            public Rig(Settings s, DateTime start) : this(s, start, NewDir()) { }

            public Rig(Settings s, DateTime start, string dir)
            {
                Dir = dir;
                Now = start;
                E = new Engine(s, dir, start, new Random(7));
                E.Say = delegate(string text, bool interrupt) { Spoken.Add(text); };
                E.Picked = delegate(string section) { Sections.Add(section); };
                E.Tick(Now, 0);      // first tick says hello; clear it so tests start clean
                Sections.Clear();
                Spoken.Clear();
            }

            // Advance the clock in steps, ticking like the real app.
            public void Run(TimeSpan span, int idleSec, int stepSec)
            {
                DateTime end = Now + span;
                while (Now < end)
                {
                    Now = Now.AddSeconds(stepSec);
                    E.Tick(Now, idleSec);
                }
            }

            public void Minutes(double minutes)
            {
                Run(TimeSpan.FromMinutes(minutes), 0, 10);
            }

            public void Toggle()
            {
                E.Toggle(Now);
            }
        }

        static DateTime At(int hour, int minute)
        {
            return new DateTime(2026, 11, 24, hour, minute, 0);
        }

        static void AddSession(Engine e, DateTime start, double minutes, int goal)
        {
            Session s = new Session();
            s.Start = start;
            s.End = start.AddMinutes(minutes);
            s.Goal = goal;
            e.History.Add(s);
        }

        // ---------------------------------------------------------------- tests

        static void Announcements()
        {
            Settings s = Quiet();
            s.GoalMinutes = 180;
            s.AnnounceEvery = 5;
            s.BreakEvery = 30;
            Rig r = new Rig(s, At(10, 0));
            r.Toggle();
            Check(r.Sections.Contains("start"), "no start line");

            Dictionary<int, List<string>> byMinute = new Dictionary<int, List<string>>();
            Dictionary<int, string> textByMinute = new Dictionary<int, string>();
            int seen = 0, spokenSeen = 0;
            for (int sec = 10; sec <= 181 * 60; sec += 10)
            {
                r.Now = At(10, 0).AddSeconds(sec);
                r.E.Tick(r.Now, 0);
                int minute = sec / 60;
                while (seen < r.Sections.Count)
                {
                    if (!byMinute.ContainsKey(minute)) byMinute[minute] = new List<string>();
                    byMinute[minute].Add(r.Sections[seen++]);
                }
                while (spokenSeen < r.Spoken.Count) textByMinute[minute] = r.Spoken[spokenSeen++];
            }

            Check(byMinute.ContainsKey(5) && byMinute[5].Contains("tick"), "nothing said at 5 minutes");
            Check(!byMinute.ContainsKey(7), "said something at 7 minutes");
            Check(byMinute[30].Contains("tick_goal") && byMinute[30].Contains("break"), "30 min should be time + goal left + stand up, got " + string.Join(",", byMinute[30].ToArray()));
            Check(!textByMinute[30].Contains("straight") && !textByMinute[30].Contains("sitting for"), "time said twice at 30 min: " + textByMinute[30]);
            Check(byMinute[90].Contains("goal_half"), "no halfway line at 90 of 180");
            Check(byMinute[165].Contains("goal_close"), "no 'close to goal' line at 165 of 180");
            Check(byMinute[180].Contains("goal_hit"), "no goal line at 180");
            int halves = 0;
            foreach (List<string> l in byMinute.Values) foreach (string x in l) if (x == "goal_half") halves++;
            Check(halves == 1, "halfway said " + halves + " times");
        }

        static void ShortStopBehind()
        {
            Settings s = Quiet();
            s.GoalMinutes = 180;
            Rig r = new Rig(s, At(18, 0));
            r.Toggle();
            r.Minutes(12);
            r.Toggle();
            Check(r.E.LastStop == "stop_short", "expected stop_short, got " + r.E.LastStop);
            Check(r.Spoken.Count > 0 && r.Spoken[r.Spoken.Count - 1].Length > 0, "nothing said");
            Check(Math.Abs(r.E.DoneToday(r.Now) - 12) < 0.01, "logged " + r.E.DoneToday(r.Now) + " minutes");
            Check(File.ReadAllText(Path.Combine(r.Dir, "sessions.csv")).Contains("2026-11-24 18:00:00,2026-11-24 18:12:00,12,180"), "sessions.csv row missing");
        }

        static string StopAfter(int goal, double earlier, DateTime start, double minutes)
        {
            Settings s = Quiet();
            s.GoalMinutes = goal;
            Rig r = new Rig(s, start);
            if (earlier > 0) AddSession(r.E, start.AddMinutes(-earlier - 30), earlier, goal);
            r.Toggle();
            r.Minutes(minutes);
            r.Toggle();
            return r.E.LastStop;
        }

        static void StopKinds()
        {
            string k;
            k = StopAfter(60, 45, At(15, 0), 5);
            Check(k == "stop_close", "10 min from goal: " + k);
            k = StopAfter(60, 50, At(15, 0), 15);
            Check(k == "stop_goal_done", "just hit the goal: " + k);
            k = StopAfter(60, 70, At(15, 0), 10);
            Check(k == "stop_after_goal", "bonus session: " + k);
            k = StopAfter(180, 0, At(12, 0), 40);
            Check(k == "stop_good", "on pace at noon: " + k);
            k = StopAfter(180, 0, At(23, 0), 25);
            Check(k == "stop_impossible", "can't make it before midnight: " + k);
            k = StopAfter(180, 0, At(20, 0), 30);
            Check(k == "stop_behind", "behind in the evening: " + k);

            Settings s = Quiet();
            Rig r = new Rig(s, At(10, 0));
            r.Toggle();
            r.Run(TimeSpan.FromSeconds(30), 0, 10);
            r.Toggle();
            Check(r.Sections.Contains("cancelled"), "under a minute should be cancelled");
            Check(r.E.DoneToday(r.Now) == 0, "a 30 second session was logged");
        }

        static void Nags()
        {
            Settings s = Quiet();
            s.GoalMinutes = 180;
            s.NagAfter = 60;
            s.NagEvery = 35;
            Rig r = new Rig(s, At(13, 0));
            List<double> times = new List<double>();
            List<string> kinds = new List<string>();
            int seen = 0;
            for (int i = 0; i < 6 * 240; i++)
            {
                r.Now = r.Now.AddSeconds(10);
                r.E.Tick(r.Now, 5);
                while (seen < r.Sections.Count)
                {
                    times.Add(r.E.OffTaskSec / 60);
                    kinds.Add(r.Sections[seen++]);
                }
            }
            Check(times.Count >= 4, "only " + times.Count + " nags in 4 hours");
            Check(times[0] >= 60 && times[0] < 61, "first nag at " + times[0] + " min");
            Check(kinds[0] == "nag_1", "first nag was " + kinds[0]);
            for (int i = 1; i < times.Count; i++)
            {
                double gap = times[i] - times[i - 1];
                Check(gap >= 26 && gap <= 45, "gap between nags: " + gap + " min");
            }
            Check(kinds.Contains("nag_2"), "never escalated to nag_2");
            Check(kinds[kinds.Count - 1] == "nag_3", "after 3 hours expected nag_3, got " + kinds[kinds.Count - 1]);

            r.Toggle();
            Check(r.E.OffTaskSec == 0, "starting a session should reset the off-task clock");
        }

        static void NagWaitsForYou()
        {
            Settings s = Quiet();
            s.NagAfter = 30;
            Rig r = new Rig(s, At(13, 0));
            r.Run(TimeSpan.FromMinutes(40), 300, 10);   // 5 min since last input: counts as on the laptop, but not there to hear it
            Check(r.Sections.Count == 0, "nagged while you weren't touching anything");
            Check(r.E.OffTaskSec >= 39 * 60, "off-task should still count, got " + r.E.OffTaskSec / 60 + " min");
            r.Run(TimeSpan.FromSeconds(20), 0, 10);
            Check(r.Sections.Count == 1 && r.Sections[0] == "nag_1", "should nag once you're back");
        }

        static void LateAndLostNags()
        {
            Settings s = Quiet();
            s.GoalMinutes = 180;
            s.NagAfter = 30;
            Rig evening = new Rig(s, At(22, 0));
            evening.Minutes(31);   // 3 hours still to do, 1.5 hours until midnight
            Check(evening.Sections.Contains("nag_lost"), "at 10:31 PM expected nag_lost, got " + string.Join(",", evening.Sections.ToArray()));
            Rig night = new Rig(s, new DateTime(2026, 11, 25, 0, 10, 0));
            night.Minutes(31);
            Check(night.Sections.Contains("nag_late"), "after midnight expected nag_late, got " + string.Join(",", night.Sections.ToArray()));
        }

        static void AwayPauses()
        {
            Settings s = Quiet();
            Rig r = new Rig(s, At(13, 0));
            r.Minutes(20);
            Check(Math.Abs(r.E.OffTaskSec - 1200) <= 10, "after 20 min: " + r.E.OffTaskSec);
            DateTime lastInput = r.Now;
            for (int i = 0; i < 180; i++)
            {
                r.Now = r.Now.AddSeconds(10);
                r.E.Tick(r.Now, (int)(r.Now - lastInput).TotalSeconds);
            }
            Check(r.E.Away, "should be away after 30 min without input");
            Check(Math.Abs(r.E.OffTaskSec - 1200) <= 20, "being away added time: " + r.E.OffTaskSec);
            r.Minutes(5);
            Check(!r.E.Away && Math.Abs(r.E.OffTaskSec - 1500) <= 20, "after coming back: " + r.E.OffTaskSec);
        }

        static void SleepGap()
        {
            Settings s = Quiet();
            Rig r = new Rig(s, At(10, 0));
            r.Toggle();
            r.Minutes(20);
            r.Now = At(13, 0);
            r.E.Tick(r.Now, 0);
            Check(!r.E.Studying, "still studying after the laptop slept");
            Check(Math.Abs(r.E.DoneToday(r.Now) - 20) < 0.01, "logged " + r.E.DoneToday(r.Now) + " min instead of 20");
            Check(r.E.LastSection == "slept", "didn't mention it");
            Check(r.Spoken[r.Spoken.Count - 1].Contains("10:20 AM"), "should say when it stopped: " + r.Spoken[r.Spoken.Count - 1]);
        }

        static void SuspendEvent()
        {
            Settings s = Quiet();
            Rig r = new Rig(s, At(10, 0));
            r.Toggle();
            r.Minutes(15);
            r.E.Suspend(r.Now);
            Check(!r.E.Studying, "still studying after suspend");
            Check(!r.Sections.Contains("slept"), "talked while going to sleep");
            r.Now = At(12, 0);
            r.E.Tick(r.Now, 0);
            Check(r.Sections.Contains("slept"), "didn't mention it after waking");
            Check(Math.Abs(r.E.DoneToday(r.Now) - 15) < 0.01, "logged " + r.E.DoneToday(r.Now));
        }

        static void ResumeAfterRestart()
        {
            Settings s = Quiet();
            Rig a = new Rig(s, At(10, 0));
            a.Toggle();
            a.Minutes(30);
            // the app is killed here and started again a minute later
            Rig b = new Rig(s, a.Now.AddMinutes(1), a.Dir);
            Check(b.E.Studying, "session wasn't resumed");
            Check(b.E.SessionStart == At(10, 0), "resumed with the wrong start time");
            Check(b.E.History.Total(b.E.History.DayOf(b.Now)) == 0, "logged the session instead of resuming it");
        }

        static void RecoverAfterCrash()
        {
            Settings s = Quiet();
            Rig a = new Rig(s, At(10, 0));
            a.Toggle();
            a.Minutes(30);
            Engine b = new Engine(s, a.Dir, At(12, 0), new Random(1));
            List<string> said = new List<string>();
            b.Say = delegate(string text, bool interrupt) { said.Add(text); };
            Check(!b.Studying, "a 90 minute old session shouldn't resume");
            double logged = b.DoneToday(At(12, 0));
            Check(logged >= 29 && logged <= 30.01, "recovered " + logged + " minutes, expected about 30");
            b.Tick(At(12, 0), 0);
            Check(said.Count > 0 && said[0].Contains("logged it until"), "didn't say it recovered the session");
        }

        static void AutoStop()
        {
            Settings s = Quiet();
            s.AutoStop = 120;
            Rig r = new Rig(s, At(9, 0));
            r.Toggle();
            r.Minutes(10);
            DateTime lastInput = r.Now;
            bool warned = false;
            for (int i = 0; i < 2 * 60 * 2 + 4; i++)
            {
                r.Now = r.Now.AddSeconds(30);
                r.E.Tick(r.Now, (int)(r.Now - lastInput).TotalSeconds);
                if (r.Sections.Contains("auto_stop_warn") && !warned)
                {
                    warned = true;
                    Check((r.Now - lastInput).TotalMinutes >= 110 && r.E.Studying, "warned too early");
                }
            }
            Check(warned, "never warned before stopping");
            Check(!r.E.Studying, "session still running after 2 idle hours");
            Check(Math.Abs(r.E.DoneToday(r.Now) - 10) < 0.01, "should end at the last input, logged " + r.E.DoneToday(r.Now));
            Check(r.Sections.Contains("auto_stopped"), "didn't say why it stopped");
        }

        static void StreakAndAverage()
        {
            string dir = NewDir();
            File.WriteAllText(Path.Combine(dir, "messages.txt"), "[probe]\n{week_short}|{month_short}|{avg}|{streak}\n");
            Settings s = Quiet();
            s.GoalMinutes = 60;
            Rig r = new Rig(s, At(10, 0), dir);
            AddSession(r.E, At(10, 0).AddDays(-4), 20, 60);
            AddSession(r.E, At(10, 0).AddDays(-3), 70, 60);
            AddSession(r.E, At(10, 0).AddDays(-2), 65, 60);
            AddSession(r.E, At(10, 0).AddDays(-1), 80, 60);
            Check(r.E.Streak(r.Now) == 3, "streak " + r.E.Streak(r.Now) + ", expected 3");
            double? avg = r.E.Average(r.Now);
            Check(avg.HasValue && Math.Abs(avg.Value - 58.75) < 0.01, "average " + avg);

            // Raising the goal later doesn't break the streak earned under the old goal.
            Settings harder = s.Clone();
            harder.GoalMinutes = 120;
            r.E.UpdateSettings(harder, r.Now);
            string probe = r.E.Line("probe");
            Check(probe == "7 hours|31 hours|59 minutes|3", "got " + probe);

            AddSession(r.E, At(8, 0), 120, 120);
            Check(r.E.Streak(r.Now) == 4, "today's goal should extend the streak");
        }

        static void Rollover()
        {
            Settings s = Quiet();
            Rig r = new Rig(s, new DateTime(2026, 11, 24, 2, 30, 0));
            AddSession(r.E, new DateTime(2026, 11, 24, 1, 0, 0), 60, 180);
            Check(r.E.History.DayOf(new DateTime(2026, 11, 24, 1, 0, 0)) == new DateTime(2026, 11, 23), "1 AM should belong to the day before");
            Check(Math.Abs(r.E.DoneToday(r.Now) - 60) < 0.01, "2:30 AM should still be 'today' = the 23rd");
            Check(r.E.DoneToday(new DateTime(2026, 11, 24, 4, 30, 0)) == 0, "4:30 AM is a new day");
            Check(File.ReadAllText(Path.Combine(r.Dir, "sessions.csv")).Contains("2026-11-23,2026-11-24 01:00:00"), "csv day column wrong");
        }

        static Messages MessagesWith(string text)
        {
            string dir = NewDir();
            string path = Path.Combine(dir, "messages.txt");
            File.WriteAllText(path, text);
            return new Messages(path, new Random(3));
        }

        static string NoValue(string name)
        {
            return name == "ok" ? "yes" : null;
        }

        static void ToneFilter()
        {
            Messages m = MessagesWith("[x]\nsoft one\n!harsh one\nsoft two\n!harsh two\n");
            bool sawSoft = false, sawHarsh = false;
            for (int i = 0; i < 40; i++)
            {
                Check(!m.Pick("x", Tone.Gentle, NoValue).StartsWith("harsh"), "gentle said a harsh line");
                Check(m.Pick("x", Tone.Brutal, NoValue).StartsWith("harsh"), "brutal said a soft line");
                string n = m.Pick("x", Tone.Normal, NoValue);
                if (n.StartsWith("soft")) sawSoft = true;
                if (n.StartsWith("harsh")) sawHarsh = true;
            }
            Check(sawSoft && sawHarsh, "normal should mix both");
            Check(m.Pick("x", Tone.Brutal, NoValue).IndexOf('!') < 0, "the ! marker was spoken");
        }

        static void Placeholders()
        {
            Messages m = MessagesWith("[y]\na {missing}\nb {ok}\nc {typo_here}\n");
            for (int i = 0; i < 10; i++) Check(m.Pick("y", Tone.Normal, NoValue) == "b yes", "used a line with no value");
            Check(m.Pick("nope", Tone.Normal, NoValue) == null, "unknown section should give nothing");
            Messages d = MessagesWith("# empty file\n");
            Check(d.Pick("water", Tone.Normal, NoValue) != null, "missing sections should fall back to the built-in lines");
        }

        static void NoRepeats()
        {
            Messages m = MessagesWith("[z]\none\ntwo\nthree\nfour\n");
            string last = null;
            for (int round = 0; round < 5; round++)
            {
                List<string> got = new List<string>();
                for (int i = 0; i < 4; i++)
                {
                    string p = m.Pick("z", Tone.Normal, NoValue);
                    Check(p != last, "same line twice in a row: " + p);
                    Check(!got.Contains(p), "repeated before using all lines: " + p);
                    got.Add(p);
                    last = p;
                }
            }
        }

        static void LockedCsv()
        {
            Settings s = Quiet();
            Rig r = new Rig(s, At(10, 0));
            string csv = Path.Combine(r.Dir, "sessions.csv");
            Directory.CreateDirectory(csv);   // something that can't be written to, like a file Excel has open
            r.Toggle();
            r.Minutes(10);
            r.Toggle();
            Check(r.E.Problem != null, "no warning shown");
            Check(File.ReadAllText(Path.Combine(r.Dir, "state.ini")).Contains("pending=2026-11-24 10:00:00|2026-11-24 10:10:00"), "unsaved session not kept in state.ini");

            // Restart while it's still locked: the session must not get lost.
            Rig b = new Rig(s, r.Now.AddMinutes(1), r.Dir);
            Check(Math.Abs(b.E.DoneToday(b.Now) - 10) < 0.01, "session lost on restart");
            Directory.Delete(csv);
            b.Minutes(1.5);
            Check(b.E.Problem == null, "warning didn't clear");
            string[] lines = File.ReadAllLines(csv);
            Check(lines.Length == 2 && lines[1].StartsWith("2026-11-24,2026-11-24 10:00:00,2026-11-24 10:10:00"), "csv: " + string.Join(" / ", lines));
            Check(!File.ReadAllText(Path.Combine(r.Dir, "state.ini")).Contains("pending="), "still pending after saving");
        }

        static void SettingsRoundTrip()
        {
            string path = Path.Combine(NewDir(), "settings.ini");
            Settings s = new Settings();
            s.GoalMinutes = 200;
            s.Tone = Tone.Brutal;
            s.Bedtime = 60;
            s.DayStart = 10 * 60 + 30;
            s.ExamOn = true;
            s.ExamDate = new DateTime(2027, 1, 15);
            s.ExamName = "finals";
            s.Voice = "Microsoft Zira Desktop - English (United States)";
            s.Hotkey = "Ctrl+Shift+F9";
            s.Water = false;
            s.Rate = -2;
            s.Save(path);
            Settings t = Settings.Load(path);
            Check(t.GoalMinutes == 200 && t.Tone == Tone.Brutal && t.Bedtime == 60 && t.DayStart == 630, "basic values");
            Check(t.ExamOn && t.ExamDate == new DateTime(2027, 1, 15) && t.ExamName == "finals", "exam");
            Check(t.Voice == s.Voice && t.Hotkey == s.Hotkey && !t.Water && t.Rate == -2, "voice/hotkey/water/rate");
        }

        static void Formatting()
        {
            Check(Fmt.Dur(0.4) == "less than a minute", Fmt.Dur(0.4));
            Check(Fmt.Dur(1) == "1 minute", Fmt.Dur(1));
            Check(Fmt.Dur(61) == "1 hour 1 minute", Fmt.Dur(61));
            Check(Fmt.Dur(195) == "3 hours 15 minutes", Fmt.Dur(195));
            Check(Fmt.Dur(120) == "2 hours", Fmt.Dur(120));
            Check(Fmt.Big(600) == "10 hours", Fmt.Big(600));
            Check(Fmt.Short(179.9) == "2h 59m", Fmt.Short(179.9));
            Check(Fmt.TimeOfDay(0) == "midnight", Fmt.TimeOfDay(0));
            Check(Fmt.TimeOfDay(60) == "1 AM", Fmt.TimeOfDay(60));
            Check(Fmt.TimeOfDay(13 * 60 + 30) == "1:30 PM", Fmt.TimeOfDay(13 * 60 + 30));
            Check(Fmt.Clock(TimeSpan.FromSeconds(3725)) == "1:02:05", Fmt.Clock(TimeSpan.FromSeconds(3725)));
        }
    }
}
