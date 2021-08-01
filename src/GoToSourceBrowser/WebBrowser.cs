using Serilog;
using System;
using System.Diagnostics;

namespace GoToSourceBrowser
{
    internal interface IWebBrowser
    {
        void Open(Uri uri);
    }

    internal sealed class WebBrowser : IWebBrowser
    {
        public void Open(Uri uri)
        {
            Log.Information($"Opening {uri}");
            Process.Start($"{uri}");
        }
    }
}
