using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace StudyMonitor
{
    class Session
    {
        public DateTime Start;
        public DateTime End;
        public int Goal;   // the daily goal at the time, so changing your goal later doesn't rewrite old streaks

        public double Minutes { get { return (End - Start).TotalMinutes; } }
    }

    // All study sessions, kept in data\sessions.csv (opens in Excel).
    class History
    {
        const string Header = "day,start,end,minutes,goal";

        readonly string path;
        int newDayHour;
        readonly List<Session> sessions = new List<Session>();
        readonly List<Session> unsaved = new List<Session>();
        readonly Dictionary<DateTime, double> totals = new Dictionary<DateTime, double>();
        readonly Dictionary<DateTime, int> goals = new Dictionary<DateTime, int>();
        DateTime firstDay = DateTime.MaxValue;

        public History(string path, int newDayHour)
        {
            this.path = path;
            this.newDayHour = newDayHour;
            Load();
        }

        // Sessions that couldn't be written yet (for example while Excel had the file locked).
        public List<Session> Unsaved { get { return unsaved; } }

        // The study day a moment belongs to. With the day starting at 4 AM, 1 AM still counts as yesterday.
        public DateTime DayOf(DateTime t)
        {
            return t.AddHours(-newDayHour).Date;
        }

        public void SetNewDayHour(int hour)
        {
            if (hour == newDayHour) return;
            newDayHour = hour;
            Rebuild();
        }

        void Load()
        {
            if (!File.Exists(path)) return;
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return; }
            foreach (string raw in lines)
            {
                string[] parts = raw.Split(new char[] { ',', ';' });
                if (parts.Length < 3) continue;
                Session s = new Session();
                if (!Ini.TryStamp(parts[1].Trim(), out s.Start) || !Ini.TryStamp(parts[2].Trim(), out s.End)) continue;
                if (s.End < s.Start) continue;
                int goal = 0;
                if (parts.Length >= 5) int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out goal);
                s.Goal = goal;
                sessions.Add(s);
            }
            Rebuild();
        }

        void Rebuild()
        {
            totals.Clear();
            goals.Clear();
            firstDay = DateTime.MaxValue;
            foreach (Session s in sessions) Index(s);
        }

        void Index(Session s)
        {
            DateTime d = DayOf(s.Start);
            double t;
            totals.TryGetValue(d, out t);
            totals[d] = t + s.Minutes;
            if (s.Goal > 0) goals[d] = s.Goal;
            if (d < firstDay) firstDay = d;
        }

        public bool Has(DateTime start)
        {
            foreach (Session s in sessions) if (s.Start == start) return true;
            return false;
        }

        // Records a session. Returns false if the file couldn't be written (it will be retried).
        public bool Add(Session s)
        {
            sessions.Add(s);
            Index(s);
            unsaved.Add(s);
            return Flush();
        }

        public bool Flush()
        {
            if (unsaved.Count == 0) return true;
            try
            {
                StringBuilder b = new StringBuilder();
                if (!File.Exists(path)) b.Append(Header).Append("\r\n");
                foreach (Session s in unsaved)
                {
                    b.Append(Ini.Day(DayOf(s.Start))).Append(',')
                     .Append(Ini.Stamp(s.Start)).Append(',')
                     .Append(Ini.Stamp(s.End)).Append(',')
                     .Append(((int)Math.Round(s.Minutes)).ToString(CultureInfo.InvariantCulture)).Append(',')
                     .Append(s.Goal.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                }
                File.AppendAllText(path, b.ToString());
                unsaved.Clear();
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public double Total(DateTime day)
        {
            double t;
            return totals.TryGetValue(day, out t) ? t : 0;
        }

        public int GoalOn(DateTime day, int fallback)
        {
            int g;
            return goals.TryGetValue(day, out g) ? g : fallback;
        }

        public List<Session> On(DateTime day)
        {
            List<Session> list = new List<Session>();
            foreach (Session s in sessions) if (DayOf(s.Start) == day) list.Add(s);
            return list;
        }

        // Days in a row the goal was hit. Today only counts once it's done; until then the streak is still alive.
        public int Streak(DateTime today, double doneToday, int goalNow)
        {
            int streak = goalNow > 0 && doneToday >= goalNow ? 1 : 0;
            DateTime d = today.AddDays(-1);
            while (d >= firstDay && streak < 100000)
            {
                int goal = GoalOn(d, goalNow);
                if (goal <= 0 || Total(d) < goal) break;
                streak++;
                d = d.AddDays(-1);
            }
            return streak;
        }

        // Average minutes per day over the last N days before today (only counting days since you started).
        public double? Average(DateTime today, int days)
        {
            if (firstDay == DateTime.MaxValue) return null;
            double sum = 0;
            int n = 0;
            for (int i = 1; i <= days; i++)
            {
                DateTime d = today.AddDays(-i);
                if (d < firstDay) break;
                sum += Total(d);
                n++;
            }
            if (n == 0) return null;
            return sum / n;
        }
    }
}
