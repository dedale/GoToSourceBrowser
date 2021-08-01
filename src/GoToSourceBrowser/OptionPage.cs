using Microsoft.VisualStudio.Shell;
using Serilog;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GoToSourceBrowser
{
    // https://docs.microsoft.com/fr-fr/visualstudio/extensibility/creating-an-options-page?view=vs-2019

    [ComVisible(true)]
    [Guid("08BECBC0-5E85-4881-9A67-8A5610426940")]
    public class OptionPage : DialogPage
    {
        public static event EventHandler OptionUpdated;

        private string loadedSites;

        [Category("General")]
        [DisplayName("Repository Sites")]
        [Description("List of RepoName=Uri, separated by semicolons.")]
        public string Sites { get; set; }

        public override void LoadSettingsFromStorage()
        {
            try
            {
                Log.Debug($"{nameof(LoadSettingsFromStorage)}...");
                base.LoadSettingsFromStorage();
                loadedSites = Sites;
            }
            catch (Exception e)
            {
                Log.Error(e, nameof(LoadSettingsFromStorage));
            }
        }

        public override void SaveSettingsToStorage()
        {
            try
            {
                Log.Debug($"{nameof(SaveSettingsToStorage)}...");
                base.SaveSettingsToStorage();
                if (loadedSites != Sites)
                    OptionUpdated?.Invoke(this, new EventArgs());
            }
            catch (Exception e)
            {
                Log.Error(e, nameof(SaveSettingsToStorage));
            }
        }
    }
}
