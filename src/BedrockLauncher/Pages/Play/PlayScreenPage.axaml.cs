using System.IO;
using System.Linq;
using Avalonia.Controls;
using BedrockLauncher.UI;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Play
{
    public partial class PlayScreenPage : UserControl
    {
        public PlayScreenPage()
        {
            InitializeComponent();
            Properties.LauncherSettings.Default.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Properties.LauncherSettings.CurrentTheme)) UpdateTheme();
            };
            UpdateTheme();
        }

        /// <summary>Background image of the selected theme (upstream Grid_Loaded).</summary>
        public void UpdateTheme()
        {
            Background.Source = ImagePathConverter.Load(GetThemeImage(Properties.LauncherSettings.Default.CurrentTheme));
        }

        public static string GetThemeImage(string currentTheme)
        {
            string original = Constants.Themes["Original"];
            if (string.IsNullOrEmpty(currentTheme) || currentTheme == "LatestUpdate") return Constants.Themes.First().Value;

            if (currentTheme.StartsWith(Constants.ThemesCustomPrefix))
            {
                string name = currentTheme.Substring(Constants.ThemesCustomPrefix.Length);
                var folder = Directory.CreateDirectory(MainDataModel.Default.FilePaths.ThemesFolder);
                return folder.GetFiles().FirstOrDefault(f => f.Name == name)?.FullName ?? original;
            }

            return Constants.Themes.TryGetValue(currentTheme, out string path) ? path : original;
        }
    }
}
