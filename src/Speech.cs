using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace StudyMonitor
{
    // The text-to-speech built into Windows (SAPI), used through COM so no extra DLLs are needed.
    class Speech
    {
        const int Async = 1, PurgeBeforeSpeak = 2, IsNotXml = 16;
        const int IsSpeakingState = 2;

        // English language codes, US first: US, UK, Australia, Canada, India, Ireland, New Zealand, South Africa
        static readonly string[] English = { "409", "809", "c09", "1009", "4009", "1809", "1409", "1c09" };

        object voice;
        string wanted = "";
        int volume = 100, rate = 0;

        public Speech()
        {
            voice = Create();
        }

        public bool Available { get { return voice != null; } }

        public List<string> VoiceNames()
        {
            List<string> names = new List<string>();
            if (voice == null) return names;
            try
            {
                object tokens = Call(voice, "GetVoices", "", "");
                int n = Convert.ToInt32(Get(tokens, "Count"));
                for (int i = 0; i < n; i++) names.Add(Describe(Call(tokens, "Item", i)));
            }
            catch
            {
            }
            return names;
        }

        // Uses the named voice, or else the first female English voice (Zira on most PCs),
        // or else any female voice. Returns the name of the voice now in use.
        public string Use(string name)
        {
            wanted = name ?? "";
            return voice == null ? "" : PickVoice(voice);
        }

        public void Configure(int newVolume, int newRate)
        {
            volume = Math.Max(0, Math.Min(100, newVolume));
            rate = Math.Max(-10, Math.Min(10, newRate));
            if (voice != null) ApplyLevels(voice);
        }

        // Speaks in the background. interrupt = cut off whatever is being said right now.
        public void Speak(string text, bool interrupt)
        {
            if (voice == null || string.IsNullOrEmpty(text)) return;
            try
            {
                if (!IsSpeaking()) Renew();
                Call(voice, "Speak", text, Async | IsNotXml | (interrupt ? PurgeBeforeSpeak : 0));
            }
            catch
            {
            }
        }

        // A fresh voice object for each new line follows the current audio device,
        // e.g. earbuds connected after the app started.
        void Renew()
        {
            object fresh = Create();
            if (fresh == null) return;
            PickVoice(fresh);
            ApplyLevels(fresh);
            object old = voice;
            voice = fresh;
            try { Marshal.FinalReleaseComObject(old); }
            catch { }
        }

        bool IsSpeaking()
        {
            try { return Convert.ToInt32(Get(Get(voice, "Status"), "RunningState")) == IsSpeakingState; }
            catch { return false; }
        }

        static object Create()
        {
            try
            {
                Type t = Type.GetTypeFromProgID("SAPI.SpVoice");
                return t == null ? null : Activator.CreateInstance(t);
            }
            catch
            {
                return null;
            }
        }

        string PickVoice(object target)
        {
            try
            {
                object token = null;
                if (wanted.Length > 0) token = Find(target, wanted);
                if (token == null)
                {
                    foreach (string lang in English)
                    {
                        token = First(target, "Gender=Female;Language=" + lang);
                        if (token != null) break;
                    }
                }
                if (token == null) token = First(target, "Gender=Female");
                if (token != null) SetRef(target, "Voice", token);
                return Describe(Get(target, "Voice"));
            }
            catch
            {
                return "";
            }
        }

        void ApplyLevels(object target)
        {
            try
            {
                Set(target, "Volume", volume);
                Set(target, "Rate", rate);
            }
            catch
            {
            }
        }

        static object First(object target, string requiredAttributes)
        {
            object tokens = Call(target, "GetVoices", requiredAttributes, "");
            int n = Convert.ToInt32(Get(tokens, "Count"));
            return n > 0 ? Call(tokens, "Item", 0) : null;
        }

        static object Find(object target, string name)
        {
            object tokens = Call(target, "GetVoices", "", "");
            int n = Convert.ToInt32(Get(tokens, "Count"));
            for (int i = 0; i < n; i++)
            {
                object token = Call(tokens, "Item", i);
                if (string.Equals(Describe(token), name, StringComparison.OrdinalIgnoreCase)) return token;
            }
            return null;
        }

        static string Describe(object token)
        {
            return token == null ? "" : Convert.ToString(Call(token, "GetDescription", 0));
        }

        static object Call(object target, string name, params object[] args)
        {
            return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);
        }

        static object Get(object target, string name)
        {
            return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
        }

        static void Set(object target, string name, object value)
        {
            target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, new object[] { value });
        }

        static void SetRef(object target, string name, object value)
        {
            target.GetType().InvokeMember(name, BindingFlags.PutRefDispProperty, null, target, new object[] { value });
        }
    }
}
