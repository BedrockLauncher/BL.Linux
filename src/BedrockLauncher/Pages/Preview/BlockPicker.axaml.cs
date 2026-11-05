using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using BedrockLauncher.Classes;
using BedrockLauncher.UI;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.UI
{
    public static class BlockPickerConverters
    {
        public const string ImportItem = "::import";
        public static readonly IValueConverter IsImport = new FuncValueConverter<string, bool>(x => x == ImportItem);
    }
}

namespace BedrockLauncher.Pages.Preview
{
    /// <summary>Port of Component_InstallationBlockPicker: the block icon of an installation, or a custom image.</summary>
    public partial class BlockPicker : UserControl
    {
        public string IconPath { get; private set; } = Constants.INSTALLATIONS_FALLBACK_ICONPATH;
        public bool IsIconCustom { get; private set; }

        public BlockPicker()
        {
            InitializeComponent();
            var items = new List<string> { BlockPickerConverters.ImportItem };
            items.AddRange(Constants.INSTALLATION_PREFABED_ICONS_LIST_RUNTIME);
            DropdownItemPanel.ItemsSource = items;
            Update();
        }

        public void Init(BLInstallation installation = null)
        {
            if (installation != null)
            {
                IconPath = installation.IconPath ?? Constants.INSTALLATIONS_FALLBACK_ICONPATH;
                IsIconCustom = installation.IsCustomIcon;
            }
            Update();
        }

        private void Update()
        {
            string full = IsIconCustom
                ? Path.Combine(MainDataModel.Default.FilePaths.GetCacheFolderPath(), IconPath)
                : Constants.INSTALLATIONS_ICONPATH + IconPath;
            SelectedBlockIcon.Source = ImagePathConverter.Load(full);
        }

        private async void Block_Click(object sender, RoutedEventArgs e)
        {
            string item = (sender as Control)?.DataContext as string;
            if (item == BlockPickerConverters.ImportItem)
            {
                string file = await MainViewModel.Default.PickFile("Image", ("Images", new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" }));
                if (string.IsNullOrEmpty(file)) return;
                string cached = MainDataModel.Default.FilePaths.AddImageToIconCache(file);
                if (string.IsNullOrEmpty(cached)) return;
                IconPath = Path.GetFileName(cached);
                IsIconCustom = true;
            }
            else if (item != null)
            {
                IconPath = item.Substring(Constants.INSTALLATIONS_PREFABED_ICONS_ROOT.Length);
                IsIconCustom = false;
            }
            DropdownButton.IsChecked = false;
            Update();
        }
    }
}
