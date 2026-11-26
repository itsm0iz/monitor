using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace StudyMonitor
{
    // Turning numbers into words the voice can say, and short text for the window.
    static class Fmt
    {
        // "45 minutes", "1 hour", "2 hours 5 minutes"
        public static string Dur(double minutes)
        {
            int m = (int)Math.Round(minutes);
            if (m < 1) return "less than a minute";
            int h = m / 60;
            m = m % 60;
            if (h == 0) return Plural(m, "minute");
            if (m == 0) return Plural(h, "hour");
            return Plural(h, "hour") + " " + Plural(m, "minute");
        }

        // Like Dur, but big amounts are rounded to whole hours: "15 hours"
        public static string Big(double minutes)
        {
            if (minutes < 120) return Dur(minutes);
            return Plural((int)Math.Round(minutes / 60.0), "hour");
        }

        public static string Plural(int n, string word)
        {
            return n + " " + word + (n == 1 ? "" : "s");
        }

        // "1h 20m", "45m", "3h" (rounded down, so 2h 59m never shows as 3h)
        public static string Short(double minutes)
        {
            int m = (int)Math.Floor(minutes + 0.0001);
            if (m < 0) m = 0;
            int h = m / 60;
            m = m % 60;
            if (h == 0) return m + "m";
            if (m == 0) return h + "h";
            return h + "h " + m + "m";
        }

        // "1:05:09"
        public static string Clock(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
        }

        // "1:05"
        public static string HoursMinutes(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", (int)t.TotalHours, t.Minutes);
        }

        // "40 percent"
        public static string Percent(double fraction)
        {
            return (int)Math.Round(fraction * 100) + " percent";
        }

        // Minutes after midnight to words: "midnight", "noon", "11 PM", "1:30 AM"
        public static string TimeOfDay(int minutesAfterMidnight)
        {
            int mins = ((minutesAfterMidnight % 1440) + 1440) % 1440;
            if (mins == 0) return "midnight";
            if (mins == 720) return "noon";
            int h = mins / 60;
            int m = mins % 60;
            string ampm = h < 12 ? "AM" : "PM";
            int h12 = h % 12;
            if (h12 == 0) h12 = 12;
            if (m == 0) return h12 + " " + ampm;
            return h12 + ":" + m.ToString("00", CultureInfo.InvariantCulture) + " " + ampm;
        }

        public static string TimeOfDay(DateTime t)
        {
            return TimeOfDay(t.Hour * 60 + t.Minute);
        }

        // "09:00" for the settings file
        public static string Hm(int minutesAfterMidnight)
        {
            int mins = ((minutesAfterMidnight % 1440) + 1440) % 1440;
            return (mins / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (mins % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        public static bool TryParseHm(string text, out int minutes)
        {
            minutes = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Trim().Split(':');
            int h, m;
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out h)) return false;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;
            minutes = h * 60 + m;
            return true;
        }
    }

    // Reading and writing the simple key=value files (settings.ini, state.ini).
    static class Ini
    {
        public const string StampFormat = "yyyy-MM-dd HH:mm:ss";
        public const string DayFormat = "yyyy-MM-dd";

        public static List<KeyValuePair<string, string>> ReadPairs(string path)
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            if (!File.Exists(path)) return pairs;
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return pairs; }
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                pairs.Add(new KeyValuePair<string, string>(line.Substring(0, eq).Trim().ToLowerInvariant(), line.Substring(eq + 1).Trim()));
            }
            return pairs;
        }

        public static Dictionary<string, string> ToDict(List<KeyValuePair<string, string>> pairs)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> p in pairs) d[p.Key] = p.Value;
            return d;
        }

        public static int Int(Dictionary<string, string> d, string key, int fallback, int min, int max)
        {
            string s;
            int v;
            if (!d.TryGetValue(key, out s) || !int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return fallback;
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static bool Bool(Dictionary<string, string> d, string key, bool fallback)
        {
            string s;
            if (!d.TryGetValue(key, out s)) return fallback;
            s = s.ToLowerInvariant();
            if (s == "true" || s == "yes" || s == "1" || s == "on") return true;
            if (s == "false" || s == "no" || s == "0" || s == "off") return false;
            return fallback;
        }

        public static string Str(Dictionary<string, string> d, string key, string fallback)
        {
            string s;
            return d.TryGetValue(key, out s) ? s : fallback;
        }

        public static int Time(Dictionary<string, string> d, string key, int fallback)
        {
            string s;
            int m;
            if (d.TryGetValue(key, out s) && Fmt.TryParseHm(s, out m)) return m;
            return fallback;
        }

        public static string Stamp(DateTime t)
        {
            return t.ToString(StampFormat, CultureInfo.InvariantCulture);
        }

        public static string Day(DateTime t)
        {
            return t.ToString(DayFormat, CultureInfo.InvariantCulture);
        }

        public static bool TryStamp(string s, out DateTime t)
        {
            if (DateTime.TryParseExact(s, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return true;
            // Excel may have re-saved the file with your local date format.
            return DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out t);
        }

        public static bool TryDay(string s, out DateTime t)
        {
            return DateTime.TryParseExact(s, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out t);
        }

        // Write to a temp file first, so a crash mid-write can't leave a half-written file.
        public static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch
            {
                File.Copy(tmp, path, true);
                File.Delete(tmp);
            }
        }
    }
}
