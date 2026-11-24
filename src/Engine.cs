using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace StudyMonitor
{
    enum StopReason { Manual, Slept, Idle, Recovered, Quit }

    // The numbers every decision is based on.
    class Plan
    {
        public double Left;        // minutes of today's goal still to do
        public double Expected;    // where you should be by now
        public double? Pace;       // studied / expected (no value early in the day or without a goal)
        public int MinutesToBed;   // negative = past bedtime
        public bool Behind;        // under 75% of where you should be
        public bool Danger;        // you'd need more than 60% of the time left before bed
        public bool Impossible;    // not enough time left before bed to hit the goal
        public bool Late;          // past bedtime
    }

    // The brain. Tracks sessions and off-task time and decides what to say and when.
    // It never reads the clock itself (the window passes the time in), so it can be tested.
    class Engine
    {
        public Settings S;
        public readonly History History;
        public readonly Messages Messages;
        public Action<string, bool> Say = delegate { };   // text, cut off whatever is playing
        public Action<string> Picked = delegate { };      // which situation a line came from (used by the tests)

        public bool Studying { get; private set; }
        public DateTime SessionStart { get; private set; }
        public double OffTaskSec { get; private set; }    // time on the laptop since you last studied
        public bool Away { get; private set; }
        public string LastSaid { get; private set; }
        public string LastSection { get; private set; }
        public string LastStop { get; private set; }
        public string Problem { get; private set; }       // shown in the window when something needs attention
        public bool TrayHintShown;

        readonly string statePath;
        readonly Random rng;
        readonly List<string> queue = new List<string>();
        bool ready, firstRun;
        DateTime clock;
        DateTime lastTick = DateTime.MinValue;
        DateTime lastSpoke = DateTime.MinValue;
        DateTime lastSaved = DateTime.MinValue;
        DateTime greeted = DateTime.MinValue;
        DateTime day, nextWater, lastEnd;
        double nextNagSec, startOffTask, nextBreakMin;
        double stopMinutes = -1;
        int lastAnnounced;
        bool saidHalf, saidClose, saidGoal, warnedIdle, elapsedSaid, sleptPending;

        public Engine(Settings settings, string dataDir, DateTime now, Random rng)
        {
            S = settings;
            this.rng = rng;
            clock = now;
            statePath = Path.Combine(dataDir, "state.ini");
            History = new History(Path.Combine(dataDir, "sessions.csv"), S.NewDayHour);
            Messages = new Messages(Path.Combine(dataDir, "messages.txt"), rng);
            day = History.DayOf(now);
            nextNagSec = S.NagAfter * 60;
            nextWater = now.AddMinutes(40 + rng.Next(41));
            LoadState(now);
            InitDayFlags(now);
            SaveState(now);
        }

        // ---------------------------------------------------------------- the loop

        // Called every second or so with the current time and seconds since the last keyboard/mouse input.
        public void Tick(DateTime now, int idleSec)
        {
            clock = now;
            if (!ready)
            {
                ready = true;
                if (queue.Count > 0) Say(string.Join(" ", queue.ToArray()), false);
                queue.Clear();
            }

            if (lastTick == DateTime.MinValue) lastTick = now;
            double dt = (now - lastTick).TotalSeconds;
            if (dt < 0) dt = 0;
            if (dt > 150)
            {
                // Over 2.5 minutes since the last tick: the laptop was asleep. Don't count that time.
                if (Studying) StopSession(lastTick, StopReason.Slept);
                dt = 0;
            }
            lastTick = now;
            if (sleptPending)
            {
                sleptPending = false;
                Speak("slept", false);
            }

            DateTime today = History.DayOf(now);
            if (today != day) NewDay(now, today);

            // Say hello once a day, the first time you're at the laptop.
            if (greeted != today && idleSec < 60)
            {
                greeted = today;
                Speak(firstRun ? "welcome" : "new_day", false);
                firstRun = false;
            }

            double done = DoneToday(now);
            if (Studying) TickStudying(now, idleSec, done);
            else TickOffTask(now, idleSec, dt, done);

            // Water: at random, only when someone's there to hear it.
            if (S.Water && now >= nextWater && (Studying || idleSec < 60) && QuietFor(now, 45))
            {
                Speak("water", false);
                nextWater = now.AddMinutes(40 + rng.Next(41));
            }

            // Heartbeat, so a crash or power cut loses at most a minute.
            if ((now - lastSaved).TotalSeconds >= 60)
            {
                if (History.Unsaved.Count > 0 && History.Flush()) Problem = null;
                SaveState(now);
            }
        }

        void TickStudying(DateTime now, int idleSec, double done)
        {
            double elapsed = (now - SessionStart).TotalMinutes;
            int whole = (int)Math.Floor(elapsed);
            int goal = S.GoalMinutes;

            // Forgot to stop? After a long time with no keyboard or mouse, end the session at the last input.
            if (S.AutoStop > 0)
            {
                if (idleSec >= S.AutoStop * 60)
                {
                    DateTime lastInput = now.AddSeconds(-idleSec);
                    StopSession(lastInput < SessionStart ? SessionStart : lastInput, StopReason.Idle);
                    return;
                }
                if (idleSec < 60) warnedIdle = false;
                else if (!warnedIdle && S.AutoStop >= 20 && idleSec >= (S.AutoStop - 10) * 60)
                {
                    warnedIdle = true;
                    Speak("auto_stop_warn", false);
                }
            }

            List<string> parts = new List<string>();
            if (goal > 0)
            {
                if (!saidGoal && done >= goal)
                {
                    saidGoal = saidClose = saidHalf = true;
                    Add(parts, "goal_hit");
                }
                else if (!saidClose && goal >= 30 && goal - done <= 15)
                {
                    saidClose = saidHalf = true;
                    Add(parts, "goal_close");
                }
                else if (!saidHalf && goal >= 60 && done >= goal / 2.0)
                {
                    saidHalf = true;
                    Add(parts, "goal_half");
                }
            }

            if (S.AnnounceEvery > 0 && whole > 0 && whole != lastAnnounced && whole % S.AnnounceEvery == 0)
            {
                lastAnnounced = whole;
                bool withGoal = parts.Count == 0 && goal > 0 && done < goal && whole % 30 == 0;
                string tick = Line(withGoal ? "tick_goal" : "tick");
                if (tick != null)
                {
                    parts.Insert(0, tick);
                    elapsedSaid = true;   // so the break line doesn't repeat the time
                }
            }

            if (S.BreakEvery > 0 && elapsed >= nextBreakMin)
            {
                nextBreakMin += S.BreakEvery;
                Add(parts, "break");
            }
            elapsedSaid = false;

            if (parts.Count > 0) SayNow(string.Join(" ", parts.ToArray()), false);
        }

        void TickOffTask(DateTime now, int idleSec, double dt, double done)
        {
            bool away = idleSec >= S.AwayAfter * 60;
            // Just went away: the last few minutes before that weren't really time on the laptop.
            if (away && !Away) OffTaskSec = Math.Max(0, OffTaskSec - S.AwayAfter * 60);
            Away = away;
            if (!away) OffTaskSec += dt;

            int goal = S.GoalMinutes;
            if (S.NagAfter <= 0 || OffTaskSec < nextNagSec) return;
            if (goal > 0 && done >= goal) return;                // goal's done, you're free
            if (idleSec >= 60 || !QuietFor(now, 45)) return;     // only when you're there to hear it

            Plan p = MakePlan(now, done);
            string section;
            if (p.Late) section = "nag_late";
            else if (p.Impossible) section = "nag_lost";
            else
            {
                double hours = OffTaskSec / 3600.0;
                int level = hours >= 3 ? 3 : hours >= 2 ? 2 : 1;
                if (S.Tone == Tone.Gentle) level = 1;
                else if (S.Tone == Tone.Brutal) level = Math.Min(3, level + 1);
                section = "nag_" + level;
            }
            Speak(section, false);
            // Next nag at a random point around the "nag every" setting, so it never feels like a timer.
            nextNagSec = OffTaskSec + S.NagEvery * 60 * (0.75 + rng.NextDouble() * 0.5);
        }

        void NewDay(DateTime now, DateTime today)
        {
            day = today;
            OffTaskSec = 0;
            nextNagSec = S.NagAfter * 60;
            InitDayFlags(now);
        }

        // ---------------------------------------------------------------- sessions

        public void Toggle(DateTime now)
        {
            clock = now;
            if (Studying) StopSession(now, StopReason.Manual);
            else StartSession(now);
        }

        public void StartSession(DateTime now)
        {
            clock = now;
            if (Studying) return;
            startOffTask = OffTaskSec;
            Studying = true;
            SessionStart = now;
            lastAnnounced = 0;
            nextBreakMin = S.BreakEvery > 0 ? S.BreakEvery : double.MaxValue;
            warnedIdle = false;
            OffTaskSec = 0;
            Away = false;
            nextNagSec = S.NagAfter * 60;
            SaveState(now);
            bool goalDone = S.GoalMinutes > 0 && DoneToday(now) >= S.GoalMinutes;
            Speak(goalDone ? "start_after_goal" : "start", true);
            startOffTask = 0;
        }

        public void StopSession(DateTime end, StopReason why)
        {
            if (!Studying) return;
            if (end < SessionStart) end = SessionStart;
            Studying = false;
            double minutes = (end - SessionStart).TotalMinutes;
            double before = History.Total(History.DayOf(SessionStart));
            if (minutes >= 1)
            {
                Session s = new Session();
                s.Start = SessionStart;
                s.End = end;
                s.Goal = S.GoalMinutes;
                if (!History.Add(s)) Problem = "Couldn't write to sessions.csv (is it open in Excel?). Your sessions are safe and will be saved once it's closed.";
            }
            lastEnd = end;
            OffTaskSec = 0;
            Away = false;
            nextNagSec = S.NagAfter * 60;
            warnedIdle = false;
            SaveState(clock > end ? clock : end);

            switch (why)
            {
                case StopReason.Slept: Speak("slept", false); return;
                case StopReason.Idle: Speak("auto_stopped", false); return;
                case StopReason.Recovered: Speak("recovered", false); return;
                case StopReason.Quit: return;
            }
            if (minutes < 1)
            {
                Speak("cancelled", true);
                return;
            }

            string section = StopSection(minutes, before, end);
            LastStop = section;
            stopMinutes = minutes;
            string main = Line(section);
            string kicker = null;
            if (section == "stop_short" || section == "stop_behind" || section == "stop_impossible")
            {
                double chance = S.Tone == Tone.Gentle ? 0.3 : S.Tone == Tone.Brutal ? 0.8 : 0.5;
                if (rng.NextDouble() < chance) kicker = Line("kicker");
            }
            stopMinutes = -1;
            if (main == null) main = kicker;
            else if (kicker != null) main = main + " " + kicker;
            SayNow(main, true);
        }

        // Which kind of "you stopped" line fits.
        string StopSection(double minutes, double before, DateTime end)
        {
            int goal = S.GoalMinutes;
            double done = before + minutes;
            Plan p = MakePlan(end, done);
            if (goal > 0 && before >= goal) return "stop_after_goal";
            if (goal > 0 && done >= goal) return "stop_goal_done";
            if (goal > 0 && p.Left <= 20) return "stop_close";
            if (p.Impossible) return "stop_impossible";
            if (minutes < 20) return p.Behind || !p.Pace.HasValue ? "stop_short" : "stop_short_ok";
            if (p.Behind || p.Danger) return "stop_behind";
            return "stop_good";
        }

        // The laptop is going to sleep: end the session now and mention it after waking up.
        public void Suspend(DateTime now)
        {
            clock = now;
            if (!Studying) return;
            StopSession(now, StopReason.Quit);
            sleptPending = true;
        }

        // The app is closing (quit, or Windows shutting down).
        public void Shutdown(DateTime now)
        {
            clock = now;
            if (Studying) StopSession(now, StopReason.Quit);
            History.Flush();
            SaveState(now);
        }

        public void UpdateSettings(Settings settings, DateTime now)
        {
            clock = now;
            S = settings;
            History.SetNewDayHour(S.NewDayHour);
            day = History.DayOf(now);
            if (Studying)
            {
                double elapsed = (now - SessionStart).TotalMinutes;
                nextBreakMin = S.BreakEvery > 0 ? (Math.Floor(elapsed / S.BreakEvery) + 1) * S.BreakEvery : double.MaxValue;
            }
            nextNagSec = Math.Max(S.NagAfter * 60, OffTaskSec + 120);
            InitDayFlags(now);
            SaveState(now);
        }

        // ---------------------------------------------------------------- numbers

        public Plan MakePlan(DateTime now, double done)
        {
            Plan p = new Plan();
            int goal = S.GoalMinutes;
            p.Left = Math.Max(0, goal - done);
            int nowM = Logical(now.Hour * 60 + now.Minute);
            int start = Logical(S.DayStart);
            int end = Logical(S.Bedtime);
            if (end <= start) end = Math.Min(1440, start + 60);
            double frac = (nowM - start) / (double)(end - start);
            if (frac < 0) frac = 0;
            if (frac > 1) frac = 1;
            p.Expected = goal * frac;
            if (goal > 0 && p.Expected >= 10) p.Pace = done / p.Expected;
            p.Behind = p.Pace.HasValue && p.Pace.Value < 0.75;
            p.MinutesToBed = end - nowM;
            p.Late = p.MinutesToBed < 0;
            p.Impossible = goal > 0 && p.Left > 0 && p.Left > Math.Max(0, p.MinutesToBed);
            p.Danger = goal > 0 && p.Left > 0 && !p.Impossible && p.Left > 0.6 * p.MinutesToBed;
            return p;
        }

        // Minutes since the study day began (the day begins at the "new day" hour, not midnight).
        int Logical(int clockMinutes)
        {
            int m = clockMinutes - S.NewDayHour * 60;
            if (m < 0) m += 1440;
            return m;
        }

        public double DoneToday(DateTime now)
        {
            DateTime today = History.DayOf(now);
            double done = History.Total(today);
            if (Studying && History.DayOf(SessionStart) == today) done += Math.Max(0, (now - SessionStart).TotalMinutes);
            return done;
        }

        public int Streak(DateTime now)
        {
            return History.Streak(History.DayOf(now), DoneToday(now), S.GoalMinutes);
        }

        public double? Average(DateTime now)
        {
            return History.Average(History.DayOf(now), 7);
        }

        // Days until the exam, or -1 if there's none (or it's over).
        public int ExamDays(DateTime now)
        {
            if (!S.ExamOn) return -1;
            int d = (S.ExamDate.Date - History.DayOf(now)).Days;
            return d < 0 ? -1 : d;
        }

        // Minutes studied on each of the last 7 days, oldest first, today last.
        public double[] Last7(DateTime now)
        {
            DateTime today = History.DayOf(now);
            double[] v = new double[7];
            for (int i = 0; i < 7; i++)
            {
                DateTime d = today.AddDays(i - 6);
                v[i] = d == today ? DoneToday(now) : History.Total(d);
            }
            return v;
        }

        public List<Session> TodaySessions(DateTime now)
        {
            return History.On(History.DayOf(now));
        }

        // ---------------------------------------------------------------- talking

        public string Line(string section)
        {
            string text = Messages.Pick(section, S.Tone, Value);
            if (text != null)
            {
                LastSection = section;
                Picked(section);
            }
            return text;
        }

        void Add(List<string> parts, string section)
        {
            string text = Line(section);
            if (text != null) parts.Add(text);
        }

        void Speak(string section, bool interrupt)
        {
            SayNow(Line(section), interrupt);
        }

        void SayNow(string text, bool interrupt)
        {
            if (string.IsNullOrEmpty(text)) return;
            LastSaid = text;
            lastSpoke = clock;
            if (!ready)
            {
                queue.Add(text);
                return;
            }
            Say(text, interrupt);
        }

        bool QuietFor(DateTime now, int seconds)
        {
            return (now - lastSpoke).TotalSeconds >= seconds;
        }

        // The text for a {placeholder}, or null if it has no value right now.
        string Value(string name)
        {
            DateTime now = clock;
            int goal = S.GoalMinutes;
            switch (name)
            {
                case "session":
                    return stopMinutes >= 0 ? Fmt.Dur(stopMinutes) : null;
                case "elapsed":
                    return Studying && !elapsedSaid ? Fmt.Dur(Math.Floor((now - SessionStart).TotalMinutes)) : null;
                case "today":
                    return Fmt.Dur(DoneToday(now));
                case "goal":
                    return goal > 0 ? Fmt.Dur(goal) : null;
                case "left":
                    {
                        double left = goal - DoneToday(now);
                        return goal > 0 && left > 0 ? Fmt.Dur(Math.Ceiling(left)) : null;
                    }
                case "pace":
                    {
                        Plan p = MakePlan(now, DoneToday(now));
                        return p.Pace.HasValue ? Fmt.Percent(p.Pace.Value) : null;
                    }
                case "streak":
                    {
                        int s = Streak(now);
                        return s > 0 ? s.ToString(CultureInfo.InvariantCulture) : null;
                    }
                case "avg":
                    {
                        double? a = Average(now);
                        return a.HasValue ? Fmt.Dur(a.Value) : null;
                    }
                case "week_short":
                    return Shortfall(now, 7);
                case "month_short":
                    return Shortfall(now, 30);
                case "offtask":
                    {
                        double sec = Studying ? startOffTask : OffTaskSec;
                        return sec >= 600 ? Fmt.Dur(Math.Floor(sec / 60)) : null;
                    }
                case "exam":
                    return ExamDays(now) >= 0 ? S.ExamName : null;
                case "exam_in":
                    {
                        int d = ExamDays(now);
                        if (d < 0) return null;
                        if (d == 0) return "today";
                        if (d == 1) return "tomorrow";
                        return "in " + d + " days";
                    }
                case "bedtime":
                    return Fmt.TimeOfDay(S.Bedtime);
                case "time":
                    return Fmt.TimeOfDay(now);
                case "end":
                    return Fmt.TimeOfDay(lastEnd);
                default:
                    return null;
            }
        }

        // How far behind your goal you fall over N days at your 7-day average.
        string Shortfall(DateTime now, int days)
        {
            double? a = Average(now);
            int goal = S.GoalMinutes;
            if (!a.HasValue || goal <= 0 || a.Value >= goal) return null;
            return Fmt.Big((goal - a.Value) * days);
        }

        // Don't announce "halfway" again if the app restarts after you already passed it.
        void InitDayFlags(DateTime now)
        {
            double done = DoneToday(now);
            int goal = S.GoalMinutes;
            saidGoal = goal > 0 && done >= goal;
            saidClose = saidGoal || (goal > 0 && goal - done <= 15);
            saidHalf = saidClose || (goal > 0 && done >= goal / 2.0);
        }

        // ---------------------------------------------------------------- state.ini

        void LoadState(DateTime now)
        {
            firstRun = !File.Exists(statePath);
            List<KeyValuePair<string, string>> pairs = Ini.ReadPairs(statePath);
            Dictionary<string, string> d = Ini.ToDict(pairs);

            // Sessions that couldn't be written to sessions.csv last time.
            foreach (KeyValuePair<string, string> p in pairs)
            {
                if (p.Key != "pending") continue;
                string[] f = p.Value.Split('|');
                Session s = new Session();
                if (f.Length < 2 || !Ini.TryStamp(f[0], out s.Start) || !Ini.TryStamp(f[1], out s.End)) continue;
                if (f.Length > 2) int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Goal);
                if (!History.Has(s.Start) && !History.Add(s)) Problem = "Couldn't write to sessions.csv (is it open in Excel?). Your sessions are safe and will be saved once it's closed.";
            }

            DateTime alive, start;
            bool hasAlive = Ini.TryStamp(Ini.Str(d, "last_alive", ""), out alive);
            if (hasAlive && Ini.TryStamp(Ini.Str(d, "session_start", ""), out start) && start <= now)
            {
                SessionStart = start;
                Studying = true;
                if ((now - alive).TotalMinutes <= 3 && alive >= start)
                {
                    // The app was only gone for a moment (restarted). Carry on with the session.
                    double elapsed = (now - start).TotalMinutes;
                    lastAnnounced = (int)Math.Floor(elapsed);
                    nextBreakMin = S.BreakEvery > 0 ? (Math.Floor(elapsed / S.BreakEvery) + 1) * S.BreakEvery : double.MaxValue;
                    Speak("resumed", false);
                }
                else
                {
                    // It crashed, was killed, or the PC lost power. Log the session up to the last sign of life.
                    DateTime end = alive < start ? start : alive;
                    if (end > now) end = now;
                    StopSession(end, StopReason.Recovered);
                }
            }

            DateTime stateDay, greetDay;
            if (hasAlive && Ini.TryDay(Ini.Str(d, "day", ""), out stateDay) && stateDay == day && !Studying)
            {
                OffTaskSec = Ini.Int(d, "offtask_sec", 0, 0, 3 * 86400);
                nextNagSec = Math.Max(Ini.Int(d, "next_nag_sec", (int)nextNagSec, 0, int.MaxValue), OffTaskSec + 60);
            }
            if (Ini.TryDay(Ini.Str(d, "greeted", ""), out greetDay)) greeted = greetDay;
            TrayHintShown = Ini.Bool(d, "tray_hint", false);
        }

        void SaveState(DateTime now)
        {
            lastSaved = now;
            StringBuilder b = new StringBuilder();
            b.AppendLine("# Study Monitor's own bookkeeping. No need to edit this.");
            if (Studying) b.AppendLine("session_start=" + Ini.Stamp(SessionStart));
            b.AppendLine("last_alive=" + Ini.Stamp(now));
            b.AppendLine("day=" + Ini.Day(day));
            b.AppendLine("offtask_sec=" + ((int)OffTaskSec).ToString(CultureInfo.InvariantCulture));
            b.AppendLine("next_nag_sec=" + ((int)Math.Min(nextNagSec, int.MaxValue)).ToString(CultureInfo.InvariantCulture));
            if (greeted != DateTime.MinValue) b.AppendLine("greeted=" + Ini.Day(greeted));
            if (TrayHintShown) b.AppendLine("tray_hint=true");
            foreach (Session s in History.Unsaved)
            {
                b.AppendLine("pending=" + Ini.Stamp(s.Start) + "|" + Ini.Stamp(s.End) + "|" + s.Goal.ToString(CultureInfo.InvariantCulture));
            }
            try { Ini.WriteAtomic(statePath, b.ToString()); }
            catch { }
        }
    }
}
