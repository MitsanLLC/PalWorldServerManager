using Microsoft.Win32;
using PalWorldServerManager.Models;
using PalWorldServerManager.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PalWorldServerManager
{
    public partial class MainWindow : Window
    {
        private readonly PalworldSettingsService _settingsService;

        private string? _settingsFilePath;
        private string _fileContents = "";
        private ServerSettings? _currentSettings;

        public MainWindow()
        {
            InitializeComponent();

            _settingsService =
                new PalworldSettingsService();
        }

        private void NavigationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (!int.TryParse(
                    button.Tag?.ToString(),
                    out int selectedPage))
            {
                return;
            }

            MainNavigationTabControl.SelectedIndex =
                selectedPage;
        }

        private void LoadButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Title =
                        "Select PalWorldSettings.ini",

                    Filter =
                        "INI files (*.ini)|*.ini|" +
                        "All files (*.*)|*.*"
                };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                LoadSettingsFile(
                    dialog.FileName);

                MessageBox.Show(
                    "Settings loaded successfully.",
                    "Loaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SaveButton.IsEnabled = false;

                MessageBox.Show(
                    "Could not load the settings file." +
                    $"\n\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadSettingsFile(
            string filePath)
        {
            _settingsFilePath =
                filePath;

            _fileContents =
                _settingsService.LoadFileContents(
                    filePath);

            _currentSettings =
                _settingsService.ParseSettings(
                    _fileContents);

            PopulateEditor(
                _currentSettings);

            UpdateDashboard(
                _currentSettings);

            SaveButton.IsEnabled = true;

            StatusTextBlock.Text =
                $"Loaded: {_settingsFilePath}";
        }

        private void PopulateEditor(
            ServerSettings settings)
        {
            // Server
            ServerNameTextBox.Text =
                settings.ServerName;

            DescriptionTextBox.Text =
                settings.ServerDescription;

            MaxPlayersTextBox.Text =
                settings.ServerPlayerMaxNum.ToString(
                    CultureInfo.InvariantCulture);

            AdminPasswordBox.Password =
                settings.AdminPassword;

            ServerPasswordBox.Password =
                settings.ServerPassword;

            // Gameplay
            ExpRateTextBox.Text =
                FormatEditorDecimal(
                    settings.ExpRate);

            PalCaptureRateTextBox.Text =
                FormatEditorDecimal(
                    settings.PalCaptureRate);

            PalSpawnRateTextBox.Text =
                FormatEditorDecimal(
                    settings.PalSpawnNumRate);

            DayTimeSpeedTextBox.Text =
                FormatEditorDecimal(
                    settings.DayTimeSpeedRate);

            NightTimeSpeedTextBox.Text =
                FormatEditorDecimal(
                    settings.NightTimeSpeedRate);

            WorkSpeedTextBox.Text =
                FormatEditorDecimal(
                    settings.WorkSpeedRate);

            // PvP and hardcore
            PvpCheckBox.IsChecked =
                settings.IsPvP;

            HardcoreCheckBox.IsChecked =
                settings.IsHardcore;

            FriendlyFireCheckBox.IsChecked =
                settings.EnableFriendlyFire;

            CharacterRecreateCheckBox.IsChecked =
                settings.CharacterRecreateInHardcore;

            DeathPenaltyComboBox.Text =
                settings.DeathPenalty;

            // Bases and guilds
            BaseWorkersTextBox.Text =
                settings.BaseCampWorkerMaxNum.ToString(
                    CultureInfo.InvariantCulture);

            GuildPlayersTextBox.Text =
                settings.GuildPlayerMaxNum.ToString(
                    CultureInfo.InvariantCulture);

            BasesPerGuildTextBox.Text =
                settings.BaseCampMaxNumInGuild.ToString(
                    CultureInfo.InvariantCulture);

            BaseCampMaxTextBox.Text =
                settings.BaseCampMaxNum.ToString(
                    CultureInfo.InvariantCulture);

            // Network
            PublicIpTextBox.Text =
                settings.PublicIP;

            PublicPortTextBox.Text =
                settings.PublicPort.ToString(
                    CultureInfo.InvariantCulture);

            RconEnabledCheckBox.IsChecked =
                settings.RconEnabled;

            RconPortTextBox.Text =
                settings.RconPort.ToString(
                    CultureInfo.InvariantCulture);

            RestApiEnabledCheckBox.IsChecked =
                settings.RestApiEnabled;

            RestApiPortTextBox.Text =
                settings.RestApiPort.ToString(
                    CultureInfo.InvariantCulture);

            // Advanced
            UseAuthCheckBox.IsChecked =
                settings.UseAuth;

            AllowClientModCheckBox.IsChecked =
                settings.AllowClientMod;

            BackupSaveDataCheckBox.IsChecked =
                settings.IsUseBackupSaveData;

            ShowPlayerListCheckBox.IsChecked =
                settings.ShowPlayerList;

            ChatPostLimitTextBox.Text =
                settings.ChatPostLimitPerMinute.ToString(
                    CultureInfo.InvariantCulture);
        }

        private bool TryCreateSettingsFromEditor(
            out ServerSettings settings)
        {
            settings =
                new ServerSettings();

            if (!TryReadInteger(
                    MaxPlayersTextBox.Text,
                    "Maximum Players",
                    1,
                    1000,
                    out int maximumPlayers))
            {
                return false;
            }

            if (!TryReadDecimal(
                    ExpRateTextBox.Text,
                    "XP Rate",
                    0,
                    1000,
                    out double expRate))
            {
                return false;
            }

            if (!TryReadDecimal(
                    PalCaptureRateTextBox.Text,
                    "Pal Capture Rate",
                    0,
                    1000,
                    out double captureRate))
            {
                return false;
            }

            if (!TryReadDecimal(
                    PalSpawnRateTextBox.Text,
                    "Pal Spawn Rate",
                    0,
                    1000,
                    out double spawnRate))
            {
                return false;
            }

            if (!TryReadDecimal(
                    DayTimeSpeedTextBox.Text,
                    "Daytime Speed Rate",
                    0,
                    1000,
                    out double daytimeSpeed))
            {
                return false;
            }

            if (!TryReadDecimal(
                    NightTimeSpeedTextBox.Text,
                    "Nighttime Speed Rate",
                    0,
                    1000,
                    out double nighttimeSpeed))
            {
                return false;
            }

            if (!TryReadDecimal(
                    WorkSpeedTextBox.Text,
                    "Work Speed Rate",
                    0,
                    1000,
                    out double workSpeed))
            {
                return false;
            }

            if (!TryReadInteger(
                    BaseWorkersTextBox.Text,
                    "Workers Per Base",
                    1,
                    1000,
                    out int workersPerBase))
            {
                return false;
            }

            if (!TryReadInteger(
                    GuildPlayersTextBox.Text,
                    "Maximum Guild Players",
                    1,
                    1000,
                    out int guildPlayers))
            {
                return false;
            }

            if (!TryReadInteger(
                    BasesPerGuildTextBox.Text,
                    "Bases Per Guild",
                    1,
                    1000,
                    out int basesPerGuild))
            {
                return false;
            }

            if (!TryReadInteger(
                    BaseCampMaxTextBox.Text,
                    "Maximum Total Base Camps",
                    1,
                    10000,
                    out int maximumBaseCamps))
            {
                return false;
            }

            if (!TryReadInteger(
                    PublicPortTextBox.Text,
                    "Public Port",
                    1,
                    65535,
                    out int publicPort))
            {
                return false;
            }

            if (!TryReadInteger(
                    RconPortTextBox.Text,
                    "RCON Port",
                    1,
                    65535,
                    out int rconPort))
            {
                return false;
            }

            if (!TryReadInteger(
                    RestApiPortTextBox.Text,
                    "REST API Port",
                    1,
                    65535,
                    out int restApiPort))
            {
                return false;
            }

            if (!TryReadInteger(
                    ChatPostLimitTextBox.Text,
                    "Chat Post Limit",
                    1,
                    10000,
                    out int chatPostLimit))
            {
                return false;
            }

            string deathPenalty =
                DeathPenaltyComboBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(
                    deathPenalty))
            {
                ShowValidationError(
                    "Select a Death Penalty value.");

                return false;
            }

            settings =
                new ServerSettings
                {
                    // Server
                    ServerName =
                        ServerNameTextBox.Text,

                    ServerDescription =
                        DescriptionTextBox.Text,

                    ServerPlayerMaxNum =
                        maximumPlayers,

                    AdminPassword =
                        AdminPasswordBox.Password,

                    ServerPassword =
                        ServerPasswordBox.Password,

                    // Gameplay
                    ExpRate =
                        expRate,

                    PalCaptureRate =
                        captureRate,

                    PalSpawnNumRate =
                        spawnRate,

                    DayTimeSpeedRate =
                        daytimeSpeed,

                    NightTimeSpeedRate =
                        nighttimeSpeed,

                    WorkSpeedRate =
                        workSpeed,

                    // PvP and hardcore
                    IsPvP =
                        PvpCheckBox.IsChecked == true,

                    IsHardcore =
                        HardcoreCheckBox.IsChecked == true,

                    EnableFriendlyFire =
                        FriendlyFireCheckBox.IsChecked == true,

                    CharacterRecreateInHardcore =
                        CharacterRecreateCheckBox.IsChecked == true,

                    DeathPenalty =
                        deathPenalty,

                    // Bases and guilds
                    BaseCampWorkerMaxNum =
                        workersPerBase,

                    GuildPlayerMaxNum =
                        guildPlayers,

                    BaseCampMaxNumInGuild =
                        basesPerGuild,

                    BaseCampMaxNum =
                        maximumBaseCamps,

                    // Network
                    PublicIP =
                        PublicIpTextBox.Text.Trim(),

                    PublicPort =
                        publicPort,

                    RconEnabled =
                        RconEnabledCheckBox.IsChecked == true,

                    RconPort =
                        rconPort,

                    RestApiEnabled =
                        RestApiEnabledCheckBox.IsChecked == true,

                    RestApiPort =
                        restApiPort,

                    // Advanced
                    UseAuth =
                        UseAuthCheckBox.IsChecked == true,

                    AllowClientMod =
                        AllowClientModCheckBox.IsChecked == true,

                    IsUseBackupSaveData =
                        BackupSaveDataCheckBox.IsChecked == true,

                    ShowPlayerList =
                        ShowPlayerListCheckBox.IsChecked == true,

                    ChatPostLimitPerMinute =
                        chatPostLimit
                };

            return true;
        }

        private void SaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    _settingsFilePath))
            {
                MessageBox.Show(
                    "Load a settings file first.",
                    "No File Loaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!TryCreateSettingsFromEditor(
                    out ServerSettings updatedSettings))
            {
                return;
            }

            try
            {
                string updatedContents =
                    _settingsService.ApplySettings(
                        _fileContents,
                        updatedSettings);

                string backupPath =
                    _settingsService.CreateBackup(
                        _settingsFilePath);

                _settingsService.SaveFile(
                    _settingsFilePath,
                    updatedContents);

                _fileContents =
                    updatedContents;

                _currentSettings =
                    updatedSettings;

                UpdateDashboard(
                    updatedSettings);

                StatusTextBlock.Text =
                    $"Saved: {_settingsFilePath}" +
                    $"\nBackup: {backupPath}";

                MessageBox.Show(
                    "Settings saved successfully." +
                    $"\n\nBackup created:\n{backupPath}",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not save the settings file." +
                    $"\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateDashboard(
            ServerSettings settings)
        {
            DashboardServerNameText.Text =
                string.IsNullOrWhiteSpace(
                    settings.ServerName)
                    ? "Unnamed Server"
                    : settings.ServerName;

            DashboardPlayerLimitText.Text =
                settings.ServerPlayerMaxNum.ToString(
                    CultureInfo.InvariantCulture);

            DashboardExpRateText.Text =
                $"{FormatDashboardDecimal(settings.ExpRate)}x";

            DashboardPvpText.Text =
                settings.IsPvP
                    ? "Enabled"
                    : "Disabled";

            DashboardPvpText.Foreground =
                settings.IsPvP
                    ? GetBrush("SuccessColor")
                    : GetBrush("MutedTextColor");

            DashboardHardcoreText.Text =
                settings.IsHardcore
                    ? "Enabled"
                    : "Disabled";

            DashboardHardcoreText.Foreground =
                settings.IsHardcore
                    ? GetBrush("WarningColor")
                    : GetBrush("MutedTextColor");

            DashboardGameModeText.Text =
                settings.GameMode;

            DashboardPortText.Text =
                settings.PublicPort.ToString(
                    CultureInfo.InvariantCulture);

            DashboardStatusDot.Fill =
                GetBrush("SuccessColor");

            DashboardStatusTitle.Text =
                "Settings file loaded";

            DashboardStatusDescription.Text =
                _settingsFilePath ?? "";
        }

        private bool TryReadInteger(
            string text,
            string displayName,
            int minimum,
            int maximum,
            out int value)
        {
            if (!int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                ShowValidationError(
                    $"{displayName} must be a whole number.");

                return false;
            }

            if (value < minimum ||
                value > maximum)
            {
                ShowValidationError(
                    $"{displayName} must be between " +
                    $"{minimum} and {maximum}.");

                return false;
            }

            return true;
        }

        private bool TryReadDecimal(
            string text,
            string displayName,
            double minimum,
            double maximum,
            out double value)
        {
            if (!double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                ShowValidationError(
                    $"{displayName} must be a valid number.");

                return false;
            }

            if (value < minimum ||
                value > maximum)
            {
                ShowValidationError(
                    $"{displayName} must be between " +
                    $"{minimum} and {maximum}.");

                return false;
            }

            return true;
        }

        private void ShowValidationError(
            string message)
        {
            MessageBox.Show(
                message,
                "Invalid Value",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private static string FormatEditorDecimal(
            double value)
        {
            return value.ToString(
                "0.000000",
                CultureInfo.InvariantCulture);
        }

        private static string FormatDashboardDecimal(
            double value)
        {
            return value.ToString(
                "0.######",
                CultureInfo.InvariantCulture);
        }

        private Brush GetBrush(
            string resourceName)
        {
            return (Brush)FindResource(
                resourceName);
        }
    }
}