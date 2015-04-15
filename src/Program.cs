using System;
using System.IO;
using System.Linq;
using System.IO.Pipes;
using System.Threading;
using System.ComponentModel;
using System.Windows.Forms;
using System.Collections.Generic;

using Xilium.CefGlue;
using System.Reflection;

namespace Browser
{
    static public class Program
    {
        private static Mutex Mutex = new Mutex(true, "Global\\Sharpview");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static int Main()
        {
            string[] Args = Environment.GetCommandLineArgs();

            if (!Mutex.WaitOne(TimeSpan.Zero, true))
            {
                if (Stop(Args)) return 0;
            }

            if (Mutex.WaitOne(TimeSpan.Zero, true))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                return Start(Args);
            }

            Stop(Args);
            return 0;
        }

        private static int Start(string[] Args)
        {
            Daemon DD = new Daemon();

            if (!DD.HandleCmd(Args)) return 0;

            string path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            path = Path.Combine(path, "Sharpview");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            string LogDir = path + "\\Logs";
            if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);

            string LogFile = LogDir + "\\Browser.log";

            string CacheDir = path + "\\Cache";
            string LocalesDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Locales";

            if (!Directory.Exists(CacheDir)) Directory.CreateDirectory(CacheDir);

            CefColor cColor = new CefColor(255, 249, 249, 249);

            string ProcessPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Browser.dll";

            return DD.Run(ProcessPath, CacheDir, LocalesDir, LogFile, cColor);           
        }

        private static bool Stop(string[] Args)
        {
            try
            {
                NamedPipeClientStream client = new NamedPipeClientStream(Application.ProductName);

                client.Connect(1000);

                using (var writer = new BinaryWriter(client))
                {
                    writer.Write(String.Join("\t", Args));
                    writer.Flush();
                    writer.Close();
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private static UInt32 Timestamp
        {
            get
            {
                TimeSpan Delta = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
                return (UInt32)Delta.TotalSeconds;
            }
        }

    }

    public static class UI
    {
        public static DialogResult Box(IWin32Window owner, string text, string caption = "Sharpview", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Error)
        {
            return MessageBox.Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button3, (MessageBoxOptions)0x40000);  // MB_TOPMOST
        }
    }
}
