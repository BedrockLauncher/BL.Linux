using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BedrockLauncher.Classes;
using BedrockLauncher.Enums;
using BedrockLauncher.Handlers;
using BedrockLauncher.Localization.Language;
using BedrockLauncher.Pages.Common;
using BedrockLauncher.Pages.Preview;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Play
{
    public partial class InstallationsScreen : UserControl
    {
        public static readonly StyledProperty<bool> AllowEditingProperty = AvaloniaProperty.Register<InstallationsScreen, bool>(nameof(AllowEditing), true);
        public static readonly StyledProperty<bool> ManualOrderProperty = AvaloniaProperty.Register<InstallationsScreen, bool>(nameof(ManualOrder));

        public bool AllowEditing { get => GetValue(AllowEditingProperty); set => SetValue(AllowEditingProperty, value); }
        public bool ManualOrder { get => GetValue(ManualOrderProperty); set => SetValue(ManualOrderProperty, value); }

        private BLProfileList config;
        private INotifyCollectionChanged source;
        private bool loading;

        public InstallationsScreen()
        {
            InitializeComponent();
            loading = true;
            var settings = Properties.LauncherSettings.Default;
            ShowReleasesCheckBox.IsChecked = settings.ShowReleases;
            ShowBetasCheckBox.IsChecked = settings.ShowBetas;
            ShowPreviewsCheckBox.IsChecked = settings.ShowPreviews;
            SortByComboBox.SelectedIndex = (int)settings.InstallationsSortMode;
            loading = false;

            var progress = MainDataModel.Default.ProgressBarState;
            progress.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ProgressBarModel.AllowEditing)) UpdateEditing(); };
            MainDataModel.Default.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainDataModel.Config)) Bind(); };
            UpdateEditing();
            Bind();
        }

        private void UpdateEditing()
        {
            AllowEditing = MainDataModel.Default.ProgressBarState.AllowEditing;
            NewInstallationButton.IsEnabled = AllowEditing;
            ShowReleasesCheckBox.IsEnabled = ShowBetasCheckBox.IsEnabled = ShowPreviewsCheckBox.IsEnabled = AllowEditing;
        }

        private void Bind()
        {
            if (config != null) config.PropertyChanged -= Config_PropertyChanged;
            config = MainDataModel.Default.Config;
            config.PropertyChanged += Config_PropertyChanged;
            BindSource();
        }

        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(BLProfileList.CurrentInstallations) or nameof(BLProfileList.CurrentProfile)) BindSource();
        }

        private void BindSource()
        {
            if (source != null) source.CollectionChanged -= Source_CollectionChanged;
            source = config.CurrentInstallations;
            if (source != null) source.CollectionChanged += Source_CollectionChanged;
            RefreshList();
        }

        private void Source_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => RefreshList();

        public void RefreshList()
        {
            ManualOrder = Properties.LauncherSettings.Default.InstallationsSortMode == InstallationSort.None;
            var items = config.CurrentInstallations?.Where(FilterSortingHandler.Filter_InstallationList) ?? Enumerable.Empty<BLInstallation>();
            var list = FilterSortingHandler.Sort_InstallationList(items).ToList();
            InstallationsList.ItemsSource = list;
            NothingFound.IsVisible = list.Count == 0;
        }

        #region Toolbar

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterSortingHandler.InstallationsSearchFilter = SearchBox.Text ?? string.Empty;
            RefreshList();
        }

        private void SortByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (loading || SortByComboBox.SelectedIndex < 0) return;
            Properties.LauncherSettings.Default.InstallationsSortMode = (InstallationSort)SortByComboBox.SelectedIndex;
            RefreshList();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            var settings = Properties.LauncherSettings.Default;
            settings.ShowReleases = ShowReleasesCheckBox.IsChecked == true;
            settings.ShowBetas = ShowBetasCheckBox.IsChecked == true;
            settings.ShowPreviews = ShowPreviewsCheckBox.IsChecked == true;
            settings.Save();
            RefreshList();
        }

        private void NewInstallationButton_Click(object sender, RoutedEventArgs e) => MainViewModel.Default.SetOverlayFrame(new EditInstallationScreen());

        #endregion

        #region Item buttons (InstallationItem.xaml.cs)

        private static BLInstallation Item(object sender) => (sender as Control)?.DataContext as BLInstallation;

        private async void Play_Click(object sender, RoutedEventArgs e) =>
            await MainDataModel.Default.Play(MainDataModel.Default.Config.CurrentProfile, Item(sender), Properties.LauncherSettings.Default.KeepLauncherOpen, false);

        private void Folder_Click(object sender, RoutedEventArgs e) => Item(sender)?.OpenDirectory();

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            MainDataModel.Default.Config.Installation_MoveUp(Item(sender));
            RefreshList();
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            MainDataModel.Default.Config.Installation_MoveDown(Item(sender));
            RefreshList();
        }

        private void More_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            InstallationsList.SelectedItem = Item(sender);
            button.ContextMenu?.Open(button);
        }

        private void Edit_Click(object sender, RoutedEventArgs e) => MainViewModel.Default.SetOverlayFrame(new EditInstallationScreen(Item(sender)));

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var installation = Item(sender);
            var result = await DialogPrompt.ShowDialog_YesNo_Optional(
                LanguageManager.GetResource("Dialog_DeleteItem_Title"),
                LanguageManager.GetResource("Dialog_DeleteItem_Text"),
                LanguageManager.GetResource("Dialog_Item_Optional_DeleteInstallationData"), true,
                LanguageManager.GetResource("Dialog_Item_Installation_Text"), installation.DisplayName_Full);
            if (result.Item1 == DialogResult.Yes) MainDataModel.Default.Config.Installation_Delete(installation, result.Item2);
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e) => MainDataModel.Default.Config.Installation_Clone(Item(sender));

        private async void Install_Click(object sender, RoutedEventArgs e) => await MainDataModel.Default.Install(MainDataModel.Default.Config.CurrentProfile, Item(sender));

        private async void Backup_Click(object sender, RoutedEventArgs e) => await InstallationBackupHandler.BackupAsync(Item(sender));

        private async void Restore_Click(object sender, RoutedEventArgs e) => await InstallationBackupHandler.RestoreAsync(Item(sender));

        #endregion
    }
}
