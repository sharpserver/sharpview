namespace Browser
{
    using System;
    using System.Drawing;
    using System.Threading;
    using System.Windows.Forms;
    using System.ComponentModel;
    using System.Collections.Generic;

    using Xilium.CefGlue;
    using Xilium.CefGlue.GUI;
    using MenuItemImpl = System.Windows.Forms.MenuItem;

    internal sealed class MainViewImpl : Form, IMainView
    {
        private string sPath = "";
        private CefWebBrowser browserCtl;
        private readonly DemoApp _application;
        private readonly string _applicationTitle;

        private readonly SynchronizationContext _pUIThread;

        public MainViewImpl(DemoApp application, CefColor cColor)
        {
            _pUIThread = WindowsFormsSynchronizationContext.Current;

            _application = application;
            _applicationTitle = _application.Name;

            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainViewImpl));

            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            BackColor = Color.FromArgb(cColor.A, cColor.R, cColor.G, cColor.B);

            Text = _applicationTitle;
            Size = new Size(1024, 768);

            string sFile = "http://google.com";

            var state = new WebBrowserState();

            browserCtl = new CefWebBrowser();
            
            browserCtl.Parent = this;
            browserCtl.Dock = DockStyle.Fill;
            browserCtl.BringToFront();
            browserCtl.BackColor = this.BackColor;

            var browser = browserCtl.WebBrowser;
            browser.StartUrl = sFile;

            browser.TitleChanged += (s, e) =>
                {
                    state.Title = e.Title;
                    _pUIThread.Post((_state) => { UpdateTitle(e.Title); }, null);
                };

            browser.AddressChanged += (s, e) =>
                {
                    state.Title = e.Address;

                    _pUIThread.Post((_state) =>
                    {
                        //navBox.Address = e.Address; 
                    }, null);
                };

            browser.TargetUrlChanged += (s, e) =>
                {
                    state.TargetUrl = e.TargetUrl;
                    // TODO: show targeturl in status bar
                    // _pUIThread.Post((_state) => { UpdateTargetUrl(e.TargetUrl); }, null);
                };

            browser.LoadingStateChanged += (s, e) =>
                {
                    _pUIThread.Post((_state) =>
                        {
                            //navBox.CanGoBack = e.CanGoBack;
                            //navBox.CanGoForward = e.CanGoForward;
                            
                            if (!e.Loading)
                            {
                            }

                        }, null);
                };

            this.Controls.Add(browserCtl);
            Visible = true;

        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (browserCtl != null)
            {
                browserCtl.Dispose();
            }           

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _application.Quit();
        }

        private void UpdateTitle(string title)
        {
            if (title == "about:blank") return;
            Text = string.IsNullOrEmpty(title) ? _applicationTitle : title;
        }

        public void NavigateTo(string url)
        {
            if (browserCtl != null)
            {
                CurrentBrowser.StopLoad();
                CurrentBrowser.GetMainFrame().LoadUrl(url);
            }
        }

        public CefBrowser CurrentBrowser
        {
            get
            {
                return browserCtl.WebBrowser.CefBrowser;
            }
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainViewImpl));
            this.SuspendLayout();
            // 
            // MainViewImpl
            // 
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.ClientSize = new System.Drawing.Size(292, 273);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainViewImpl";
            this.ResumeLayout(false);
        }
    }
}
