using Microsoft.Win32;
using PalWorldServerManager.Models;
using PalWorldServerManager.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PalWorldServerManager
{
    public partial class MainWindow : Window
    {
        private readonly PalworldSettingsService _settingsService = new();
        private readonly BackupService _backupService = new();
        private readonly ServerProcessService _serverProcessService = new();
        private readonly AppPreferencesService _preferencesService = new();
        private readonly PalworldRestApiService _restApiService = new();
        private readonly DispatcherTimer _serverStatusTimer;
        private readonly ObservableCollection<string> _activityLog = new();

        private string? _settingsFilePath;
        private string _fileContents = "";
        private ServerSettings? _currentSettings;
        private AppPreferences _preferences;

        public MainWindow()
        {
            InitializeComponent();

            _preferences = _preferencesService.Load();
            PopulatePreferencesControls();
            WireBackupEvents();
            WireServerControlEvents();
            WireConsoleEvents();
            WirePlayerEvents();
            UpdateBackupPageForNoFile();

            _serverStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _serverStatusTimer.Tick += ServerStatusTimer_Tick;
            _serverStatusTimer.Start();

            if (_preferences.AttachToRunningServerOnStartup)
            {
                _serverProcessService.AttachToRunningServer();
            }

            UpdateServerProcessDisplay();
            AddActivity("PalWorld Server Manager started.");
            Closed += MainWindow_Closed;
        }

        private void WireBackupEvents()
        {
            RefreshBackupsButton.Click += RefreshBackupsButton_Click;
            OpenBackupFolderButton.Click += OpenBackupFolderButton_Click;
            RestoreBackupButton.Click += RestoreBackupButton_Click;
            DeleteBackupButton.Click += DeleteBackupButton_Click;
            BackupListView.SelectionChanged += BackupListView_SelectionChanged;
        }

        private void WireServerControlEvents()
        {
            BrowseServerExecutableButton.Click += BrowseServerExecutableButton_Click;
            SaveServerPreferencesButton.Click += SaveServerPreferencesButton_Click;
            StartServerButton.Click += StartServerButton_Click;
            StopServerButton.Click += StopServerButton_Click;
            RestartServerButton.Click += RestartServerButton_Click;
            ForceStopServerButton.Click += ForceStopServerButton_Click;
        }

        private void WirePlayerEvents()
        {
            RefreshPlayersButton.Click += RefreshPlayersButton_Click;
            UpdatePlayersPageForUnavailableServer();
        }

        private async void RefreshPlayersButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshPlayersAsync();
        }

        private async Task RefreshPlayersAsync()
        {
            PlayersListView.ItemsSource = null;
            PlayersListView.Visibility = Visibility.Collapsed;
            NoPlayersPanel.Visibility = Visibility.Visible;
            PlayersOnlineCountText.Text = "—";

            if (!_serverProcessService.IsRunning)
            {
                UpdatePlayersPageForUnavailableServer();
                return;
            }

            if (_currentSettings is null)
            {
                PlayersStatusDot.Fill = GetBrush("WarningColor");
                PlayersStatusTitle.Text = "Settings file required";
                PlayersStatusDescription.Text =
                    "Load the active PalWorldSettings.ini before retrieving players.";
                NoPlayersText.Text =
                    "The manager needs the REST API port and Admin Password from the active server settings.";
                return;
            }

            if (!_currentSettings.RestApiEnabled)
            {
                PlayersStatusDot.Fill = GetBrush("WarningColor");
                PlayersStatusTitle.Text = "REST API disabled";
                PlayersStatusDescription.Text =
                    "Enable the REST API in Settings, save, and restart PalServer.";
                NoPlayersText.Text =
                    "Player information is retrieved through the Palworld REST API.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentSettings.AdminPassword))
            {
                PlayersStatusDot.Fill = GetBrush("WarningColor");
                PlayersStatusTitle.Text = "Admin Password required";
                PlayersStatusDescription.Text =
                    "Set an Admin Password in the active server configuration.";
                NoPlayersText.Text =
                    "Palworld requires REST API authentication to retrieve the player list.";
                return;
            }

            RefreshPlayersButton.IsEnabled = false;
            PlayersStatusDot.Fill = GetBrush("WarningColor");
            PlayersStatusTitle.Text = "Refreshing players...";
            PlayersStatusDescription.Text =
                "Reading the current player list from the Palworld REST API.";

            try
            {
                PalworldPlayersResponse response =
                    await _restApiService.GetPlayersAsync(
                        _currentSettings.RestApiPort,
                        _currentSettings.AdminPassword);

                var players = response.Players;

                PlayersOnlineCountText.Text =
                    $"{players.Count} / {_currentSettings.ServerPlayerMaxNum}";

                PlayersStatusDot.Fill =
                    GetBrush("SuccessColor");

                if (players.Count == 0)
                {
                    PlayersStatusTitle.Text =
                        "Server online — no players connected";

                    PlayersStatusDescription.Text =
                        "The REST API connection is working.";

                    NoPlayersText.Text =
                        "No players are currently connected to the server.";

                    AddActivity("Player list refreshed: 0 players online.");
                    return;
                }

                PlayersListView.ItemsSource =
                    players;

                PlayersListView.Visibility =
                    Visibility.Visible;

                NoPlayersPanel.Visibility =
                    Visibility.Collapsed;

                PlayersStatusTitle.Text =
                    $"{players.Count} player{(players.Count == 1 ? "" : "s")} online";

                PlayersStatusDescription.Text =
                    "Player information retrieved successfully.";

                AddActivity(
                    $"Player list refreshed: {players.Count} player{(players.Count == 1 ? "" : "s")} online.");
            }
            catch (Exception ex)
            {
                PlayersStatusDot.Fill =
                    GetBrush("WarningColor");

                PlayersStatusTitle.Text =
                    "Could not retrieve players";

                PlayersStatusDescription.Text =
                    ex.Message;

                NoPlayersText.Text =
                    "Check that PalServer is running and that the REST API settings match the active PalWorldSettings.ini.";

                AddActivity(
                    $"Player list refresh failed: {ex.Message}");
            }
            finally
            {
                RefreshPlayersButton.IsEnabled =
                    true;
            }
        }

        private void UpdatePlayersPageForUnavailableServer()
        {
            PlayersListView.ItemsSource = null;
            PlayersListView.Visibility = Visibility.Collapsed;
            NoPlayersPanel.Visibility = Visibility.Visible;

            PlayersStatusDot.Fill =
                GetBrush("WarningColor");

            PlayersStatusTitle.Text =
                "Server offline";

            PlayersStatusDescription.Text =
                "Start PalServer to view connected players.";

            PlayersOnlineCountText.Text =
                "—";

            NoPlayersText.Text =
                "The player list will be available while the Palworld server is running.";
        }

        private void WireConsoleEvents()
        {
            ConsoleListBox.ItemsSource = _activityLog;
            ClearConsoleButton.Click += ClearConsoleButton_Click;
            SendAnnouncementButton.Click += SendAnnouncementButton_Click;
        }

        private async void SendAnnouncementButton_Click(object sender, RoutedEventArgs e)
        {
            string message = AnnouncementTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show(
                    "Enter an announcement message first.",
                    "Announcement Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!_serverProcessService.IsRunning)
            {
                MessageBox.Show(
                    "The Palworld server is not running.",
                    "Server Offline",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!TryGetRestApiConnectionSettings(out int restApiPort, out string adminPassword))
            {
                return;
            }

            SendAnnouncementButton.IsEnabled = false;

            try
            {
                await _restApiService.AnnounceAsync(
                    restApiPort,
                    adminPassword,
                    message);

                AddActivity($"Announcement sent: {message}");
                AnnouncementTextBox.Clear();
            }
            catch (Exception ex)
            {
                AddActivity($"Announcement failed: {ex.Message}");

                MessageBox.Show(
                    "Could not send the announcement.\n\n" +
                    ex.Message,
                    "Announcement Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SendAnnouncementButton.IsEnabled = true;
            }
        }

        private void ClearConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            _activityLog.Clear();
            AddActivity("Console cleared.");
        }

        private void AddActivity(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _activityLog.Add(entry);

            if (ConsoleAutoScrollCheckBox.IsChecked == true &&
                _activityLog.Count > 0)
            {
                ConsoleListBox.ScrollIntoView(
                    _activityLog[_activityLog.Count - 1]);
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _serverStatusTimer.Stop();
            _serverProcessService.Dispose();
            _restApiService.Dispose();
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                !int.TryParse(button.Tag?.ToString(), out int selectedPage))
            {
                return;
            }

            MainNavigationTabControl.SelectedIndex = selectedPage;

            if (selectedPage == 2)
            {
                _ = RefreshPlayersAsync();
            }

            if (selectedPage == 4)
            {
                RefreshBackupList();
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
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
                LoadSettingsFile(dialog.FileName);
                MessageBox.Show(
                    "Settings loaded successfully.",
                    "Loaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SaveButton.IsEnabled = false;
                UpdateBackupPageForNoFile();
                MessageBox.Show(
                    $"Could not load the settings file.\n\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadSettingsFile(string filePath)
        {
            _settingsFilePath = filePath;
            _fileContents = _settingsService.LoadFileContents(filePath);
            _currentSettings = _settingsService.ParseSettings(_fileContents);

            PopulateEditor(_currentSettings);
            UpdateDashboard(_currentSettings);
            SaveButton.IsEnabled = true;
            StatusTextBlock.Text = $"Loaded: {_settingsFilePath}";
            RefreshBackupList();
            AddActivity($"Loaded settings: {filePath}");
        }

        private void PopulateEditor(ServerSettings settings)
        {
            ServerNameTextBox.Text = settings.ServerName;
            DescriptionTextBox.Text = settings.ServerDescription;
            MaxPlayersTextBox.Text = settings.ServerPlayerMaxNum.ToString(CultureInfo.InvariantCulture);
            AdminPasswordBox.Password = settings.AdminPassword;
            ServerPasswordBox.Password = settings.ServerPassword;

            ExpRateTextBox.Text = FormatEditorDecimal(settings.ExpRate);
            PalCaptureRateTextBox.Text = FormatEditorDecimal(settings.PalCaptureRate);
            PalSpawnRateTextBox.Text = FormatEditorDecimal(settings.PalSpawnNumRate);
            DayTimeSpeedTextBox.Text = FormatEditorDecimal(settings.DayTimeSpeedRate);
            NightTimeSpeedTextBox.Text = FormatEditorDecimal(settings.NightTimeSpeedRate);
            WorkSpeedTextBox.Text = FormatEditorDecimal(settings.WorkSpeedRate);

            PvpCheckBox.IsChecked = settings.IsPvP;
            HardcoreCheckBox.IsChecked = settings.IsHardcore;
            FriendlyFireCheckBox.IsChecked = settings.EnableFriendlyFire;
            CharacterRecreateCheckBox.IsChecked = settings.CharacterRecreateInHardcore;
            DeathPenaltyComboBox.Text = settings.DeathPenalty;

            BaseWorkersTextBox.Text = settings.BaseCampWorkerMaxNum.ToString(CultureInfo.InvariantCulture);
            GuildPlayersTextBox.Text = settings.GuildPlayerMaxNum.ToString(CultureInfo.InvariantCulture);
            BasesPerGuildTextBox.Text = settings.BaseCampMaxNumInGuild.ToString(CultureInfo.InvariantCulture);
            BaseCampMaxTextBox.Text = settings.BaseCampMaxNum.ToString(CultureInfo.InvariantCulture);

            PublicIpTextBox.Text = settings.PublicIP;
            PublicPortTextBox.Text = settings.PublicPort.ToString(CultureInfo.InvariantCulture);
            RconEnabledCheckBox.IsChecked = settings.RconEnabled;
            RconPortTextBox.Text = settings.RconPort.ToString(CultureInfo.InvariantCulture);
            RestApiEnabledCheckBox.IsChecked = settings.RestApiEnabled;
            RestApiPortTextBox.Text = settings.RestApiPort.ToString(CultureInfo.InvariantCulture);

            UseAuthCheckBox.IsChecked = settings.UseAuth;
            AllowClientModCheckBox.IsChecked = settings.AllowClientMod;
            BackupSaveDataCheckBox.IsChecked = settings.IsUseBackupSaveData;
            ShowPlayerListCheckBox.IsChecked = settings.ShowPlayerList;
            ChatPostLimitTextBox.Text = settings.ChatPostLimitPerMinute.ToString(CultureInfo.InvariantCulture);
        }

        private bool TryCreateSettingsFromEditor(out ServerSettings settings)
        {
            settings = new ServerSettings();

            if (!TryReadInteger(MaxPlayersTextBox.Text, "Maximum Players", 1, 1000, out int maximumPlayers) ||
                !TryReadDecimal(ExpRateTextBox.Text, "XP Rate", 0, 1000, out double expRate) ||
                !TryReadDecimal(PalCaptureRateTextBox.Text, "Pal Capture Rate", 0, 1000, out double captureRate) ||
                !TryReadDecimal(PalSpawnRateTextBox.Text, "Pal Spawn Rate", 0, 1000, out double spawnRate) ||
                !TryReadDecimal(DayTimeSpeedTextBox.Text, "Daytime Speed Rate", 0, 1000, out double daytimeSpeed) ||
                !TryReadDecimal(NightTimeSpeedTextBox.Text, "Nighttime Speed Rate", 0, 1000, out double nighttimeSpeed) ||
                !TryReadDecimal(WorkSpeedTextBox.Text, "Work Speed Rate", 0, 1000, out double workSpeed) ||
                !TryReadInteger(BaseWorkersTextBox.Text, "Workers Per Base", 1, 1000, out int workersPerBase) ||
                !TryReadInteger(GuildPlayersTextBox.Text, "Maximum Guild Players", 1, 1000, out int guildPlayers) ||
                !TryReadInteger(BasesPerGuildTextBox.Text, "Bases Per Guild", 1, 1000, out int basesPerGuild) ||
                !TryReadInteger(BaseCampMaxTextBox.Text, "Maximum Total Base Camps", 1, 10000, out int maximumBaseCamps) ||
                !TryReadInteger(PublicPortTextBox.Text, "Public Port", 1, 65535, out int publicPort) ||
                !TryReadInteger(RconPortTextBox.Text, "RCON Port", 1, 65535, out int rconPort) ||
                !TryReadInteger(RestApiPortTextBox.Text, "REST API Port", 1, 65535, out int restApiPort) ||
                !TryReadInteger(ChatPostLimitTextBox.Text, "Chat Post Limit", 1, 10000, out int chatPostLimit))
            {
                return false;
            }

            string deathPenalty = DeathPenaltyComboBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(deathPenalty))
            {
                ShowValidationError("Select a Death Penalty value.");
                return false;
            }

            settings = new ServerSettings
            {
                ServerName = ServerNameTextBox.Text,
                ServerDescription = DescriptionTextBox.Text,
                ServerPlayerMaxNum = maximumPlayers,
                AdminPassword = AdminPasswordBox.Password,
                ServerPassword = ServerPasswordBox.Password,
                ExpRate = expRate,
                PalCaptureRate = captureRate,
                PalSpawnNumRate = spawnRate,
                DayTimeSpeedRate = daytimeSpeed,
                NightTimeSpeedRate = nighttimeSpeed,
                WorkSpeedRate = workSpeed,
                IsPvP = PvpCheckBox.IsChecked == true,
                IsHardcore = HardcoreCheckBox.IsChecked == true,
                EnableFriendlyFire = FriendlyFireCheckBox.IsChecked == true,
                CharacterRecreateInHardcore = CharacterRecreateCheckBox.IsChecked == true,
                DeathPenalty = deathPenalty,
                BaseCampWorkerMaxNum = workersPerBase,
                GuildPlayerMaxNum = guildPlayers,
                BaseCampMaxNumInGuild = basesPerGuild,
                BaseCampMaxNum = maximumBaseCamps,
                PublicIP = PublicIpTextBox.Text.Trim(),
                PublicPort = publicPort,
                RconEnabled = RconEnabledCheckBox.IsChecked == true,
                RconPort = rconPort,
                RestApiEnabled = RestApiEnabledCheckBox.IsChecked == true,
                RestApiPort = restApiPort,
                UseAuth = UseAuthCheckBox.IsChecked == true,
                AllowClientMod = AllowClientModCheckBox.IsChecked == true,
                IsUseBackupSaveData = BackupSaveDataCheckBox.IsChecked == true,
                ShowPlayerList = ShowPlayerListCheckBox.IsChecked == true,
                ChatPostLimitPerMinute = chatPostLimit
            };

            return true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settingsFilePath))
            {
                MessageBox.Show("Load a settings file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!TryCreateSettingsFromEditor(out ServerSettings updatedSettings))
            {
                return;
            }

            try
            {
                string updatedContents = _settingsService.ApplySettings(_fileContents, updatedSettings);
                string backupPath = _settingsService.CreateBackup(_settingsFilePath);
                _settingsService.SaveFile(_settingsFilePath, updatedContents);

                _fileContents = updatedContents;
                _currentSettings = updatedSettings;
                UpdateDashboard(updatedSettings);
                RefreshBackupList();
                StatusTextBlock.Text = $"Saved: {_settingsFilePath}\nBackup: {backupPath}";
                AddActivity($"Settings saved. Backup created: {System.IO.Path.GetFileName(backupPath)}");

                MessageBox.Show(
                    $"Settings saved successfully.\n\nBackup created:\n{backupPath}",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save the settings file.\n\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDashboard(ServerSettings settings)
        {
            DashboardServerNameText.Text = string.IsNullOrWhiteSpace(settings.ServerName) ? "Unnamed Server" : settings.ServerName;
            DashboardPlayerLimitText.Text = settings.ServerPlayerMaxNum.ToString(CultureInfo.InvariantCulture);
            DashboardExpRateText.Text = $"{FormatDashboardDecimal(settings.ExpRate)}x";
            DashboardPvpText.Text = settings.IsPvP ? "Enabled" : "Disabled";
            DashboardPvpText.Foreground = settings.IsPvP ? GetBrush("SuccessColor") : GetBrush("MutedTextColor");
            DashboardHardcoreText.Text = settings.IsHardcore ? "Enabled" : "Disabled";
            DashboardHardcoreText.Foreground = settings.IsHardcore ? GetBrush("WarningColor") : GetBrush("MutedTextColor");
            DashboardGameModeText.Text = settings.GameMode;
            DashboardPortText.Text = settings.PublicPort.ToString(CultureInfo.InvariantCulture);
            DashboardStatusDot.Fill = GetBrush("SuccessColor");
            DashboardStatusTitle.Text = "Settings file loaded";
            DashboardStatusDescription.Text = _settingsFilePath ?? "";
        }

        // Server process controls
        private void PopulatePreferencesControls()
        {
            ServerExecutablePathTextBox.Text = _preferences.ServerExecutablePath;
            ServerLaunchArgumentsTextBox.Text = _preferences.LaunchArguments;
            AttachToRunningServerCheckBox.IsChecked = _preferences.AttachToRunningServerOnStartup;
        }

        private void BrowseServerExecutableButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "Select PalServer.exe",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                ServerExecutablePathTextBox.Text = dialog.FileName;
            }
        }

        private void SaveServerPreferencesButton_Click(object sender, RoutedEventArgs e)
        {
            SavePreferencesFromControls();
            MessageBox.Show("Server preferences saved.", "Preferences", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SavePreferencesFromControls()
        {
            _preferences = new AppPreferences
            {
                ServerExecutablePath = ServerExecutablePathTextBox.Text.Trim(),
                LaunchArguments = ServerLaunchArgumentsTextBox.Text.Trim(),
                AttachToRunningServerOnStartup = AttachToRunningServerCheckBox.IsChecked == true
            };
            _preferencesService.Save(_preferences);
            AddActivity("Server preferences saved.");
        }

        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SavePreferencesFromControls();
                _serverProcessService.StartServer(_preferences.ServerExecutablePath, _preferences.LaunchArguments);
                AddActivity("Palworld server started.");
                UpdateServerProcessDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start the server.\n\n{ex.Message}", "Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_serverProcessService.IsRunning)
            {
                return;
            }

            if (!TryGetRestApiConnectionSettings(out int restApiPort, out string adminPassword))
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "Save the world and gracefully shut down the Palworld server?\n\n" +
                "Players will receive a 10-second shutdown warning.",
                "Stop Server",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            SetServerControlButtonsEnabled(false);
            AddActivity("Graceful server shutdown requested.");
            ServerProcessStatusTitle.Text = "Stopping server...";
            ServerProcessStatusDescription.Text = "Saving the world before shutdown.";

            try
            {
                await _restApiService.SaveWorldAsync(
                    restApiPort,
                    adminPassword);
                AddActivity("World save completed through REST API.");

                ServerProcessStatusDescription.Text =
                    "World saved. Sending graceful shutdown request...";

                await _restApiService.ShutdownAsync(
                    restApiPort,
                    adminPassword,
                    10,
                    "Server shutting down in 10 seconds.");

                bool exited = await WaitForServerToExitAsync(
                    TimeSpan.FromSeconds(30));

                if (exited)
                {
                    AddActivity("Palworld server stopped gracefully.");
                }
                else
                {
                    AddActivity("Graceful shutdown timed out; server is still running.");
                    MessageBox.Show(
                        "Palworld accepted the shutdown request, but the server is still running.\n\n" +
                        "Wait a little longer before using Force Stop.",
                        "Server Still Running",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The graceful shutdown could not be completed.\n\n" +
                    $"{ex.Message}\n\n" +
                    "The server has NOT been force stopped.",
                    "Shutdown Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                UpdateServerProcessDisplay();
            }
        }

        private void ForceStopServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_serverProcessService.IsRunning)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "Force stopping can risk unsaved world data. Continue?",
                "Confirm Force Stop",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _serverProcessService.ForceStopServer();
                AddActivity("Server force stopped.");
                UpdateServerProcessDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not force stop the server.\n\n{ex.Message}", "Force Stop Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RestartServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_serverProcessService.IsRunning)
            {
                return;
            }

            if (!TryGetRestApiConnectionSettings(out int restApiPort, out string adminPassword))
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "Save the world and restart the Palworld server?\n\n" +
                "Players will receive a 10-second restart warning.",
                "Restart Server",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                SavePreferencesFromControls();

                SetServerControlButtonsEnabled(false);
                AddActivity("Safe server restart requested.");
                ServerProcessStatusTitle.Text = "Restarting server...";
                ServerProcessStatusDescription.Text = "Saving the world before restart.";

                await _restApiService.SaveWorldAsync(
                    restApiPort,
                    adminPassword);

                ServerProcessStatusDescription.Text =
                    "World saved. Sending graceful shutdown request...";

                await _restApiService.ShutdownAsync(
                    restApiPort,
                    adminPassword,
                    10,
                    "Server restarting in 10 seconds.");

                bool exited = await WaitForServerToExitAsync(
                    TimeSpan.FromSeconds(30));

                if (!exited)
                {
                    MessageBox.Show(
                        "The server did not exit within 30 seconds.\n\n" +
                        "Restart was cancelled. Use Force Stop only if necessary.",
                        "Restart Cancelled",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                ServerProcessStatusTitle.Text = "Starting server...";
                ServerProcessStatusDescription.Text =
                    "Palworld stopped safely. Starting it again...";

                await Task.Delay(1500);

                _serverProcessService.StartServer(
                    _preferences.ServerExecutablePath,
                    _preferences.LaunchArguments);
                AddActivity("Palworld server restarted successfully.");

                UpdateServerProcessDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not restart the server safely.\n\n" +
                    $"{ex.Message}",
                    "Restart Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                UpdateServerProcessDisplay();
            }
        }

        private bool TryGetRestApiConnectionSettings(
            out int port,
            out string adminPassword)
        {
            port = 0;
            adminPassword = "";

            if (_currentSettings is null)
            {
                MessageBox.Show(
                    "Load PalWorldSettings.ini first.\n\n" +
                    "The manager needs the REST API port and Admin Password from the settings file.",
                    "Settings Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return false;
            }

            if (!_currentSettings.RestApiEnabled)
            {
                MessageBox.Show(
                    "The Palworld REST API is disabled.\n\n" +
                    "Open Settings, enable REST API, save the settings, and restart the Palworld server.",
                    "REST API Disabled",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            port = _currentSettings.RestApiPort;
            adminPassword = _currentSettings.AdminPassword;

            if (port < 1 || port > 65535)
            {
                MessageBox.Show(
                    "The REST API port in PalWorldSettings.ini is invalid.",
                    "Invalid REST API Port",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                MessageBox.Show(
                    "An Admin Password is required for safe server control through the REST API.",
                    "Admin Password Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        private async Task<bool> WaitForServerToExitAsync(
            TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (!_serverProcessService.IsRunning)
                {
                    return true;
                }

                await Task.Delay(500);
            }

            return !_serverProcessService.IsRunning;
        }

        private void SetServerControlButtonsEnabled(bool enabled)
        {
            if (!enabled)
            {
                StartServerButton.IsEnabled = false;
                StopServerButton.IsEnabled = false;
                RestartServerButton.IsEnabled = false;
                ForceStopServerButton.IsEnabled = false;
                return;
            }

            UpdateServerProcessDisplay();
        }

        private void ServerStatusTimer_Tick(object? sender, EventArgs e)
        {
            UpdateServerProcessDisplay();
        }

        private void UpdateServerProcessDisplay()
        {
            bool running = _serverProcessService.IsRunning;

            ServerProcessStatusDot.Fill = running ? GetBrush("SuccessColor") : GetBrush("WarningColor");
            ServerProcessStatusTitle.Text = running ? "Server running" : "Server offline";
            ServerProcessStatusDescription.Text = running
                ? "PalServer is currently active."
                : "Select PalServer.exe and click Start Server.";

            ServerProcessIdText.Text = _serverProcessService.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? "—";
            ServerCpuUsageText.Text = running ? $"{_serverProcessService.GetCpuUsagePercent():0.0}%" : "—";
            ServerMemoryUsageText.Text = running ? FormatMemory(_serverProcessService.MemoryUsageBytes) : "—";
            ServerUptimeText.Text = running ? FormatUptime(_serverProcessService.Uptime) : "—";

            StartServerButton.IsEnabled = !running;
            StopServerButton.IsEnabled = running;
            RestartServerButton.IsEnabled = running;
            ForceStopServerButton.IsEnabled = running;
        }

        private static string FormatMemory(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 MB";
            }

            double megabytes = bytes / 1024d / 1024d;
            return megabytes >= 1024
                ? $"{megabytes / 1024d:0.00} GB"
                : $"{megabytes:0} MB";
        }

        private static string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
            {
                return $"{(int)uptime.TotalDays}d {uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
            }

            return $"{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
        }

        // Backup management
        private void RefreshBackupsButton_Click(object sender, RoutedEventArgs e) => RefreshBackupList();

        private void OpenBackupFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSettingsFileLoaded()) return;

            try
            {
                _backupService.OpenBackupFolder(_settingsFilePath!);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open the backup folder.\n\n{ex.Message}", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSettingsFileLoaded()) return;
            if (BackupListView.SelectedItem is not BackupItem selectedBackup)
            {
                MessageBox.Show("Select a backup to restore.", "No Backup Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"Restore the selected backup?\n\nThe current settings file will be backed up first.\n\nBackup:\n{selectedBackup.FileName}",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes) return;

            try
            {
                string safetyBackupPath = _backupService.RestoreBackup(_settingsFilePath!, selectedBackup);
                LoadSettingsFile(_settingsFilePath!);
                StatusTextBlock.Text = $"Backup restored successfully.\nSafety backup: {safetyBackupPath}";
                AddActivity($"Backup restored: {selectedBackup.FileName}");
                MessageBox.Show($"The backup was restored successfully.\n\nSafety backup created:\n{safetyBackupPath}", "Backup Restored", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not restore the selected backup.\n\n{ex.Message}", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (BackupListView.SelectedItem is not BackupItem selectedBackup)
            {
                MessageBox.Show("Select a backup to delete.", "No Backup Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                $"Permanently delete this backup?\n\n{selectedBackup.FileName}\n\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes) return;

            try
            {
                _backupService.DeleteBackup(selectedBackup);
                AddActivity($"Backup deleted: {selectedBackup.FileName}");
                RefreshBackupList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete the selected backup.\n\n{ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackupListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool selected = BackupListView.SelectedItem is BackupItem;
            RestoreBackupButton.IsEnabled = selected;
            DeleteBackupButton.IsEnabled = selected;
        }

        private void RefreshBackupList()
        {
            BackupListView.ItemsSource = null;
            RestoreBackupButton.IsEnabled = false;
            DeleteBackupButton.IsEnabled = false;

            if (string.IsNullOrWhiteSpace(_settingsFilePath))
            {
                UpdateBackupPageForNoFile();
                return;
            }

            try
            {
                var backups = _backupService.GetBackups(_settingsFilePath);
                BackupListView.ItemsSource = backups;
                bool hasBackups = backups.Count > 0;

                BackupListView.Visibility = hasBackups ? Visibility.Visible : Visibility.Collapsed;
                NoBackupsPanel.Visibility = hasBackups ? Visibility.Collapsed : Visibility.Visible;
                BackupStatusDot.Fill = GetBrush("SuccessColor");
                BackupStatusTitleText.Text = hasBackups ? $"{backups.Count} backup{(backups.Count == 1 ? "" : "s")} found" : "No backups found";
                BackupStatusDescriptionText.Text = hasBackups ? "Select a backup to restore or delete." : "Save your settings to create a timestamped backup.";
                NoBackupsText.Text = "Save the settings file to create your first timestamped backup.";
            }
            catch (Exception ex)
            {
                BackupListView.Visibility = Visibility.Collapsed;
                NoBackupsPanel.Visibility = Visibility.Visible;
                BackupStatusDot.Fill = GetBrush("WarningColor");
                BackupStatusTitleText.Text = "Could not load backups";
                BackupStatusDescriptionText.Text = ex.Message;
                NoBackupsText.Text = "An error occurred while reading the backup folder.";
            }
        }

        private void UpdateBackupPageForNoFile()
        {
            BackupListView.ItemsSource = null;
            BackupListView.Visibility = Visibility.Collapsed;
            NoBackupsPanel.Visibility = Visibility.Visible;
            RestoreBackupButton.IsEnabled = false;
            DeleteBackupButton.IsEnabled = false;
            BackupStatusDot.Fill = GetBrush("WarningColor");
            BackupStatusTitleText.Text = "No settings file loaded";
            BackupStatusDescriptionText.Text = "Load PalWorldSettings.ini before viewing backups.";
            NoBackupsText.Text = "Load a settings file to view its available backups.";
        }

        private bool EnsureSettingsFileLoaded()
        {
            if (!string.IsNullOrWhiteSpace(_settingsFilePath)) return true;
            MessageBox.Show("Load a PalWorldSettings.ini file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        // Validation and formatting
        private bool TryReadInteger(string text, string displayName, int minimum, int maximum, out int value)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                ShowValidationError($"{displayName} must be a whole number.");
                return false;
            }

            if (value < minimum || value > maximum)
            {
                ShowValidationError($"{displayName} must be between {minimum} and {maximum}.");
                return false;
            }

            return true;
        }

        private bool TryReadDecimal(string text, string displayName, double minimum, double maximum, out double value)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                ShowValidationError($"{displayName} must be a valid number.");
                return false;
            }

            if (value < minimum || value > maximum)
            {
                ShowValidationError($"{displayName} must be between {minimum} and {maximum}.");
                return false;
            }

            return true;
        }

        private void ShowValidationError(string message)
        {
            MessageBox.Show(message, "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static string FormatEditorDecimal(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
        private static string FormatDashboardDecimal(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        private Brush GetBrush(string resourceName) => (Brush)FindResource(resourceName);
    }
}