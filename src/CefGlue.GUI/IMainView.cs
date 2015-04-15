namespace Xilium.CefGlue.GUI
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public interface IMainView : IDisposable
    {
        void Close();
        void NavigateTo(string url);

        CefBrowser CurrentBrowser { get; }
    }
}
