using Microsoft.Win32;
using PalWorldServerManager.Models;
using System.IO;
using System.Windows;

namespace PalWorldServerManager
{
    public partial class SetupWizardWindow : Window
    {
        public string ServerExecutablePath =>
            ServerExecutablePathTextBox.Text.Trim();

        public string SettingsFilePath =>
            SettingsFilePathTextBox.Text.Trim();

        public string SteamCmdPath =>
            SteamCmdPathTextBox.Text.Trim();

        public bool AttachToRunningServer =>
            AttachToRunningServerCheckBox.IsChecked == true;

        public bool CheckForUpdatesBeforeStartup =>
            CheckForUpdatesCheckBox.IsChecked == true;

        public bool StartWithWindows =>
            StartWithWindowsCheckBox.IsChecked == true;

        public SetupWizardWindow(
            AppPreferences preferences)
        {
            InitializeComponent();

            ServerExecutablePathTextBox.Text =
                preferences.ServerExecutablePath;

            SettingsFilePathTextBox.Text =
                preferences.LastSettingsFilePath;

            SteamCmdPathTextBox.Text =
                preferences.SteamCmdPath;

            AttachToRunningServerCheckBox.IsChecked =
                preferences.AttachToRunningServerOnStartup;

            CheckForUpdatesCheckBox.IsChecked =
                preferences.CheckForServerUpdatesBeforeStartup;

            StartWithWindowsCheckBox.IsChecked =
                preferences.StartWithWindows;
        }

        private void BrowseServer_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Title = "Select PalServer.exe",
                    Filter = "PalServer.exe|PalServer.exe|Executable files (*.exe)|*.exe"
                };

            if (dialog.ShowDialog() == true)
            {
                ServerExecutablePathTextBox.Text =
                    dialog.FileName;
            }
        }

        private void BrowseSettings_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Title = "Select PalWorldSettings.ini",
                    Filter = "PalWorldSettings.ini|PalWorldSettings.ini|INI files (*.ini)|*.ini"
                };

            if (dialog.ShowDialog() == true)
            {
                SettingsFilePathTextBox.Text =
                    dialog.FileName;
            }
        }

        private void BrowseSteamCmd_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Title = "Select steamcmd.exe",
                    Filter = "steamcmd.exe|steamcmd.exe|Executable files (*.exe)|*.exe"
                };

            if (dialog.ShowDialog() == true)
            {
                SteamCmdPathTextBox.Text =
                    dialog.FileName;
            }
        }

        private void Finish_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    ServerExecutablePath) ||
                !File.Exists(
                    ServerExecutablePath))
            {
                MessageBox.Show(
                    "Select a valid PalServer.exe before finishing setup.",
                    "PalServer.exe Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    SettingsFilePath) ||
                !File.Exists(
                    SettingsFilePath))
            {
                MessageBox.Show(
                    "Select a valid PalWorldSettings.ini before finishing setup.",
                    "Settings File Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (CheckForUpdatesBeforeStartup &&
                (string.IsNullOrWhiteSpace(
                     SteamCmdPath) ||
                 !File.Exists(
                     SteamCmdPath)))
            {
                MessageBox.Show(
                    "Update-before-start is enabled, so select a valid steamcmd.exe or turn that option off.",
                    "SteamCMD Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            DialogResult =
                true;

            Close();
        }

        private void Skip_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult =
                false;

            Close();
        }

        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult =
                false;

            Close();
        }
    }
}