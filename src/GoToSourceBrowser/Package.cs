using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Serilog;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;
using Task = System.Threading.Tasks.Task;

namespace GoToSourceBrowser
{
    // https://docs.microsoft.com/fr-fr/visualstudio/extensibility/creating-an-extension-with-a-vspackage?view=vs-2019
    // https://github.com/madskristensen/SolutionLoadSample

    // Registers installed product
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    // Publish command in menu
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // Needed to find dll
    [ProvideBindingPath]
    [ProvideOptionPage(typeof(OptionPage), "GoToSourceBrowser", "General", 0, 0, true)]
    [Guid(PackageGuidString)]
    public sealed class SourcePackage : AsyncPackage
    {
        private const string PackageGuidString = "68413A97-1DE9-49B3-977A-8A96E82627AE";

        private static DTE dte;
        private static SourceBrowserUri sourceBrowserUri;

        static void CreateLogger()
        {
            var logDir = Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? ".", "GoToSourceBrowser");
            Directory.CreateDirectory(logDir);
            Log.Logger = new LoggerConfiguration()
                //.MinimumLevel.Verbose()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logDir, "trace.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        static SourcePackage()
        {
            CreateLogger();
        }

        private async Task<bool> IsSolutionLoadedAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionService = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            if (solutionService == null)
                return false;

            ErrorHandler.ThrowOnFailure(solutionService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out var value));

            return value is bool isSolutionOpen && isSolutionOpen;
        }

        private void UpdateSourceBrowserUri(object sender = null, EventArgs e = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Log.Information($"{nameof(UpdateSourceBrowserUri)}...");
            sourceBrowserUri.HandleSolution(dte.Solution.FullName);
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Log.Information($"{nameof(InitializeAsync)}...");

            dte = (DTE)GetGlobalService(typeof(DTE));
            sourceBrowserUri = new SourceBrowserUri(new Git());

            // Load settings
            var optionPage = (OptionPage)GetDialogPage(typeof(OptionPage));
            sourceBrowserUri.UpdateSites(optionPage.Sites ?? "");

            // Initial solution
            var isSolutionLoaded = await IsSolutionLoadedAsync();
            if (isSolutionLoaded)
                UpdateSourceBrowserUri();

            // Handle new solutions
            SolutionEvents.OnAfterOpenSolution += UpdateSourceBrowserUri;

            // Handle new settings
            sourceBrowserUri.Updated += UpdateSourceBrowserUri;

            var commandService = (OleMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService));
            if (commandService == null)
                Log.Error($"{nameof(OleMenuCommandService)} is null.");
            else
            {
                var visualStudio = new VisualStudio(dte);
                var browser = new WebBrowser();
                var command = new Command(visualStudio, browser, () => sourceBrowserUri.Value);
                var menuCommand = new OleMenuCommand(InvokeHandler, command.Id);
                menuCommand.BeforeQueryStatus += BeforeQueryStatus;
                commandService.AddCommand(menuCommand);

                void InvokeHandler(object sender, EventArgs e)
                {
                    JoinableTaskFactory.RunAsync(() => command.RunAsync());
                }
                void BeforeQueryStatus(object sender, EventArgs e)
                {
                    menuCommand.Visible = menuCommand.Enabled = command.Enabled;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            Log.Debug("Disposing...");
            Log.CloseAndFlush();

            base.Dispose(disposing);
        }
    }
}
