using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StudyMonitor
{
    // "Ctrl+Alt+S" -> the numbers RegisterHotKey wants.
    static class Hotkeys
    {
        public static bool TryParse(string text, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) return false;
                if (i < parts.Length - 1)
                {
                    string m = p.ToLowerInvariant();
                    if (m == "ctrl" || m == "control") modifiers |= Native.MOD_CONTROL;
                    else if (m == "alt") modifiers |= Native.MOD_ALT;
                    else if (m == "shift") modifiers |= Native.MOD_SHIFT;
                    else if (m == "win" || m == "windows") modifiers |= Native.MOD_WIN;
                    else return false;
                }
                else
                {
                    if (p.Length == 1 && char.IsDigit(p[0])) p = "D" + p;
                    Keys k;
                    if (!Enum.TryParse(p, true, out k)) return false;
                    if ((int)k <= 0 || (int)k > 0xFE) return false;
                    key = (uint)k;
                }
            }
            return key != 0 && modifiers != 0;
        }
    }

    // Starting with Windows, via the normal per-user "Run" registry entry (no admin needed).
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "StudyMonitor";

        public static void Apply(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enabled) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" /tray");
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                }
            }
            catch
            {
            }
        }
    }
}
