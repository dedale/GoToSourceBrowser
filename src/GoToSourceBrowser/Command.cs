using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace GoToSourceBrowser
{
    internal sealed class Command
    {
        public static readonly Guid GroupGuid = new Guid("8C8FAD7E-477F-41FD-AFA4-A9762680C226");
        public CommandID Id => new CommandID(GroupGuid, 0x2001);

        private readonly IVisualStudio visualStudio;
        private readonly IWebBrowser webBrowser;
        private readonly Func<Uri> getUri;

        private static string GetName(CodeElementInfo codeElement)
        {
            // Redirects ctor to class
            if (codeElement.Kind == vsCMElement.vsCMElementFunction
                && codeElement.FullName.EndsWith($"{codeElement.Name}.{codeElement.Name}", StringComparison.Ordinal))
                return codeElement.FullName.TrimSuffix($".{codeElement.Name}");

            return codeElement.FullName;
        }

        public Command(IVisualStudio visualStudio, IWebBrowser webBrowser, Func<Uri> getUri)
        {
            this.visualStudio = visualStudio;
            this.webBrowser = webBrowser;
            this.getUri = getUri;
        }

        public bool Enabled
        {
            get
            {
                if (getUri() == null)
                {
                    Log.Verbose("Command disabled (no Source Browser URI).");
                    return false;
                }
                var enabled = visualStudio.GetCurrentCodeElement() != null;
                Log.Verbose(enabled
                    ? "Command enabled (found code element at cursor)."
                    : "Command disabled (no current code element).");
                return enabled;
            }
        }

        public Task RunAsync()
        {
            var codeElement = visualStudio.GetCurrentCodeElement();
            var uri = new Uri($"{getUri()}#q={GetName(codeElement)}");
            webBrowser.Open(uri);
            return Task.CompletedTask;
        }
    }

    internal sealed class SourceBrowserUri
    {
        private readonly IGit git;
        private readonly Dictionary<string, Uri> knownSites = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler Updated;

        public SourceBrowserUri(IGit git)
        {
            this.git = git;

            OptionPage.OptionUpdated += OptionUpdated;
        }

        public void UpdateSites(string setting)
        {
            knownSites.Clear();
            setting
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Parse)
                .Where(x => x.Repo != null)
                .ToList()
                .ForEach(x => knownSites.Add(x.Repo, x.Site));
            Updated?.Invoke(this, new EventArgs());

            (string Repo, Uri Site) Parse(string repoSite)
            {
                var parts = repoSite.Split('=');
                if (parts.Length == 2)
                {
                    var repo = parts[0];
                    var site = parts[1];
                    if (Regex.IsMatch(repo, @"^[0-9a-zA-Z_\-]+$") && Uri.IsWellFormedUriString(site, UriKind.Absolute))
                        return (repo, new Uri(site));
                }
                return (null, null);
            }
        }

        private void OptionUpdated(object sender, EventArgs e)
        {
            Log.Debug($"{nameof(OptionUpdated)}...");
            if (sender is OptionPage optionPage)
                UpdateSites(optionPage.Sites);
        }

        public void HandleSolution(string solutionPath)
        {
            var repoName = git.GetRemoteRepositories(solutionPath).FirstOrDefault();
            if (repoName != null)
            {
                Log.Debug($"Using '{repoName}' repository name.");
                Value = knownSites.TryGetValue(repoName, out var site) ? site : null;
            }
            else
            {
                Log.Debug("Could not guess repo name from git remotes.");
                Value = null;
            }
            Log.Information($"Source Browser URI is {(Value == null ? "undefined" : Value.AbsoluteUri)}.");
        }

        public Uri Value { get; private set; }
    }
}
