using System;
using System.Linq;
using Avalonia.Controls;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Settings
{
    /// <summary>Port of LanguageCombobox: switches the launcher language at once.</summary>
    public class LanguageCombobox : ComboBox
    {
        protected override Type StyleKeyOverride => typeof(ComboBox);

        public LanguageCombobox()
        {
            Theme = (Avalonia.Styling.ControlTheme)Avalonia.Application.Current?.FindResource("BlankComboBoxStyle");
            MinWidth = 220;
            var languages = LanguageManager.GetLanguages(MainDataModel.Default.FilePaths.LanguagesFolder);
            ItemsSource = languages;
            SelectedItem = languages.FirstOrDefault(x => x.Locale == LanguageManager.CurrentLocale);
            SelectionChanged += (_, _) =>
            {
                if (SelectedItem is not LanguageDefinition language || language.Locale == LanguageManager.CurrentLocale) return;
                LanguageManager.SetLanguage(language.Locale, language.IsExternal ? language.Path : null);
                Properties.LauncherSettings.Default.Language = language.Locale;
                Properties.LauncherSettings.Default.Save();
            };
        }
    }

    /// <summary>Port of ThemeCombobox: play screen background (built-in updates or images in the themes folder).</summary>
    public class ThemeCombobox : ComboBox
    {
        protected override Type StyleKeyOverride => typeof(ComboBox);

        public ThemeCombobox()
        {
            Theme = (Avalonia.Styling.ControlTheme)Avalonia.Application.Current?.FindResource("BlankComboBoxStyle");
            MinWidth = 220;
            AddItem("LatestUpdate", Avalonia.Media.Brushes.Yellow);
            foreach (var entry in Constants.Themes) AddItem(entry.Key);

            var folder = System.IO.Directory.CreateDirectory(MainDataModel.Default.FilePaths.ThemesFolder);
            foreach (var file in folder.GetFiles().Where(f => f.Extension is ".png" or ".jpg" or ".jpeg"))
                AddItem(file.Name, Avalonia.Media.Brushes.LightYellow, isCustom: true);

            string current = Properties.LauncherSettings.Default.CurrentTheme;
            SelectedItem = Items.Cast<ComboBoxItem>().FirstOrDefault(x => (string)x.Tag == current)
                           ?? Items.Cast<ComboBoxItem>().First();

            SelectionChanged += (_, _) =>
            {
                if (SelectedItem is not ComboBoxItem item) return;
                Properties.LauncherSettings.Default.CurrentTheme = (string)item.Tag;
                Properties.LauncherSettings.Default.Save();
            };
        }

        private void AddItem(string tag, Avalonia.Media.IBrush brush = null, bool isCustom = false)
        {
            var item = new ComboBoxItem { VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center };
            if (brush != null) item.Foreground = brush;
            if (isCustom)
            {
                item.Tag = Constants.ThemesCustomPrefix + tag;
                item.Content = $"{LanguageManager.GetResource("ThemeEntries_Custom")} - {tag}";
            }
            else
            {
                item.Tag = tag;
                item.Bind(ContentControl.ContentProperty, item.GetResourceObservable("ThemeEntries_" + tag));
            }
            Items.Add(item);
        }
    }
}
