namespace Xilium.CefGlue.GUI
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Diagnostics;
    using System.Collections.Generic;

    using Xilium.CefGlue.Wrapper;

    public abstract class DemoApp : IDisposable
    {
        public string Name { get { return "Sharpview"; } }

        public static CefMessageRouterBrowserSide BrowserMessageRouter { get; private set; }

        private const string DumpRequestDomain = "dump-request.demoapp.cefglue.xilium.local";

        private IMainView _mainView;

        protected DemoApp()
        {
        }

        #region IDisposable

        ~DemoApp()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        #endregion
        
        protected IMainView MainView { get { return _mainView; } }

        public int Run(string ProcessPath, string CachePath, string LocalesPath, string LogFile, CefColor cColor)
        {
            try
            {
                return RunInternal(ProcessPath, CachePath, LocalesPath, LogFile, cColor);
            }
            catch (Exception ex)
            {
                PlatformMessageBox(ex.ToString());
                return 1;
            }
        }

        protected bool MultiThreadedMessageLoop { get; private set; }

        private int RunInternal(string ProcessPath, string CachePath, string LocalesPath, string LogFile, CefColor cColor)
        {
            CefRuntime.Load();

            var settings = new CefSettings();
            
            settings.NoSandbox = true;
            settings.SingleProcess = false;
            settings.RemoteDebuggingPort = 0;

            settings.PackLoadingDisabled = false;
            settings.PersistSessionCookies = true;
            settings.IgnoreCertificateErrors = true;
            settings.WindowlessRenderingEnabled = false;

            settings.Locale = "en-US";
            settings.CachePath = CachePath;
            settings.LocalesDirPath = LocalesPath;
            settings.BackgroundColor = cColor;
            settings.BrowserSubprocessPath = ProcessPath;

            settings.MultiThreadedMessageLoop = MultiThreadedMessageLoop = CefRuntime.Platform == CefRuntimePlatform.Windows;
            
            settings.LogFile = LogFile;

#if DEBUG
            settings.LogSeverity = CefLogSeverity.Verbose;
#else
            settings.LogSeverity = CefLogSeverity.Default;
#endif       
            string[] args = new string[1];

            args[0] = "--no-proxy-server";

            var mainArgs = new CefMainArgs(args);
            var app = new DemoCefApp();

            var exitCode = CefRuntime.ExecuteProcess(mainArgs, app, IntPtr.Zero);
            Console.WriteLine("CefRuntime.ExecuteProcess() returns {0}", exitCode);
            
            if (exitCode != -1) return exitCode;

            // guard if something wrong
            foreach (var arg in args) { if (arg != null && arg.StartsWith("--type=")) { return -2; } }

            CefRuntime.Initialize(mainArgs, settings, app, IntPtr.Zero);

            RegisterSchemes();
            RegisterMessageRouter();

            PlatformInitialize();

            _mainView = CreateMainView(cColor);

            PlatformRunMessageLoop();

            _mainView.Dispose();
            _mainView = null;

            CefRuntime.Shutdown();

            PlatformShutdown();
            return 0;
        }

        public void Quit()
        {
            PlatformQuitMessageLoop();
        }

        protected abstract void PlatformInitialize();

        protected abstract void PlatformShutdown();

        protected abstract void PlatformRunMessageLoop();

        protected abstract void PlatformQuitMessageLoop();

        protected abstract IMainView CreateMainView(CefColor cColor);

        protected abstract void PlatformMessageBox(string message);

        #region Commands

        private void SchemeHandlerDumpRequestCommand(object sender, EventArgs e)
        {
            MainView.NavigateTo("http://" + DumpRequestDomain);
        }

        private void SendProcessMessageCommand(object sender, EventArgs e)
        {
            var browser = MainView.CurrentBrowser;
            if (browser != null)
            {
                var message = CefProcessMessage.Create("myMessage1");
                var arguments = message.Arguments;
                arguments.SetString(0, "hello");
                arguments.SetInt(1, 12345);
                arguments.SetDouble(2, 12345.6789);
                arguments.SetBool(3, true);

                browser.SendProcessMessage(CefProcessId.Renderer, message);
            }
        }

        private void PopupWindowCommand(object sender, EventArgs e)
        {
            var url = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(new Uri(typeof(DemoApp).Assembly.CodeBase).LocalPath), "transparency.html");
            //MainView.NewWebView(url, false);
        }

        private void TransparentPopupWindowCommand(object sender, EventArgs e)
        {
            var url = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(new Uri(typeof(DemoApp).Assembly.CodeBase).LocalPath), "transparency.html");
            //MainView.NewWebView(url, true);
        }

        private void OpenDeveloperToolsCommand(object sender, EventArgs e)
        {
            var host = MainView.CurrentBrowser.GetHost();
            var wi = CefWindowInfo.Create();
            wi.SetAsPopup(IntPtr.Zero, "DevTools");
            host.ShowDevTools(wi, new DevToolsWebClient(), new CefBrowserSettings());
            // Process.Start(devToolsUrl);
        }

        private class DevToolsWebClient : CefClient
        {
        }

        private void SendKeyEventCommand(object sender, EventArgs e)
        {
            var host = MainView.CurrentBrowser.GetHost();

            foreach (var c in "This text typed with CefBrowserHost.SendKeyEvent method!")
            {
                // little hacky
                host.SendKeyEvent(new CefKeyEvent
                    {
                        EventType = CefKeyEventType.Char,
                        Modifiers= CefEventFlags.None,
                        WindowsKeyCode = c,
                        NativeKeyCode = c,
                        Character = c,
                        UnmodifiedCharacter = c,
                    });
            }
        }

        #endregion

        private void RegisterSchemes()
        {
            // register custom scheme handler
            CefRuntime.RegisterSchemeHandlerFactory("http", DumpRequestDomain, new DemoAppSchemeHandlerFactory());
            // CefRuntime.AddCrossOriginWhitelistEntry("http://localhost", "http", "", true);

        }

        private void RegisterMessageRouter()
        {
            if (!CefRuntime.CurrentlyOn(CefThreadId.UI))
            {
                PostTask(CefThreadId.UI, this.RegisterMessageRouter);
                return;
            }

            // window.cefQuery({ request: 'my_request', onSuccess: function(response) { console.log(response); }, onFailure: function(err,msg) { console.log(err, msg); } });
            DemoApp.BrowserMessageRouter = new CefMessageRouterBrowserSide(new CefMessageRouterConfig());
            DemoApp.BrowserMessageRouter.AddHandler(new DemoMessageRouterHandler());
        }

        private class DemoMessageRouterHandler : CefMessageRouterBrowserSide.Handler
        {
            public override bool OnQuery(CefBrowser browser, CefFrame frame, long queryId, string request, bool persistent, CefMessageRouterBrowserSide.Callback callback)
            {
                if (request == "wait5")
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(5000);
                        callback.Success("success! responded after 5 sec timeout."); // TODO: at this place crash can occurs, if application closed
                    }).Start();
                    return true;
                }

                if (request == "wait5f")
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(5000);
                        callback.Failure(12345, "success! responded after 5 sec timeout. responded as failure.");
                    }).Start();
                    return true;
                }

                if (request == "wait30")
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(30000);
                        callback.Success("success! responded after 30 sec timeout.");
                    }).Start();
                    return true;
                }

                if (request == "noanswer")
                {
                    return true;
                }

                var chars = request.ToCharArray();
                Array.Reverse(chars);
                var response = new string(chars);
                callback.Success(response);
                return true;
            }

            public override void OnQueryCanceled(CefBrowser browser, CefFrame frame, long queryId)
            {
            }
        }

        public static void PostTask(CefThreadId threadId, Action action)
        {
            CefRuntime.PostTask(threadId, new ActionTask(action));
        }

        internal sealed class ActionTask : CefTask
        {
            public Action _action;

            public ActionTask(Action action)
            {
                _action = action;
            }

            protected override void Execute()
            {
                _action();
                _action = null;
            }
        }

        public delegate void Action();
    }
}
