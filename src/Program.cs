using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace StudyMonitor
{
    static class Program
    {
        public static string DataDir;

        [STAThread]
        static void Main(string[] args)
        {
            bool startHidden = false;
            foreach (string a in args)
            {
                if (a.Equals("/tray", StringComparison.OrdinalIgnoreCase) || a.Equals("--tray", StringComparison.OrdinalIgnoreCase)) startHidden = true;
            }

            bool firstInstance;
            using (Mutex mutex = new Mutex(true, @"Local\StudyMonitor.SingleInstance", out firstInstance))
            {
                if (!firstInstance)
                {
                    // Already running (probably hidden in the tray): ask it to show its window.
                    Native.PostMessage(Native.HWND_BROADCAST, MainForm.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                try { Native.SetProcessDPIAware(); }
                catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                DataDir = FindDataDir();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { LogError(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { LogError(e.ExceptionObject as Exception); };

                Application.Run(new MainForm(DataDir, startHidden));
            }
        }

        // Your files live in %APPDATA%\StudyMonitor, so the exe can be moved or replaced without losing anything.
        // Portable mode: if there's a "data" folder next to the exe (earlier versions made one), that's used instead.
        static string FindDataDir()
        {
            string portable = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "data");
            if (Directory.Exists(portable) && CanWrite(portable)) return portable;
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StudyMonitor");
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch
            {
                Directory.CreateDirectory(portable);
                return portable;
            }
        }

        static bool CanWrite(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Unexpected errors go to data\errors.log instead of crashing the app.
        public static void LogError(Exception ex)
        {
            if (ex == null) return;
            try
            {
                File.AppendAllText(Path.Combine(DataDir ?? Path.GetTempPath(), "errors.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
