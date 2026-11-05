using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using BedrockLauncher.Classes;
using BedrockLauncher.ViewModels;

namespace BedrockLauncher.Pages.Play
{
    /// <summary>The installation combo box under the play button; selection is the profile's current installation.</summary>
    public partial class InstallationSelector : UserControl
    {
        private BLProfileList config;
        private INotifyCollectionChanged installations;
        private bool updating;

        public event EventHandler SelectionChanged;

        public BLInstallation SelectedItem => Combo.SelectedItem as BLInstallation;

        public InstallationSelector()
        {
            InitializeComponent();
            MainDataModel.Default.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainDataModel.Config)) Bind(); };
            MainDataModel.Default.ProgressBarState.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProgressBarModel.AllowEditing)) Combo.IsEnabled = MainDataModel.Default.ProgressBarState.AllowEditing;
            };
            Bind();
        }

        private void Bind()
        {
            if (config != null) config.PropertyChanged -= Config_PropertyChanged;
            config = MainDataModel.Default.Config;
            config.PropertyChanged += Config_PropertyChanged;
            Refresh();
        }

        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(BLProfileList.CurrentInstallations) or nameof(BLProfileList.CurrentProfile)
                or nameof(BLProfileList.CurrentInstallationUUID) or nameof(BLProfileList.CurrentInstallation))
                Refresh();
        }

        private void Refresh()
        {
            updating = true;
            try
            {
                if (installations != null) installations.CollectionChanged -= Installations_CollectionChanged;
                var list = config.CurrentInstallations;
                installations = list;
                if (installations != null) installations.CollectionChanged += Installations_CollectionChanged;
                if (!ReferenceEquals(Combo.ItemsSource, list)) Combo.ItemsSource = list;
                var selected = list?.FirstOrDefault(x => x.InstallationUUID == config.CurrentInstallationUUID) ?? list?.FirstOrDefault();
                Combo.SelectedItem = selected;
                // Like WPF's IsSynchronizedWithCurrentItem: with no valid current installation the first one becomes current.
                if (selected != null && selected.InstallationUUID != config.CurrentInstallationUUID)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => config.CurrentInstallationUUID = selected.InstallationUUID);
            }
            finally
            {
                updating = false;
            }
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Installations_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => Refresh();

        private void Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updating) return;
            if (Combo.SelectedItem is BLInstallation installation && installation.InstallationUUID != config.CurrentInstallationUUID)
                config.CurrentInstallationUUID = installation.InstallationUUID;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
