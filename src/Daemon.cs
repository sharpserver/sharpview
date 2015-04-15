namespace Browser
{
    using System;
    using System.IO;
    using System.Text;
    using System.Reflection;
    using System.Windows.Forms;
    using System.Collections.Generic;

    using Xilium.CefGlue;
    using Xilium.CefGlue.GUI;
    using System.Threading;
    using System.IO.Pipes;

    internal sealed class Daemon : DemoApp
    {
        internal Thread readerThread;
        internal static MainViewImpl Main;

        protected override void PlatformInitialize()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }

        protected override void PlatformShutdown()
        {
        }

        protected override void PlatformRunMessageLoop()
        {
            if (!MultiThreadedMessageLoop)
            {
                Application.Idle += (s, e) => CefRuntime.DoMessageLoopWork();
            }

            Application.Run();
        }

        protected override void PlatformQuitMessageLoop()
        {
            if (readerThread != null)
            {
                try
                {
                    readerThread.Abort();
                    readerThread = null;
                }
                catch { readerThread = null; }
            }

            Application.Exit();
        }

        protected override IMainView CreateMainView(CefColor cColor)
        {
            Main = new MainViewImpl(this, cColor);

            readerThread = new Thread(new ThreadStart(ReaderThreadLoop));

            readerThread.IsBackground = true;
            readerThread.SetApartmentState(ApartmentState.STA);
            readerThread.Start();

            return Main;
        }

        protected override void PlatformMessageBox(string message)
        {
            UI.Box(null, message);
        }

        static string GetArgs(string[] Args)
        {
            if (Args.Length < 2) return "";
            return Args[1].ToLower().TrimEnd();
        }

        public bool HandleCmd(string[] Args)
        {
            if (Args.Length < 2) return true;

            string sCommand = GetArgs(Args);

            if (sCommand == "/quit")
            {
                if (Main != null)
                {
                    Main.Close();
                    Main = null;
                }

                Application.Exit();
                return false;
            }

            return true;
        }

        void ReaderThreadLoop()
        {
            try
            {
                while ((Thread.CurrentThread.ThreadState == ThreadState.Background) || (Thread.CurrentThread.ThreadState == ThreadState.Running))
                {
                    NamedPipeServerStream server = new NamedPipeServerStream(Application.ProductName);

                    while (true)
                    {
                        try
                        {
                            server.WaitForConnection();
                            break;
                        }
                        catch (IOException)
                        {
                            server.Disconnect();
                            continue;
                        }
                    }

                    using (var reader = new BinaryReader(server))
                    {
                        string[] Args = reader.ReadString().Split('\t');

                        if (!HandleCmd(Args)) return;

                        if (Main != null)
                        {
                            Main.WindowState = FormWindowState.Minimized;
                            Main.Show();
                            Main.WindowState = FormWindowState.Maximized;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("ReaderThread: " + ex.Message);
            }
        }
    }
}
