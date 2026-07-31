using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace PalWorldServerManager
{
    public partial class MainWindow : Window
    {
        private string? _settingsFilePath;
        private string _fileContents = "";

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

            MainNavigationTabControl.SelectedIndex = selectedPage;
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select PalWorldSettings.ini",
                Filter = "INI files (*.ini)|*.ini|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _settingsFilePath = dialog.FileName;
                _fileContents = File.ReadAllText(_settingsFilePath);

                if (!_fileContents.Contains("OptionSettings=("))
                {
                    throw new InvalidDataException(
                        "This file does not contain a Palworld OptionSettings section.");
                }

                LoadServerSettings();
                LoadGameplaySettings();
                LoadPvpSettings();
                LoadBaseSettings();
                LoadNetworkSettings();
                LoadAdvancedSettings();

                SaveButton.IsEnabled = true;

                StatusTextBlock.Text =
                    $"Loaded: {_settingsFilePath}";

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
                    $"Could not load the settings file.\n\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadServerSettings()
        {
            ServerNameTextBox.Text =
                GetSettingValue("ServerName");

            DescriptionTextBox.Text =
                GetSettingValue("ServerDescription");

            MaxPlayersTextBox.Text =
                GetSettingValue("ServerPlayerMaxNum");

            AdminPasswordBox.Password =
                GetSettingValue("AdminPassword");

            ServerPasswordBox.Password =
                GetSettingValue("ServerPassword");
        }

        private void LoadGameplaySettings()
        {
            ExpRateTextBox.Text =
                GetSettingValue("ExpRate");

            PalCaptureRateTextBox.Text =
                GetSettingValue("PalCaptureRate");

            PalSpawnRateTextBox.Text =
                GetSettingValue("PalSpawnNumRate");

            DayTimeSpeedTextBox.Text =
                GetSettingValue("DayTimeSpeedRate");

            NightTimeSpeedTextBox.Text =
                GetSettingValue("NightTimeSpeedRate");

            WorkSpeedTextBox.Text =
                GetSettingValue("WorkSpeedRate");
        }

        private void LoadPvpSettings()
        {
            PvpCheckBox.IsChecked =
                GetBooleanSetting("bIsPvP");

            HardcoreCheckBox.IsChecked =
                GetBooleanSetting("bHardcore");

            FriendlyFireCheckBox.IsChecked =
                GetBooleanSetting("bEnableFriendlyFire");

            CharacterRecreateCheckBox.IsChecked =
                GetBooleanSetting("bCharacterRecreateInHardcore");

            DeathPenaltyComboBox.Text =
                GetSettingValue("DeathPenalty");
        }

        private void LoadBaseSettings()
        {
            BaseWorkersTextBox.Text =
                GetSettingValue("BaseCampWorkerMaxNum");

            GuildPlayersTextBox.Text =
                GetSettingValue("GuildPlayerMaxNum");

            BasesPerGuildTextBox.Text =
                GetSettingValue("BaseCampMaxNumInGuild");

            BaseCampMaxTextBox.Text =
                GetSettingValue("BaseCampMaxNum");
        }

        private void LoadNetworkSettings()
        {
            PublicIpTextBox.Text =
                GetSettingValue("PublicIP");

            PublicPortTextBox.Text =
                GetSettingValue("PublicPort");

            RconEnabledCheckBox.IsChecked =
                GetBooleanSetting("RCONEnabled");

            RconPortTextBox.Text =
                GetSettingValue("RCONPort");

            RestApiEnabledCheckBox.IsChecked =
                GetBooleanSetting("RESTAPIEnabled");

            RestApiPortTextBox.Text =
                GetSettingValue("RESTAPIPort");
        }

        private void LoadAdvancedSettings()
        {
            UseAuthCheckBox.IsChecked =
                GetBooleanSetting("bUseAuth");

            AllowClientModCheckBox.IsChecked =
                GetBooleanSetting("bAllowClientMod");

            BackupSaveDataCheckBox.IsChecked =
                GetBooleanSetting("bIsUseBackupSaveData");

            ShowPlayerListCheckBox.IsChecked =
                GetBooleanSetting("bShowPlayerList");

            ChatPostLimitTextBox.Text =
                GetSettingValue("ChatPostLimitPerMinute");
        }

        private string GetSettingValue(string settingName)
        {
            string pattern =
                $@"(?:^|,){Regex.Escape(settingName)}=(?:""(?<quoted>(?:\\.|[^""])*)""|(?<plain>[^,\)]*))";

            Match match = Regex.Match(
                _fileContents,
                pattern);

            if (!match.Success)
            {
                return "";
            }

            if (match.Groups["quoted"].Success)
            {
                return UnescapeIniText(
                    match.Groups["quoted"].Value);
            }

            return match.Groups["plain"].Value.Trim();
        }

        private bool GetBooleanSetting(string settingName)
        {
            return GetSettingValue(settingName)
                .Equals(
                    "True",
                    StringComparison.OrdinalIgnoreCase);
        }

        private string SetSettingValue(
            string contents,
            string settingName,
            string newValue,
            bool useQuotes)
        {
            string formattedValue = useQuotes
                ? $"\"{EscapeIniText(newValue)}\""
                : newValue;

            string pattern =
                $@"(?<prefix>(?:^|,){Regex.Escape(settingName)}=)(?:""(?:\\.|[^""])*""|[^,\)]*)";

            Match match = Regex.Match(
                contents,
                pattern);

            if (!match.Success)
            {
                throw new InvalidDataException(
                    $"The setting '{settingName}' was not found.");
            }

            string replacement =
                match.Groups["prefix"].Value +
                formattedValue;

            return contents
                .Remove(match.Index, match.Length)
                .Insert(match.Index, replacement);
        }

        private string EscapeIniText(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private string UnescapeIniText(string value)
        {
            return value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
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

            if (value < minimum || value > maximum)
            {
                ShowValidationError(
                    $"{displayName} must be between {minimum} and {maximum}.");

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

            if (value < minimum || value > maximum)
            {
                ShowValidationError(
                    $"{displayName} must be between {minimum} and {maximum}.");

                return false;
            }

            return true;
        }

        private void ShowValidationError(string message)
        {
            MessageBox.Show(
                message,
                "Invalid Value",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void SaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settingsFilePath))
            {
                MessageBox.Show(
                    "Load a settings file first.");

                return;
            }

            if (!TryReadInteger(
                    MaxPlayersTextBox.Text,
                    "Maximum Players",
                    1,
                    1000,
                    out int maxPlayers))
            {
                return;
            }

            if (!TryReadDecimal(
                    ExpRateTextBox.Text,
                    "XP Rate",
                    0,
                    1000,
                    out double expRate))
            {
                return;
            }

            if (!TryReadDecimal(
                    PalCaptureRateTextBox.Text,
                    "Pal Capture Rate",
                    0,
                    1000,
                    out double captureRate))
            {
                return;
            }

            if (!TryReadDecimal(
                    PalSpawnRateTextBox.Text,
                    "Pal Spawn Rate",
                    0,
                    1000,
                    out double spawnRate))
            {
                return;
            }

            if (!TryReadDecimal(
                    DayTimeSpeedTextBox.Text,
                    "Daytime Speed Rate",
                    0,
                    1000,
                    out double daySpeed))
            {
                return;
            }

            if (!TryReadDecimal(
                    NightTimeSpeedTextBox.Text,
                    "Nighttime Speed Rate",
                    0,
                    1000,
                    out double nightSpeed))
            {
                return;
            }

            if (!TryReadDecimal(
                    WorkSpeedTextBox.Text,
                    "Work Speed Rate",
                    0,
                    1000,
                    out double workSpeed))
            {
                return;
            }

            if (!TryReadInteger(
                    BaseWorkersTextBox.Text,
                    "Workers Per Base",
                    1,
                    1000,
                    out int baseWorkers))
            {
                return;
            }

            if (!TryReadInteger(
                    GuildPlayersTextBox.Text,
                    "Maximum Guild Players",
                    1,
                    1000,
                    out int guildPlayers))
            {
                return;
            }

            if (!TryReadInteger(
                    BasesPerGuildTextBox.Text,
                    "Bases Per Guild",
                    1,
                    1000,
                    out int basesPerGuild))
            {
                return;
            }

            if (!TryReadInteger(
                    BaseCampMaxTextBox.Text,
                    "Maximum Total Base Camps",
                    1,
                    10000,
                    out int maximumBaseCamps))
            {
                return;
            }

            if (!TryReadInteger(
                    PublicPortTextBox.Text,
                    "Public Port",
                    1,
                    65535,
                    out int publicPort))
            {
                return;
            }

            if (!TryReadInteger(
                    RconPortTextBox.Text,
                    "RCON Port",
                    1,
                    65535,
                    out int rconPort))
            {
                return;
            }

            if (!TryReadInteger(
                    RestApiPortTextBox.Text,
                    "REST API Port",
                    1,
                    65535,
                    out int restApiPort))
            {
                return;
            }

            if (!TryReadInteger(
                    ChatPostLimitTextBox.Text,
                    "Chat Post Limit",
                    1,
                    10000,
                    out int chatPostLimit))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    DeathPenaltyComboBox.Text))
            {
                ShowValidationError(
                    "Select a Death Penalty value.");

                return;
            }

            try
            {
                string updatedContents = _fileContents;

                // Server
                updatedContents = SetSettingValue(
                    updatedContents,
                    "ServerName",
                    ServerNameTextBox.Text,
                    true);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "ServerDescription",
                    DescriptionTextBox.Text,
                    true);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "ServerPlayerMaxNum",
                    maxPlayers.ToString(
                        CultureInfo.InvariantCulture),
                    false);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "AdminPassword",
                    AdminPasswordBox.Password,
                    true);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "ServerPassword",
                    ServerPasswordBox.Password,
                    true);

                // Gameplay
                updatedContents = SetSettingValue(
                    updatedContents,
                    "ExpRate",
                    FormatDecimal(expRate),
                    false);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "PalCaptureRate",
                    FormatDecimal(captureRate),
                    false);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "PalSpawnNumRate",
                    FormatDecimal(spawnRate),
                    false);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "DayTimeSpeedRate",
                    FormatDecimal(daySpeed),
                    false);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "NightTimeSpeedRate",
                    FormatDecimal(nightSpeed),
                    false);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "WorkSpeedRate",
                    FormatDecimal(workSpeed),
                    false);

                // PvP
                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "bIsPvP",
                    PvpCheckBox.IsChecked == true);

                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "bHardcore",
                    HardcoreCheckBox.IsChecked == true);

                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "bEnableFriendlyFire",
                    FriendlyFireCheckBox.IsChecked == true);

                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "bCharacterRecreateInHardcore",
                    CharacterRecreateCheckBox.IsChecked == true);

                updatedContents = SetSettingValue(
                    updatedContents,
                    "DeathPenalty",
                    DeathPenaltyComboBox.Text.Trim(),
                    false);

                // Bases
                updatedContents = SetIntegerSetting(
                    updatedContents,
                    "BaseCampWorkerMaxNum",
                    baseWorkers);

                updatedContents = SetIntegerSetting(
                    updatedContents,
                    "GuildPlayerMaxNum",
                    guildPlayers);

                updatedContents = SetIntegerSetting(
                    updatedContents,
                    "BaseCampMaxNumInGuild",
                    basesPerGuild);

                updatedContents = SetIntegerSetting(
                    updatedContents,
                    "BaseCampMaxNum",
                    maximumBaseCamps);

                // Network
                updatedContents = SetSettingValue(
                    updatedContents,
                    "PublicIP",
                    PublicIpTextBox.Text.Trim(),
                    true);

                updatedContents = SetIntegerSetting(
                    updatedContents,
                    "PublicPort",
                    publicPort);

                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "RCONEnabled",
                    RconEnabledCheckBox.IsChecked == true);

                updatedContents = SetIntegerSetting(
                    updatedContents,
                    "RCONPort",
                    rconPort);

                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "RESTAPIEnabled",
                    RestApiEnabledCheckBox.IsChecked == true);

                updatedContents = SetIntegerSetting(
                    updatedContents,
                    "RESTAPIPort",
                    restApiPort);

                // Advanced
                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "bUseAuth",
                    UseAuthCheckBox.IsChecked == true);

                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "bAllowClientMod",
                    AllowClientModCheckBox.IsChecked == true);

                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "bIsUseBackupSaveData",
                    BackupSaveDataCheckBox.IsChecked == true);

                updatedContents = SetBooleanSetting(
                    updatedContents,
                    "bShowPlayerList",
                    ShowPlayerListCheckBox.IsChecked == true);

                updatedContents = SetIntegerSetting(
                    updatedContents,
                    "ChatPostLimitPerMinute",
                    chatPostLimit);

                string backupPath =
                    CreateBackupFile();

                File.WriteAllText(
                    _settingsFilePath,
                    updatedContents);

                _fileContents = updatedContents;

                StatusTextBlock.Text =
                    $"Saved: {_settingsFilePath}\nBackup: {backupPath}";

                MessageBox.Show(
                    $"Settings saved successfully.\n\nBackup created:\n{backupPath}",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not save the settings file.\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string SetBooleanSetting(
            string contents,
            string settingName,
            bool value)
        {
            return SetSettingValue(
                contents,
                settingName,
                value ? "True" : "False",
                false);
        }

        private string SetIntegerSetting(
            string contents,
            string settingName,
            int value)
        {
            return SetSettingValue(
                contents,
                settingName,
                value.ToString(
                    CultureInfo.InvariantCulture),
                false);
        }

        private string FormatDecimal(double value)
        {
            return value.ToString(
                "0.000000",
                CultureInfo.InvariantCulture);
        }

        private string CreateBackupFile()
        {
            if (string.IsNullOrWhiteSpace(_settingsFilePath))
            {
                throw new InvalidOperationException(
                    "No settings file is loaded.");
            }

            string directory =
                Path.GetDirectoryName(_settingsFilePath)
                ?? "";

            string fileNameWithoutExtension =
                Path.GetFileNameWithoutExtension(
                    _settingsFilePath);

            string extension =
                Path.GetExtension(_settingsFilePath);

            string timeStamp =
                DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture);

            string backupPath =
                Path.Combine(
                    directory,
                    $"{fileNameWithoutExtension}.{timeStamp}.backup{extension}");

            File.Copy(
                _settingsFilePath,
                backupPath,
                overwrite: false);

            return backupPath;
        }
    }


}