using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BedrockLauncher.Classes;
using BedrockLauncher.UpdateProcessor.Enums;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Preview
{
    public partial class EditInstallationVersionSelectScreen : UserControl
    {
        private readonly TaskCompletionSource<string> result = new();

        public EditInstallationVersionSelectScreen()
        {
            InitializeComponent();
            Refresh();
        }

        public Task<string> GetVersionUUID() => result.Task;

        private bool Accept(MCVersion v)
        {
            if (v.IsCustom && ShowImported.IsChecked != true) return false;
            if (v.IsRelease && ShowRelease.IsChecked != true) return false;
            if (v.IsBeta && ShowBeta.IsChecked != true) return false;
            if (v.IsPreview && ShowPreview.IsChecked != true) return false;
            if (v.PackageType == PackageType.UWP && ShowUWP.IsChecked != true) return false;
            if (v.PackageType == PackageType.GDK && ShowGDK.IsChecked != true) return false;

            string arch = (v.Architecture ?? string.Empty).ToLowerInvariant();
            if (arch == "x86" && ShowX86.IsChecked != true) return false;
            if (arch == "x64" && ShowX64.IsChecked != true) return false;
            if (arch.StartsWith("arm") && ShowARM.IsChecked != true) return false;

            string search = SearchBox.Text;
            return string.IsNullOrWhiteSpace(search) || (v.DisplayName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private void Refresh()
        {
            if (VersionsList == null) return;
            VersionsList.ItemsSource = MainDataModel.Default.Versions.Where(Accept).ToList();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => Refresh();

        private void Close(string value)
        {
            MainViewModel.Default.SetDialogFrame(null);
            result.TrySetResult(value);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(null);
        private void Select_Click(object sender, RoutedEventArgs e) => Close((VersionsList.SelectedItem as MCVersion)?.UUID);
        private void VersionsList_DoubleTapped(object sender, TappedEventArgs e) => Select_Click(sender, e);
    }
}
