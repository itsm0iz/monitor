using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace StudyMonitor
{
    // The lines it says, grouped by situation. Loaded from data\messages.txt (you can edit it),
    // falling back to the built-in lines for any section the file doesn't have.
    class Messages
    {
        static readonly Regex Placeholder = new Regex(@"\{(\w+)\}");

        readonly string path;
        readonly Random rng;
        readonly Dictionary<string, List<string>> defaults;
        Dictionary<string, List<string>> fromFile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, List<string>> used = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        DateTime loadedStamp = DateTime.MinValue;

        public Messages(string path, Random rng)
        {
            this.path = path;
            this.rng = rng;
            defaults = Parse(DefaultMessages.Text);
            if (!File.Exists(path))
            {
                try { File.WriteAllText(path, DefaultMessages.Text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false)); }
                catch { }
            }
            ReloadIfChanged();
        }

        void ReloadIfChanged()
        {
            try
            {
                if (!File.Exists(path))
                {
                    fromFile.Clear();
                    return;
                }
                DateTime stamp = File.GetLastWriteTimeUtc(path);
                if (stamp == loadedStamp) return;
                fromFile = Parse(File.ReadAllText(path));
                loadedStamp = stamp;
                used.Clear();
            }
            catch
            {
                // file busy (being saved) - try again next time
            }
        }

        public static Dictionary<string, List<string>> Parse(string text)
        {
            Dictionary<string, List<string>> d = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            List<string> current = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    if (!d.TryGetValue(name, out current))
                    {
                        current = new List<string>();
                        d[name] = current;
                    }
                    continue;
                }
                if (current != null) current.Add(line);
            }
            return d;
        }

        // Picks a line for a situation. value(name) returns the text for {name}, or null if it has no value
        // right now; lines using such a placeholder are skipped. Returns null if nothing fits.
        public string Pick(string section, Tone tone, Func<string, string> value)
        {
            ReloadIfChanged();
            List<string> lines;
            if (!fromFile.TryGetValue(section, out lines) || lines.Count == 0)
            {
                if (!defaults.TryGetValue(section, out lines)) return null;
            }

            List<string> soft = new List<string>();
            List<string> harsh = new List<string>();
            foreach (string line in lines)
            {
                if (!CanFill(line, value)) continue;
                if (line.StartsWith("!")) harsh.Add(line);
                else soft.Add(line);
            }

            // Lines starting with ! are the harsh ones.
            List<string> pool;
            if (tone == Tone.Gentle) pool = soft;
            else if (tone == Tone.Brutal) pool = harsh.Count > 0 ? harsh : soft;
            else
            {
                pool = new List<string>(soft);
                pool.AddRange(harsh);
            }
            if (pool.Count == 0) return null;

            // Don't repeat a line until every other line in the section has been used.
            List<string> seen;
            if (!used.TryGetValue(section, out seen))
            {
                seen = new List<string>();
                used[section] = seen;
            }
            List<string> fresh = new List<string>();
            foreach (string line in pool) if (!seen.Contains(line)) fresh.Add(line);
            if (fresh.Count == 0)
            {
                string last = seen.Count > 0 ? seen[seen.Count - 1] : null;
                seen.Clear();
                foreach (string line in pool) if (line != last) fresh.Add(line);
                if (fresh.Count == 0) fresh.AddRange(pool);
            }
            string pick = fresh[rng.Next(fresh.Count)];
            seen.Add(pick);
            return Fill(pick.TrimStart('!').Trim(), value);
        }

        static bool CanFill(string line, Func<string, string> value)
        {
            foreach (Match m in Placeholder.Matches(line))
            {
                if (value(m.Groups[1].Value.ToLowerInvariant()) == null) return false;
            }
            return true;
        }

        static string Fill(string line, Func<string, string> value)
        {
            return Placeholder.Replace(line, delegate(Match m)
            {
                string v = value(m.Groups[1].Value.ToLowerInvariant());
                return v ?? m.Value;
            });
        }
    }
}
