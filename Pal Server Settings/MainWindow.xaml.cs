using Microsoft.Win32;
using PalWorldServerManager.Models;
using PalWorldServerManager.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly DispatcherTimer _restartScheduleTimer;
        private readonly DispatcherTimer _scheduledBackupTimer;
        private readonly ObservableCollection<string> _activityLog = new();
        private bool _metricsRefreshInProgress;
        private int _serverStatusTickCount;
        private DateTime? _lastWorldSaveTime;
        private DateTime? _nextAutoSaveTime;
        private bool _autoSaveInProgress;
        private DateTime? _nextRestartTime;
        private bool _scheduledRestartInProgress;
        private readonly HashSet<string> _restartWarningsSent = new();
        private DateTime? _nextScheduledBackupTime;
        private bool _scheduledBackupInProgress;

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
            WireMetricsEvents();
            UpdateBackupPageForNoFile();

            _serverStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _serverStatusTimer.Tick += ServerStatusTimer_Tick;
            _serverStatusTimer.Start();

            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            _restartScheduleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _restartScheduleTimer.Tick += RestartScheduleTimer_Tick;

            _scheduledBackupTimer = new DispatcherTimer();
            _scheduledBackupTimer.Tick += ScheduledBackupTimer_Tick;

            WireSchedulerEvents();

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
            SaveWorldButton.Click += SaveWorldButton_Click;
        }

        private void WireMetricsEvents()
        {
            RefreshMetricsButton.Click += RefreshMetricsButton_Click;
            ResetLiveMetricsDisplay();
        }

        private async void RefreshMetricsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RefreshLiveMetricsAsync(
                logResult: true);
        }

        private async Task RefreshLiveMetricsAsync(
            bool logResult = false)
        {
            if (_metricsRefreshInProgress)
            {
                return;
            }

            if (!_serverProcessService.IsRunning ||
                _currentSettings is null ||
                !_currentSettings.RestApiEnabled ||
                string.IsNullOrWhiteSpace(_currentSettings.AdminPassword))
            {
                ResetLiveMetricsDisplay();
                return;
            }

            _metricsRefreshInProgress = true;
            RefreshMetricsButton.IsEnabled = false;

            try
            {
                PalworldServerMetrics metrics =
                    await _restApiService.GetMetricsAsync(
                        _currentSettings.RestApiPort,
                        _currentSettings.AdminPassword);

                LivePlayersText.Text =
                    $"{metrics.CurrentPlayerCount} / {metrics.MaximumPlayerCount}";

                LiveServerFpsText.Text =
                    metrics.ServerFps.ToString(
                        CultureInfo.InvariantCulture);

                LiveWorldUptimeText.Text =
                    FormatUptime(
                        TimeSpan.FromSeconds(
                            Math.Max(
                                0,
                                metrics.UptimeSeconds)));

                LiveWorldDayText.Text =
                    metrics.WorldDays.ToString(
                        CultureInfo.InvariantCulture);

                LiveBaseCountText.Text =
                    metrics.BaseCampCount.ToString(
                        CultureInfo.InvariantCulture);

                LiveMetricsStatusText.Text =
                    $"REST API connected • Frame time {metrics.ServerFrameTime:0.00} ms";

                if (logResult)
                {
                    AddActivity(
                        $"Metrics refreshed: {metrics.CurrentPlayerCount}/{metrics.MaximumPlayerCount} players, {metrics.ServerFps} FPS.");
                }
            }
            catch (Exception ex)
            {
                ResetLiveMetricsDisplay();

                LiveMetricsStatusText.Text =
                    $"Metrics unavailable: {ex.Message}";

                if (logResult)
                {
                    AddActivity(
                        $"Metrics refresh failed: {ex.Message}");
                }
            }
            finally
            {
                RefreshMetricsButton.IsEnabled = true;
                _metricsRefreshInProgress = false;
            }
        }

        private void ResetLiveMetricsDisplay()
        {
            LivePlayersText.Text = "—";
            LiveServerFpsText.Text = "—";
            LiveWorldUptimeText.Text = "—";
            LiveWorldDayText.Text = "—";
            LiveBaseCountText.Text = "—";

            LiveMetricsStatusText.Text =
                "Metrics unavailable until the server is running and REST API is connected.";
        }

        private void WirePlayerEvents()
        {
            RefreshPlayersButton.Click += RefreshPlayersButton_Click;
            PlayersListView.SelectionChanged += PlayersListView_SelectionChanged;
            KickPlayerButton.Click += KickPlayerButton_Click;
            BanPlayerButton.Click += BanPlayerButton_Click;
            UnbanPlayerButton.Click += UnbanPlayerButton_Click;
            UpdatePlayersPageForUnavailableServer();
        }

        private void PlayersListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            bool selected =
                PlayersListView.SelectedItem is PalworldPlayer;

            KickPlayerButton.IsEnabled =
                selected;

            BanPlayerButton.IsEnabled =
                selected;

            if (PlayersListView.SelectedItem is PalworldPlayer player)
            {
                SelectedPlayerText.Text =
                    $"Selected: {player.Name} ({player.UserId})";
            }
            else
            {
                SelectedPlayerText.Text =
                    "Select a player above to enable administration controls.";
            }
        }

        private async void KickPlayerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (PlayersListView.SelectedItem is not PalworldPlayer player)
            {
                return;
            }

            if (!TryGetRestApiConnectionSettings(
                    out int restApiPort,
                    out string adminPassword))
            {
                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    $"Kick {player.Name} from the server?\n\n" +
                    $"User ID: {player.UserId}",
                    "Confirm Kick",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                KickPlayerButton.IsEnabled = false;
                BanPlayerButton.IsEnabled = false;

                await _restApiService.KickPlayerAsync(
                    restApiPort,
                    adminPassword,
                    player.UserId,
                    "You were removed by a server administrator.");

                AddActivity(
                    $"Player kicked: {player.Name} ({player.UserId})");

                await Task.Delay(750);
                await RefreshPlayersAsync();
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Kick failed for {player.Name}: {ex.Message}");

                MessageBox.Show(
                    "Could not kick the selected player.\n\n" +
                    ex.Message,
                    "Kick Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                PlayersListView_SelectionChanged(
                    PlayersListView,
                    new SelectionChangedEventArgs(
                        Selector.SelectionChangedEvent,
                        Array.Empty<object>(),
                        Array.Empty<object>()));
            }
        }

        private async void BanPlayerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (PlayersListView.SelectedItem is not PalworldPlayer player)
            {
                return;
            }

            if (!TryGetRestApiConnectionSettings(
                    out int restApiPort,
                    out string adminPassword))
            {
                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    $"BAN {player.Name} from this server?\n\n" +
                    $"User ID: {player.UserId}\n\n" +
                    "This is more permanent than kicking the player.",
                    "Confirm Ban",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                KickPlayerButton.IsEnabled = false;
                BanPlayerButton.IsEnabled = false;

                await _restApiService.BanPlayerAsync(
                    restApiPort,
                    adminPassword,
                    player.UserId,
                    "You were banned by a server administrator.");

                AddActivity(
                    $"Player banned: {player.Name} ({player.UserId})");

                await Task.Delay(750);
                await RefreshPlayersAsync();
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Ban failed for {player.Name}: {ex.Message}");

                MessageBox.Show(
                    "Could not ban the selected player.\n\n" +
                    ex.Message,
                    "Ban Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                PlayersListView_SelectionChanged(
                    PlayersListView,
                    new SelectionChangedEventArgs(
                        Selector.SelectionChangedEvent,
                        Array.Empty<object>(),
                        Array.Empty<object>()));
            }
        }

        private async void UnbanPlayerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string userId =
                UnbanUserIdTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(userId))
            {
                MessageBox.Show(
                    "Enter the banned player's User ID first.",
                    "User ID Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!TryGetRestApiConnectionSettings(
                    out int restApiPort,
                    out string adminPassword))
            {
                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    $"Unban this player?\n\nUser ID: {userId}",
                    "Confirm Unban",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            UnbanPlayerButton.IsEnabled = false;

            try
            {
                await _restApiService.UnbanPlayerAsync(
                    restApiPort,
                    adminPassword,
                    userId);

                AddActivity(
                    $"Player unbanned: {userId}");

                UnbanUserIdTextBox.Clear();

                MessageBox.Show(
                    $"Player unbanned successfully.\n\nUser ID: {userId}",
                    "Player Unbanned",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Unban failed for {userId}: {ex.Message}");

                MessageBox.Show(
                    "Could not unban the player.\n\n" +
                    ex.Message,
                    "Unban Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                UnbanPlayerButton.IsEnabled = true;
            }
        }

        private async void RefreshPlayersButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshPlayersAsync();
        }

        private async Task RefreshPlayersAsync()
        {
            PlayersListView.SelectedItem = null;
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

        private void WireSchedulerEvents()
        {
            StartAutoSaveButton.Click += StartAutoSaveButton_Click;
            StopAutoSaveButton.Click += StopAutoSaveButton_Click;

            RestartScheduleComboBox.SelectionChanged += RestartScheduleComboBox_SelectionChanged;
            StartRestartScheduleButton.Click += StartRestartScheduleButton_Click;
            CancelRestartScheduleButton.Click += CancelRestartScheduleButton_Click;

            StartScheduledBackupButton.Click += StartScheduledBackupButton_Click;
            StopScheduledBackupButton.Click += StopScheduledBackupButton_Click;

            UpdateAutoSaveDisplay(false);
            UpdateRestartScheduleDisplay(false);
            UpdateScheduledBackupDisplay(false);
            RestartScheduleComboBox_SelectionChanged(
                RestartScheduleComboBox,
                new SelectionChangedEventArgs(
                    Selector.SelectionChangedEvent,
                    Array.Empty<object>(),
                    Array.Empty<object>()));
        }

        private void StartScheduledBackupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settingsFilePath))
            {
                MessageBox.Show(
                    "Load the active PalWorldSettings.ini first.",
                    "Settings File Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!TryGetSelectedScheduledBackupInterval(
                    out int intervalMinutes))
            {
                return;
            }

            _scheduledBackupTimer.Stop();

            _scheduledBackupTimer.Interval =
                TimeSpan.FromMinutes(
                    intervalMinutes);

            _nextScheduledBackupTime =
                DateTime.Now.AddMinutes(
                    intervalMinutes);

            _scheduledBackupTimer.Start();

            UpdateScheduledBackupDisplay(true);

            AddActivity(
                $"Configuration backup schedule enabled every {intervalMinutes} minutes.");
        }

        private void StopScheduledBackupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _scheduledBackupTimer.Stop();
            _nextScheduledBackupTime = null;

            UpdateScheduledBackupDisplay(false);

            AddActivity(
                "Configuration backup schedule disabled.");
        }

        private void ScheduledBackupTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (_scheduledBackupInProgress)
            {
                return;
            }

            _scheduledBackupInProgress = true;

            try
            {
                if (string.IsNullOrWhiteSpace(
                        _settingsFilePath))
                {
                    AddActivity(
                        "Scheduled configuration backup skipped because no settings file is loaded.");

                    ScheduleNextConfigurationBackup();
                    return;
                }

                string backupPath =
                    _settingsService.CreateBackup(
                        _settingsFilePath);

                AddActivity(
                    $"Scheduled configuration backup created: {System.IO.Path.GetFileName(backupPath)}");

                RefreshBackupList();
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Scheduled configuration backup failed: {ex.Message}");
            }
            finally
            {
                _scheduledBackupInProgress = false;
                ScheduleNextConfigurationBackup();
            }
        }

        private void ScheduleNextConfigurationBackup()
        {
            if (!_scheduledBackupTimer.IsEnabled)
            {
                return;
            }

            _nextScheduledBackupTime =
                DateTime.Now.Add(
                    _scheduledBackupTimer.Interval);

            UpdateScheduledBackupDisplay(true);
        }

        private bool TryGetSelectedScheduledBackupInterval(
            out int intervalMinutes)
        {
            intervalMinutes = 0;

            if (ScheduledBackupIntervalComboBox.SelectedItem
                    is not ComboBoxItem selectedItem ||
                !int.TryParse(
                    selectedItem.Tag?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out intervalMinutes) ||
                intervalMinutes <= 0)
            {
                MessageBox.Show(
                    "Select a valid backup interval.",
                    "Invalid Backup Interval",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        private void UpdateScheduledBackupDisplay(
            bool enabled)
        {
            StartScheduledBackupButton.IsEnabled =
                !enabled;

            StopScheduledBackupButton.IsEnabled =
                enabled;

            ScheduledBackupIntervalComboBox.IsEnabled =
                !enabled;

            ScheduledBackupStatusDot.Fill =
                enabled
                    ? GetBrush("SuccessColor")
                    : GetBrush("WarningColor");

            ScheduledBackupStatusTitle.Text =
                enabled
                    ? "Scheduled configuration backups enabled"
                    : "Scheduled configuration backups disabled";

            if (enabled &&
                _nextScheduledBackupTime.HasValue)
            {
                ScheduledBackupStatusDescription.Text =
                    $"PalWorldSettings.ini will be backed up every {_scheduledBackupTimer.Interval.TotalMinutes:0} minutes.";

                NextScheduledBackupText.Text =
                    _nextScheduledBackupTime.Value.ToString(
                        "MMM d h:mm tt",
                        CultureInfo.CurrentCulture);
            }
            else
            {
                ScheduledBackupStatusDescription.Text =
                    "Choose an interval and click Start Backup Schedule.";

                NextScheduledBackupText.Text =
                    "—";
            }
        }

        private void RestartScheduleComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            bool daily =
                RestartScheduleComboBox.SelectedItem
                    is ComboBoxItem item &&
                string.Equals(
                    item.Tag?.ToString(),
                    "daily",
                    StringComparison.OrdinalIgnoreCase);

            DailyRestartTimeTextBox.IsEnabled =
                daily &&
                !_restartScheduleTimer.IsEnabled;
        }

        private void StartRestartScheduleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryCalculateNextRestartTime(
                    out DateTime nextRestart))
            {
                return;
            }

            if (_currentSettings is null)
            {
                MessageBox.Show(
                    "Load the active PalWorldSettings.ini first.",
                    "Settings Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!_currentSettings.RestApiEnabled ||
                string.IsNullOrWhiteSpace(
                    _currentSettings.AdminPassword))
            {
                MessageBox.Show(
                    "Scheduled restarts require the Palworld REST API and an Admin Password.",
                    "REST API Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SavePreferencesFromControls();

            _restartWarningsSent.Clear();
            _nextRestartTime = nextRestart;
            _restartScheduleTimer.Start();

            UpdateRestartScheduleDisplay(true);

            AddActivity(
                $"Restart schedule enabled. Next restart: {_nextRestartTime:MMM d, yyyy h:mm:ss tt}.");
        }

        private void CancelRestartScheduleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _restartScheduleTimer.Stop();
            _scheduledBackupTimer.Stop();
            _nextRestartTime = null;
            _restartWarningsSent.Clear();

            UpdateRestartScheduleDisplay(false);

            AddActivity(
                "Scheduled restart cancelled.");
        }

        private async void RestartScheduleTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (!_nextRestartTime.HasValue ||
                _scheduledRestartInProgress)
            {
                return;
            }

            TimeSpan remaining =
                _nextRestartTime.Value -
                DateTime.Now;

            NextRestartText.Text =
                remaining.TotalSeconds > 0
                    ? FormatRestartCountdown(remaining)
                    : "Restarting...";

            await TrySendScheduledRestartWarningAsync(
                remaining,
                TimeSpan.FromMinutes(10),
                "10m",
                "Server restart in 10 minutes.");

            await TrySendScheduledRestartWarningAsync(
                remaining,
                TimeSpan.FromMinutes(5),
                "5m",
                "Server restart in 5 minutes.");

            await TrySendScheduledRestartWarningAsync(
                remaining,
                TimeSpan.FromMinutes(1),
                "1m",
                "Server restart in 1 minute.");

            await TrySendScheduledRestartWarningAsync(
                remaining,
                TimeSpan.FromSeconds(10),
                "10s",
                "Server restart in 10 seconds.");

            if (remaining.TotalSeconds <= 0)
            {
                await PerformScheduledRestartAsync();
            }
        }

        private async Task TrySendScheduledRestartWarningAsync(
            TimeSpan remaining,
            TimeSpan threshold,
            string key,
            string message)
        {
            if (_restartWarningsSent.Contains(key) ||
                remaining > threshold ||
                remaining.TotalSeconds <= 0)
            {
                return;
            }

            _restartWarningsSent.Add(key);

            if (!_serverProcessService.IsRunning)
            {
                return;
            }

            if (!TryGetRestApiConnectionSettings(
                    out int restApiPort,
                    out string adminPassword))
            {
                return;
            }

            try
            {
                await _restApiService.AnnounceAsync(
                    restApiPort,
                    adminPassword,
                    message);

                AddActivity(
                    $"Scheduled restart warning sent: {message}");
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Scheduled restart warning failed: {ex.Message}");
            }
        }

        private async Task PerformScheduledRestartAsync()
        {
            if (_scheduledRestartInProgress)
            {
                return;
            }

            _scheduledRestartInProgress = true;
            _restartScheduleTimer.Stop();

            try
            {
                if (!_serverProcessService.IsRunning)
                {
                    AddActivity(
                        "Scheduled restart reached its time while the server was offline.");

                    ScheduleNextRestart();
                    return;
                }

                if (!TryGetRestApiConnectionSettings(
                        out int restApiPort,
                        out string adminPassword))
                {
                    ScheduleNextRestart();
                    return;
                }

                AddActivity(
                    "Scheduled restart started. Saving world.");

                await _restApiService.SaveWorldAsync(
                    restApiPort,
                    adminPassword);

                _lastWorldSaveTime = DateTime.Now;

                LastWorldSaveText.Text =
                    $"Last World Save: {_lastWorldSaveTime:MMM d, yyyy h:mm:ss tt}";

                AddActivity(
                    "Scheduled restart world save completed.");

                await _restApiService.ShutdownAsync(
                    restApiPort,
                    adminPassword,
                    10,
                    "Server restarting in 10 seconds.");

                bool exited =
                    await WaitForServerToExitAsync(
                        TimeSpan.FromSeconds(30));

                if (!exited)
                {
                    AddActivity(
                        "Scheduled restart cancelled because PalServer did not exit within 30 seconds.");

                    MessageBox.Show(
                        "The scheduled restart could not finish because PalServer did not exit within 30 seconds.\n\n" +
                        "The app did not force stop the server.",
                        "Scheduled Restart Incomplete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    ScheduleNextRestart();
                    return;
                }

                await Task.Delay(1500);

                _serverProcessService.StartServer(
                    _preferences.ServerExecutablePath,
                    _preferences.LaunchArguments);

                AddActivity(
                    "Scheduled restart completed successfully.");

                UpdateServerProcessDisplay();

                ScheduleNextRestart();
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Scheduled restart failed: {ex.Message}");

                MessageBox.Show(
                    "Scheduled restart failed.\n\n" +
                    ex.Message,
                    "Scheduled Restart Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ScheduleNextRestart();
            }
            finally
            {
                _scheduledRestartInProgress = false;
            }
        }

        private void ScheduleNextRestart()
        {
            _restartWarningsSent.Clear();

            if (!TryCalculateNextRestartTime(
                    out DateTime nextRestart))
            {
                _nextRestartTime = null;
                UpdateRestartScheduleDisplay(false);
                return;
            }

            _nextRestartTime = nextRestart;
            _restartScheduleTimer.Start();

            UpdateRestartScheduleDisplay(true);
        }

        private bool TryCalculateNextRestartTime(
            out DateTime nextRestart)
        {
            nextRestart = default;

            if (RestartScheduleComboBox.SelectedItem
                is not ComboBoxItem selectedItem)
            {
                MessageBox.Show(
                    "Select a restart schedule.",
                    "Restart Schedule Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return false;
            }

            string tag =
                selectedItem.Tag?.ToString() ?? "";

            if (string.Equals(
                    tag,
                    "daily",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!TimeSpan.TryParseExact(
                        DailyRestartTimeTextBox.Text.Trim(),
                        @"hh\:mm",
                        CultureInfo.InvariantCulture,
                        out TimeSpan dailyTime))
                {
                    MessageBox.Show(
                        "Enter the daily restart time using 24-hour HH:mm format, for example 04:00 or 23:30.",
                        "Invalid Restart Time",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return false;
                }

                DateTime candidate =
                    DateTime.Today.Add(
                        dailyTime);

                if (candidate <= DateTime.Now)
                {
                    candidate =
                        candidate.AddDays(1);
                }

                nextRestart = candidate;
                return true;
            }

            if (!int.TryParse(
                    tag,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int intervalMinutes) ||
                intervalMinutes <= 0)
            {
                MessageBox.Show(
                    "The selected restart interval is invalid.",
                    "Invalid Restart Schedule",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            nextRestart =
                DateTime.Now.AddMinutes(
                    intervalMinutes);

            return true;
        }

        private void UpdateRestartScheduleDisplay(
            bool enabled)
        {
            StartRestartScheduleButton.IsEnabled =
                !enabled;

            CancelRestartScheduleButton.IsEnabled =
                enabled;

            RestartScheduleComboBox.IsEnabled =
                !enabled;

            bool daily =
                RestartScheduleComboBox.SelectedItem
                    is ComboBoxItem item &&
                string.Equals(
                    item.Tag?.ToString(),
                    "daily",
                    StringComparison.OrdinalIgnoreCase);

            DailyRestartTimeTextBox.IsEnabled =
                !enabled &&
                daily;

            RestartScheduleStatusDot.Fill =
                enabled
                    ? GetBrush("SuccessColor")
                    : GetBrush("WarningColor");

            RestartScheduleStatusTitle.Text =
                enabled
                    ? "Scheduled restarts enabled"
                    : "Scheduled restarts disabled";

            if (enabled &&
                _nextRestartTime.HasValue)
            {
                RestartScheduleStatusDescription.Text =
                    "Palworld will save the world, shut down gracefully, and start again automatically.";

                NextRestartText.Text =
                    _nextRestartTime.Value.ToString(
                        "MMM d h:mm tt",
                        CultureInfo.CurrentCulture);
            }
            else
            {
                RestartScheduleStatusDescription.Text =
                    "Choose a restart schedule and click Start Restart Schedule.";

                NextRestartText.Text =
                    "—";
            }
        }

        private static string FormatRestartCountdown(
            TimeSpan remaining)
        {
            if (remaining.TotalDays >= 1)
            {
                return $"{(int)remaining.TotalDays}d {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            }

            return $"{Math.Max(0, (int)remaining.TotalHours):00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        private void StartAutoSaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryGetSelectedAutoSaveInterval(
                    out int intervalMinutes))
            {
                return;
            }

            if (_currentSettings is null)
            {
                MessageBox.Show(
                    "Load the active PalWorldSettings.ini first.",
                    "Settings Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!_currentSettings.RestApiEnabled ||
                string.IsNullOrWhiteSpace(
                    _currentSettings.AdminPassword))
            {
                MessageBox.Show(
                    "Automatic saves require the Palworld REST API and an Admin Password.\n\n" +
                    "Enable the REST API in Settings, save, and restart PalServer.",
                    "REST API Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            _autoSaveTimer.Stop();
            _restartScheduleTimer.Stop();

            _autoSaveTimer.Interval =
                TimeSpan.FromMinutes(
                    intervalMinutes);

            _nextAutoSaveTime =
                DateTime.Now.AddMinutes(
                    intervalMinutes);

            _autoSaveTimer.Start();

            UpdateAutoSaveDisplay(true);

            AddActivity(
                $"Automatic world saves enabled every {intervalMinutes} minutes.");
        }

        private void StopAutoSaveButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _autoSaveTimer.Stop();
            _nextAutoSaveTime = null;

            UpdateAutoSaveDisplay(false);

            AddActivity(
                "Automatic world saves disabled.");
        }

        private async void AutoSaveTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (_autoSaveInProgress)
            {
                return;
            }

            if (!_serverProcessService.IsRunning)
            {
                AddActivity(
                    "Automatic world save skipped because the server is offline.");

                ScheduleNextAutoSave();
                return;
            }

            if (!TryGetRestApiConnectionSettings(
                    out int restApiPort,
                    out string adminPassword))
            {
                ScheduleNextAutoSave();
                return;
            }

            _autoSaveInProgress = true;

            try
            {
                await _restApiService.SaveWorldAsync(
                    restApiPort,
                    adminPassword);

                _lastWorldSaveTime =
                    DateTime.Now;

                LastWorldSaveText.Text =
                    $"Last World Save: {_lastWorldSaveTime:MMM d, yyyy h:mm:ss tt}";

                AddActivity(
                    $"Automatic world save completed at {_lastWorldSaveTime:h:mm:ss tt}.");
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Automatic world save failed: {ex.Message}");
            }
            finally
            {
                _autoSaveInProgress = false;
                ScheduleNextAutoSave();
            }
        }

        private void ScheduleNextAutoSave()
        {
            if (!_autoSaveTimer.IsEnabled)
            {
                return;
            }

            _nextAutoSaveTime =
                DateTime.Now.Add(
                    _autoSaveTimer.Interval);

            UpdateAutoSaveDisplay(true);
        }

        private bool TryGetSelectedAutoSaveInterval(
            out int intervalMinutes)
        {
            intervalMinutes = 0;

            if (AutoSaveIntervalComboBox.SelectedItem
                is not ComboBoxItem selectedItem ||
                !int.TryParse(
                    selectedItem.Tag?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out intervalMinutes) ||
                intervalMinutes <= 0)
            {
                MessageBox.Show(
                    "Select a valid automatic save interval.",
                    "Invalid Interval",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        private void UpdateAutoSaveDisplay(
            bool enabled)
        {
            StartAutoSaveButton.IsEnabled =
                !enabled;

            StopAutoSaveButton.IsEnabled =
                enabled;

            AutoSaveIntervalComboBox.IsEnabled =
                !enabled;

            AutoSaveStatusDot.Fill =
                enabled
                    ? GetBrush("SuccessColor")
                    : GetBrush("WarningColor");

            AutoSaveStatusTitle.Text =
                enabled
                    ? "Automatic saves enabled"
                    : "Automatic saves disabled";

            if (enabled &&
                _nextAutoSaveTime.HasValue)
            {
                AutoSaveStatusDescription.Text =
                    $"World saves will run every {_autoSaveTimer.Interval.TotalMinutes:0} minutes.";

                NextAutoSaveText.Text =
                    _nextAutoSaveTime.Value.ToString(
                        "h:mm:ss tt",
                        CultureInfo.CurrentCulture);
            }
            else
            {
                AutoSaveStatusDescription.Text =
                    "Choose an interval and click Start Auto Save.";

                NextAutoSaveText.Text =
                    "—";
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _serverStatusTimer.Stop();
            _autoSaveTimer.Stop();
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
            _ = RefreshLiveMetricsAsync();
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

        private async void SaveWorldButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_serverProcessService.IsRunning)
            {
                return;
            }

            if (!TryGetRestApiConnectionSettings(
                    out int restApiPort,
                    out string adminPassword))
            {
                return;
            }

            SaveWorldButton.IsEnabled = false;

            try
            {
                await _restApiService.SaveWorldAsync(
                    restApiPort,
                    adminPassword);

                _lastWorldSaveTime = DateTime.Now;

                LastWorldSaveText.Text =
                    $"Last World Save: {_lastWorldSaveTime:MMM d, yyyy h:mm:ss tt}";

                AddActivity(
                    $"World saved manually at {_lastWorldSaveTime:h:mm:ss tt}.");

                MessageBox.Show(
                    "World saved successfully.",
                    "World Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Manual world save failed: {ex.Message}");

                MessageBox.Show(
                    "Could not save the world.\n\n" +
                    ex.Message,
                    "Save World Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SaveWorldButton.IsEnabled =
                    _serverProcessService.IsRunning;
            }
        }

        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SavePreferencesFromControls();
                _serverProcessService.StartServer(_preferences.ServerExecutablePath, _preferences.LaunchArguments);
                AddActivity("Palworld server started.");
                UpdateServerProcessDisplay();
                _ = RefreshLiveMetricsAsync();
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
                _ = RefreshLiveMetricsAsync();
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

            _serverStatusTickCount++;

            if (_serverStatusTickCount >= 5)
            {
                _serverStatusTickCount = 0;
                _ = RefreshLiveMetricsAsync();
            }
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
            SaveWorldButton.IsEnabled = running;
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