using Microsoft.Win32;
using PalWorldServerManager.Models;
using PalWorldServerManager.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;

namespace PalWorldServerManager
{
    public partial class MainWindow : Window
    {
        private readonly PalworldSettingsService _settingsService = new();
        private readonly BackupService _backupService = new();
        private readonly WorldBackupService _worldBackupService = new();
        private readonly ServerModService _serverModService = new();
        private readonly ServerProcessService _serverProcessService = new();
        private readonly AppPreferencesService _preferencesService = new();
        private readonly PalworldRestApiService _restApiService = new();
        private readonly DispatcherTimer _serverStatusTimer;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly DispatcherTimer _restartScheduleTimer;
        private readonly DispatcherTimer _scheduledBackupTimer;
        private readonly DispatcherTimer _scheduledWorldBackupTimer;
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
        private DateTime? _nextScheduledWorldBackupTime;
        private bool _scheduledWorldBackupInProgress;
        private bool _playerRefreshInProgress;
        private int _playerRefreshTickCount;
        private string? _lastKnownPlayerIdsSignature;
        private string _lastHealthState = "Unknown";
        private DateTime? _lastHealthWarningTime;
        private DateTime? _lastCrashTime;
        private bool _previousServerRunning;
        private int _restApiFailureCount;
        private readonly DispatcherTimer _crashRecoveryTimer;
        private DateTime? _crashRecoveryTargetTime;
        private int _crashRecoveryAttemptCount;
        private int _crashCount;
        private bool _intentionalServerStopInProgress;
        private bool _crashRecoveryAttemptInProgress;

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
            WireModEvents();
            WireMetricsEvents();

            _serverProcessService.OutputReceived += ServerProcessService_OutputReceived;
            _serverProcessService.ErrorReceived += ServerProcessService_ErrorReceived;
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

            _scheduledWorldBackupTimer = new DispatcherTimer();
            _scheduledWorldBackupTimer.Tick += ScheduledWorldBackupTimer_Tick;

            _crashRecoveryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _crashRecoveryTimer.Tick += CrashRecoveryTimer_Tick;

            WireSchedulerEvents();

            if (_preferences.AttachToRunningServerOnStartup)
            {
                _serverProcessService.AttachToRunningServer();
            }

            RestoreLastSettingsFile();
            RestoreSchedulerPreferences();

            UpdateServerProcessDisplay();
            _previousServerRunning = _serverProcessService.IsRunning;
            UpdateServerHealthDisplay(
                "Unknown",
                "Health monitoring will begin when PalServer is running.",
                false);

            WireCrashRecoveryEvents();
            RestoreCrashRecoveryPreferences();

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

            BackupWorldNowButton.Click += BackupWorldNowButton_Click;
            RefreshWorldBackupsButton.Click += RefreshWorldBackupsButton_Click;
            OpenWorldSaveFolderButton.Click += OpenWorldSaveFolderButton_Click;
            OpenWorldBackupFolderButton.Click += OpenWorldBackupFolderButton_Click;
            RestoreWorldBackupButton.Click += RestoreWorldBackupButton_Click;
            DeleteWorldBackupButton.Click += DeleteWorldBackupButton_Click;
            WorldBackupListView.SelectionChanged += WorldBackupListView_SelectionChanged;
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

        private void WireModEvents()
        {
            InstallModButton.Click += InstallModButton_Click;
            RefreshModsButton.Click += RefreshModsButton_Click;
            OpenModsFolderButton.Click += OpenModsFolderButton_Click;
            EnableModButton.Click += EnableModButton_Click;
            DisableModButton.Click += DisableModButton_Click;
            RemoveModButton.Click += RemoveModButton_Click;
            ModsListView.SelectionChanged += ModsListView_SelectionChanged;

            UpdateModsPageForUnavailablePath();
        }

        private void InstallModButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_serverProcessService.IsRunning)
            {
                MessageBox.Show(
                    "Stop PalServer before installing a mod.",
                    "Server Must Be Stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SavePreferencesFromControls();

            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Title =
                        "Select Palworld PAK mod",

                    Filter =
                        "Palworld PAK files (*.pak)|*.pak|" +
                        "All files (*.*)|*.*",

                    Multiselect =
                        false
                };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                string installedPath =
                    _serverModService.InstallPakMod(
                        _preferences.ServerExecutablePath,
                        dialog.FileName);

                AddActivity(
                    $"Mod installed: {System.IO.Path.GetFileName(installedPath)}");

                RefreshModsList();

                MessageBox.Show(
                    "Mod installed successfully.\n\n" +
                    installedPath,
                    "Mod Installed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Mod install failed: {ex.Message}");

                MessageBox.Show(
                    "Could not install the selected mod.\n\n" +
                    ex.Message,
                    "Install Mod Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshModsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshModsList();
        }

        private void OpenModsFolderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SavePreferencesFromControls();

            try
            {
                _serverModService.OpenPakDirectory(
                    _preferences.ServerExecutablePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open the Palworld Paks folder.\n\n" +
                    ex.Message,
                    "Mods Folder Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ModsListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ModsListView.SelectedItem
                is not ServerModItem selectedMod)
            {
                SelectedModText.Text =
                    "Select a mod above to enable or disable it.";

                EnableModButton.IsEnabled =
                    false;

                DisableModButton.IsEnabled =
                    false;

                RemoveModButton.IsEnabled =
                    false;

                return;
            }

            SelectedModText.Text =
                $"Selected: {selectedMod.DisplayName} ({selectedMod.StatusDisplay})";

            bool canModify =
                !_serverProcessService.IsRunning;

            EnableModButton.IsEnabled =
                canModify &&
                !selectedMod.IsEnabled;

            DisableModButton.IsEnabled =
                canModify &&
                selectedMod.IsEnabled;

            RemoveModButton.IsEnabled =
                canModify;
        }

        private void EnableModButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SetSelectedModEnabled(
                true);
        }

        private void DisableModButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            SetSelectedModEnabled(
                false);
        }

        private void RemoveModButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_serverProcessService.IsRunning)
            {
                MessageBox.Show(
                    "Stop PalServer before removing a mod.",
                    "Server Must Be Stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (ModsListView.SelectedItem
                is not ServerModItem selectedMod)
            {
                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    "Permanently remove this mod from the server?\n\n" +
                    $"{selectedMod.DisplayName}\n\n" +
                    "This deletes the .pak file from the server's Paks folder.",
                    "Remove Mod",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _serverModService.RemoveMod(
                    selectedMod);

                AddActivity(
                    $"Mod removed: {selectedMod.DisplayName}");

                RefreshModsList();
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Mod removal failed for {selectedMod.DisplayName}: {ex.Message}");

                MessageBox.Show(
                    "Could not remove the selected mod.\n\n" +
                    ex.Message,
                    "Remove Mod Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SetSelectedModEnabled(
            bool enabled)
        {
            if (_serverProcessService.IsRunning)
            {
                MessageBox.Show(
                    "Stop PalServer before changing mod state.",
                    "Server Must Be Stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (ModsListView.SelectedItem
                is not ServerModItem selectedMod)
            {
                return;
            }

            string action =
                enabled
                    ? "enable"
                    : "disable";

            MessageBoxResult result =
                MessageBox.Show(
                    $"{char.ToUpperInvariant(action[0]) + action[1..]} this mod?\n\n" +
                    $"{selectedMod.DisplayName}\n\n" +
                    "The server should remain stopped until the mod list is ready.",
                    $"{(enabled ? "Enable" : "Disable")} Mod",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _serverModService.SetEnabled(
                    selectedMod,
                    enabled);

                AddActivity(
                    $"Mod {(enabled ? "enabled" : "disabled")}: {selectedMod.DisplayName}");

                RefreshModsList();
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Mod {action} failed for {selectedMod.DisplayName}: {ex.Message}");

                MessageBox.Show(
                    $"Could not {action} the selected mod.\n\n" +
                    ex.Message,
                    "Mod Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshModsList()
        {
            ModsListView.SelectedItem =
                null;

            ModsListView.ItemsSource =
                null;

            ModsListView.Visibility =
                Visibility.Collapsed;

            NoModsPanel.Visibility =
                Visibility.Visible;

            ModsCountText.Text =
                "—";

            SavePreferencesFromControls();

            try
            {
                var mods =
                    _serverModService.GetInstalledMods(
                        _preferences.ServerExecutablePath);

                ModsCountText.Text =
                    mods.Count.ToString(
                        CultureInfo.InvariantCulture);

                ModsStatusDot.Fill =
                    GetBrush("SuccessColor");

                if (mods.Count == 0)
                {
                    ModsStatusTitle.Text =
                        "No PAK mods installed";

                    ModsStatusDescription.Text =
                        "The server Paks folder was found successfully.";

                    NoModsText.Text =
                        "PAK mods placed in Pal\\Content\\Paks will appear here.";

                    return;
                }

                ModsListView.ItemsSource =
                    mods;

                ModsListView.Visibility =
                    Visibility.Visible;

                NoModsPanel.Visibility =
                    Visibility.Collapsed;

                int enabledCount =
                    mods.Count(
                        mod =>
                            mod.IsEnabled);

                ModsStatusTitle.Text =
                    $"{mods.Count} mod{(mods.Count == 1 ? "" : "s")} found";

                ModsStatusDescription.Text =
                    $"{enabledCount} enabled • {mods.Count - enabledCount} disabled";
            }
            catch (Exception ex)
            {
                ModsStatusDot.Fill =
                    GetBrush("WarningColor");

                ModsStatusTitle.Text =
                    "Mods unavailable";

                ModsStatusDescription.Text =
                    ex.Message;

                NoModsText.Text =
                    "Select a valid PalServer.exe on the Dashboard.";
            }
        }

        private void UpdateModsPageForUnavailablePath()
        {
            ModsListView.ItemsSource =
                null;

            ModsListView.Visibility =
                Visibility.Collapsed;

            NoModsPanel.Visibility =
                Visibility.Visible;

            ModsStatusDot.Fill =
                GetBrush("WarningColor");

            ModsStatusTitle.Text =
                "Mods not loaded";

            ModsStatusDescription.Text =
                "Select PalServer.exe on the Dashboard to locate the Paks folder.";

            ModsCountText.Text =
                "—";

            SelectedModText.Text =
                "Select a mod above to enable or disable it.";

            EnableModButton.IsEnabled =
                false;

            DisableModButton.IsEnabled =
                false;

            RemoveModButton.IsEnabled =
                false;
        }

        private void WireCrashRecoveryEvents()
        {
            EnableCrashRecoveryCheckBox.Checked += CrashRecoverySettings_Changed;
            EnableCrashRecoveryCheckBox.Unchecked += CrashRecoverySettings_Changed;
            CrashRecoveryDelayComboBox.SelectionChanged += CrashRecoverySettings_Changed;
            CrashRecoveryMaxAttemptsComboBox.SelectionChanged += CrashRecoverySettings_Changed;
        }

        private void RestoreCrashRecoveryPreferences()
        {
            EnableCrashRecoveryCheckBox.IsChecked =
                _preferences.CrashRecoveryEnabled;

            SelectComboBoxItemByTag(
                CrashRecoveryDelayComboBox,
                Math.Max(
                    10,
                    _preferences.CrashRecoveryDelaySeconds)
                    .ToString(
                        CultureInfo.InvariantCulture));

            SelectComboBoxItemByTag(
                CrashRecoveryMaxAttemptsComboBox,
                Math.Max(
                    1,
                    _preferences.CrashRecoveryMaxAttempts)
                    .ToString(
                        CultureInfo.InvariantCulture));

            UpdateCrashRecoveryStatus();
        }

        private void CrashRecoverySettings_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_preferences is null)
            {
                return;
            }

            _preferences.CrashRecoveryEnabled =
                EnableCrashRecoveryCheckBox.IsChecked == true;

            if (CrashRecoveryDelayComboBox.SelectedItem
                    is ComboBoxItem delayItem &&
                int.TryParse(
                    delayItem.Tag?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int delaySeconds))
            {
                _preferences.CrashRecoveryDelaySeconds =
                    delaySeconds;
            }

            if (CrashRecoveryMaxAttemptsComboBox.SelectedItem
                    is ComboBoxItem attemptsItem &&
                int.TryParse(
                    attemptsItem.Tag?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int maxAttempts))
            {
                _preferences.CrashRecoveryMaxAttempts =
                    maxAttempts;
            }

            _preferencesService.Save(
                _preferences);

            if (!_preferences.CrashRecoveryEnabled)
            {
                CancelCrashRecovery();
            }

            UpdateCrashRecoveryStatus();
        }

        private void ScheduleCrashRecovery()
        {
            if (!_preferences.CrashRecoveryEnabled ||
                _intentionalServerStopInProgress ||
                _crashRecoveryAttemptInProgress)
            {
                return;
            }

            _crashRecoveryAttemptCount =
                0;

            _crashRecoveryTargetTime =
                DateTime.Now.AddSeconds(
                    Math.Max(
                        1,
                        _preferences.CrashRecoveryDelaySeconds));

            _crashRecoveryTimer.Start();

            UpdateCrashRecoveryStatus();

            AddActivity(
                $"Automatic crash recovery scheduled for {_crashRecoveryTargetTime:h:mm:ss tt}.");
        }

        private void CancelCrashRecovery()
        {
            _crashRecoveryTimer.Stop();

            _crashRecoveryTargetTime =
                null;

            _crashRecoveryAttemptCount =
                0;

            _crashRecoveryAttemptInProgress =
                false;

            UpdateCrashRecoveryStatus();
        }

        private async void CrashRecoveryTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (!_preferences.CrashRecoveryEnabled ||
                _intentionalServerStopInProgress)
            {
                CancelCrashRecovery();
                return;
            }

            if (_serverProcessService.IsRunning)
            {
                CancelCrashRecovery();
                return;
            }

            if (!_crashRecoveryTargetTime.HasValue)
            {
                return;
            }

            TimeSpan remaining =
                _crashRecoveryTargetTime.Value -
                DateTime.Now;

            if (remaining.TotalSeconds > 0)
            {
                CrashRecoveryStatusText.Text =
                    $"Recovery attempt {_crashRecoveryAttemptCount + 1} will start in {Math.Ceiling(remaining.TotalSeconds):0} seconds.";

                return;
            }

            if (_crashRecoveryAttemptInProgress)
            {
                return;
            }

            int maxAttempts =
                Math.Max(
                    1,
                    _preferences.CrashRecoveryMaxAttempts);

            if (_crashRecoveryAttemptCount >=
                maxAttempts)
            {
                _crashRecoveryTimer.Stop();

                _crashRecoveryTargetTime =
                    null;

                CrashRecoveryStatusText.Text =
                    $"Automatic recovery stopped after {maxAttempts} failed attempt{(maxAttempts == 1 ? "" : "s")}.";

                AddActivity(
                    $"CRASH RECOVERY FAILED: maximum of {maxAttempts} attempt{(maxAttempts == 1 ? "" : "s")} reached.");

                return;
            }

            _crashRecoveryAttemptInProgress =
                true;

            _crashRecoveryAttemptCount++;

            try
            {
                SavePreferencesFromControls();

                AddActivity(
                    $"Crash recovery attempt {_crashRecoveryAttemptCount} of {maxAttempts} starting.");

                _serverProcessService.StartServer(
                    _preferences.ServerExecutablePath,
                    _preferences.LaunchArguments);

                await Task.Delay(
                    2000);

                if (!_serverProcessService.IsRunning)
                {
                    throw new InvalidOperationException(
                        "PalServer exited immediately after the recovery start attempt.");
                }

                _previousServerRunning =
                    true;

                _crashRecoveryTimer.Stop();

                _crashRecoveryTargetTime =
                    null;

                LastRecoveryText.Text =
                    DateTime.Now.ToString(
                        "MMM d, yyyy h:mm:ss tt",
                        CultureInfo.CurrentCulture);

                CrashRecoveryStatusText.Text =
                    $"Recovered successfully on attempt {_crashRecoveryAttemptCount}.";

                AddActivity(
                    $"CRASH RECOVERY SUCCESS: PalServer restarted on attempt {_crashRecoveryAttemptCount}.");

                UpdateServerProcessDisplay();
                _ = RefreshLiveMetricsAsync();
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Crash recovery attempt {_crashRecoveryAttemptCount} failed: {ex.Message}");

                if (_crashRecoveryAttemptCount <
                    maxAttempts)
                {
                    _crashRecoveryTargetTime =
                        DateTime.Now.AddSeconds(
                            Math.Max(
                                1,
                                _preferences.CrashRecoveryDelaySeconds));

                    CrashRecoveryStatusText.Text =
                        $"Recovery attempt {_crashRecoveryAttemptCount} failed. Retrying shortly.";
                }
                else
                {
                    _crashRecoveryTargetTime =
                        DateTime.Now;

                    CrashRecoveryStatusText.Text =
                        $"Recovery attempt {_crashRecoveryAttemptCount} failed.";
                }
            }
            finally
            {
                _crashRecoveryAttemptInProgress =
                    false;
            }
        }

        private void UpdateCrashRecoveryStatus()
        {
            CrashCountText.Text =
                _crashCount.ToString(
                    CultureInfo.InvariantCulture);

            if (!_preferences.CrashRecoveryEnabled)
            {
                CrashRecoveryStatusText.Text =
                    "Crash recovery is disabled.";

                return;
            }

            if (_crashRecoveryTargetTime.HasValue)
            {
                CrashRecoveryStatusText.Text =
                    $"Crash recovery is armed. Next attempt: {_crashRecoveryTargetTime:h:mm:ss tt}.";

                return;
            }

            CrashRecoveryStatusText.Text =
                $"Crash recovery enabled • {_preferences.CrashRecoveryDelaySeconds}s delay • up to {_preferences.CrashRecoveryMaxAttempts} attempt{(_preferences.CrashRecoveryMaxAttempts == 1 ? "" : "s")}.";
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

                _restApiFailureCount = 0;

                EvaluateServerHealth(
                    metrics);

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

                _restApiFailureCount++;

                if (_serverProcessService.IsRunning &&
                    _restApiFailureCount >= 2)
                {
                    UpdateServerHealthDisplay(
                        "Warning",
                        "PalServer is running, but the REST API is not responding.",
                        true);
                }

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
                await RefreshPlayersAsync(
                    preserveSelection: false,
                    logResult: false);
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
                await RefreshPlayersAsync(
                    preserveSelection: false,
                    logResult: false);
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
            await RefreshPlayersAsync(
                preserveSelection: true,
                logResult: true);
        }

        private async Task RefreshPlayersAsync(
            bool preserveSelection = false,
            bool logResult = true)
        {
            if (_playerRefreshInProgress)
            {
                return;
            }

            _playerRefreshInProgress = true;

            string? selectedUserId =
                preserveSelection &&
                PlayersListView.SelectedItem is PalworldPlayer selectedPlayer
                    ? selectedPlayer.UserId
                    : null;

            if (!preserveSelection)
            {
                PlayersListView.SelectedItem = null;
            }

            PlayersListView.ItemsSource = null;
            PlayersListView.Visibility = Visibility.Collapsed;
            NoPlayersPanel.Visibility = Visibility.Visible;
            PlayersOnlineCountText.Text = "—";

            if (!_serverProcessService.IsRunning)
            {
                UpdatePlayersPageForUnavailableServer();
                _playerRefreshInProgress = false;
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

                _playerRefreshInProgress = false;
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

                _playerRefreshInProgress = false;
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

                _playerRefreshInProgress = false;
                return;
            }

            RefreshPlayersButton.IsEnabled = false;

            if (logResult)
            {
                PlayersStatusDot.Fill = GetBrush("WarningColor");
                PlayersStatusTitle.Text = "Refreshing players...";
                PlayersStatusDescription.Text =
                    "Reading the current player list from the Palworld REST API.";
            }

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
                        "Live player monitoring is active.";

                    NoPlayersText.Text =
                        "No players are currently connected to the server.";

                    string emptySignature = "";

                    if (_lastKnownPlayerIdsSignature != emptySignature)
                    {
                        if (_lastKnownPlayerIdsSignature is not null)
                        {
                            AddActivity(
                                "All players have left the server.");
                        }

                        _lastKnownPlayerIdsSignature =
                            emptySignature;
                    }

                    if (logResult)
                    {
                        AddActivity(
                            "Player list refreshed: 0 players online.");
                    }

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
                    "Live player monitoring is active.";

                if (!string.IsNullOrWhiteSpace(selectedUserId))
                {
                    foreach (PalworldPlayer player in players)
                    {
                        if (string.Equals(
                                player.UserId,
                                selectedUserId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            PlayersListView.SelectedItem =
                                player;

                            break;
                        }
                    }
                }

                string signature =
                    string.Join(
                        "|",
                        players
                            .Select(player => player.UserId)
                            .OrderBy(userId => userId, StringComparer.OrdinalIgnoreCase));

                if (_lastKnownPlayerIdsSignature is not null &&
                    !string.Equals(
                        _lastKnownPlayerIdsSignature,
                        signature,
                        StringComparison.Ordinal))
                {
                    AddActivity(
                        $"Player list changed: {players.Count} player{(players.Count == 1 ? "" : "s")} online.");
                }

                _lastKnownPlayerIdsSignature =
                    signature;

                if (logResult)
                {
                    AddActivity(
                        $"Player list refreshed: {players.Count} player{(players.Count == 1 ? "" : "s")} online.");
                }
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

                if (logResult)
                {
                    AddActivity(
                        $"Player list refresh failed: {ex.Message}");
                }
            }
            finally
            {
                RefreshPlayersButton.IsEnabled =
                    true;

                _playerRefreshInProgress =
                    false;
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

            _lastKnownPlayerIdsSignature =
                null;

            NoPlayersText.Text =
                "The player list will be available while the Palworld server is running.";
        }

        private void ServerProcessService_OutputReceived(
            object? sender,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Dispatcher.Invoke(
                () =>
                {
                    AddActivity(
                        $"SERVER: {message}");
                });
        }

        private void ServerProcessService_ErrorReceived(
            object? sender,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Dispatcher.Invoke(
                () =>
                {
                    AddActivity(
                        $"SERVER ERROR: {message}");
                });
        }

        private void WireConsoleEvents()
        {
            ConsoleListBox.ItemsSource = _activityLog;
            ClearConsoleButton.Click += ClearConsoleButton_Click;
            SendAnnouncementButton.Click += SendAnnouncementButton_Click;
            RunConsoleCommandButton.Click += RunConsoleCommandButton_Click;
            ConsoleCommandTextBox.KeyDown += ConsoleCommandTextBox_KeyDown;
        }

        private void ConsoleCommandTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;

            RunConsoleCommandButton_Click(
                RunConsoleCommandButton,
                new RoutedEventArgs());
        }

        private async void RunConsoleCommandButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string commandLine =
                ConsoleCommandTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(
                    commandLine))
            {
                return;
            }

            AddActivity(
                $"> {commandLine}");

            ConsoleCommandTextBox.Clear();

            string[] parts =
                commandLine.Split(
                    ' ',
                    2,
                    StringSplitOptions.RemoveEmptyEntries);

            string command =
                parts[0].ToLowerInvariant();

            string argument =
                parts.Length > 1
                    ? parts[1].Trim()
                    : "";

            try
            {
                switch (command)
                {
                    case "save":
                        await ExecuteConsoleSaveAsync();
                        break;

                    case "announce":
                        await ExecuteConsoleAnnouncementAsync(
                            argument);
                        break;

                    case "players":
                        await RefreshPlayersAsync(
                            preserveSelection: true,
                            logResult: true);
                        break;

                    case "restart":
                        RestartServerButton_Click(
                            RestartServerButton,
                            new RoutedEventArgs());
                        break;

                    case "stop":
                        StopServerButton_Click(
                            StopServerButton,
                            new RoutedEventArgs());
                        break;

                    case "clear":
                        _activityLog.Clear();
                        AddActivity(
                            "Console cleared.");
                        break;

                    default:
                        AddActivity(
                            $"Unknown command: {command}");

                        MessageBox.Show(
                            "Unknown command.\n\n" +
                            "Available commands:\n" +
                            "save\n" +
                            "announce <message>\n" +
                            "players\n" +
                            "restart\n" +
                            "stop\n" +
                            "clear",
                            "Unknown Command",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        break;
                }
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Command failed: {ex.Message}");
            }
        }

        private async Task ExecuteConsoleSaveAsync()
        {
            if (!_serverProcessService.IsRunning)
            {
                AddActivity(
                    "Save command failed: server is offline.");

                return;
            }

            if (!TryGetRestApiConnectionSettings(
                    out int restApiPort,
                    out string adminPassword))
            {
                return;
            }

            await _restApiService.SaveWorldAsync(
                restApiPort,
                adminPassword);

            _lastWorldSaveTime =
                DateTime.Now;

            LastWorldSaveText.Text =
                $"Last World Save: {_lastWorldSaveTime:MMM d, yyyy h:mm:ss tt}";

            AddActivity(
                $"World saved from Console at {_lastWorldSaveTime:h:mm:ss tt}.");
        }

        private async Task ExecuteConsoleAnnouncementAsync(
            string message)
        {
            if (string.IsNullOrWhiteSpace(
                    message))
            {
                AddActivity(
                    "Usage: announce <message>");

                return;
            }

            if (!_serverProcessService.IsRunning)
            {
                AddActivity(
                    "Announcement failed: server is offline.");

                return;
            }

            if (!TryGetRestApiConnectionSettings(
                    out int restApiPort,
                    out string adminPassword))
            {
                return;
            }

            await _restApiService.AnnounceAsync(
                restApiPort,
                adminPassword,
                message);

            AddActivity(
                $"Announcement sent from Console: {message}");
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

        private void RestoreLastSettingsFile()
        {
            if (string.IsNullOrWhiteSpace(
                    _preferences.LastSettingsFilePath))
            {
                return;
            }

            if (!System.IO.File.Exists(
                    _preferences.LastSettingsFilePath))
            {
                AddActivity(
                    $"Last settings file was not found: {_preferences.LastSettingsFilePath}");

                _preferences.LastSettingsFilePath = "";
                _preferencesService.Save(_preferences);
                return;
            }

            try
            {
                LoadSettingsFile(
                    _preferences.LastSettingsFilePath);

                AddActivity(
                    "Last settings file loaded automatically.");
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Automatic settings load failed: {ex.Message}");
            }
        }

        private void RestoreSchedulerPreferences()
        {
            SelectComboBoxItemByTag(
                AutoSaveIntervalComboBox,
                _preferences.AutoSaveIntervalMinutes.ToString(
                    CultureInfo.InvariantCulture));

            SelectComboBoxItemByTag(
                ScheduledBackupIntervalComboBox,
                _preferences.ScheduledBackupIntervalMinutes.ToString(
                    CultureInfo.InvariantCulture));

            SelectComboBoxItemByTag(
                ScheduledWorldBackupIntervalComboBox,
                _preferences.ScheduledWorldBackupIntervalMinutes.ToString(
                    CultureInfo.InvariantCulture));

            EnableWorldBackupRetentionCheckBox.IsChecked =
                _preferences.WorldBackupRetentionEnabled;

            SelectComboBoxItemByTag(
                WorldBackupRetentionComboBox,
                _preferences.WorldBackupRetentionCount.ToString(
                    CultureInfo.InvariantCulture));

            SelectComboBoxItemByTag(
                RestartScheduleComboBox,
                string.IsNullOrWhiteSpace(
                    _preferences.RestartScheduleTag)
                    ? "360"
                    : _preferences.RestartScheduleTag);

            DailyRestartTimeTextBox.Text =
                string.IsNullOrWhiteSpace(
                    _preferences.DailyRestartTime)
                    ? "04:00"
                    : _preferences.DailyRestartTime;

            RestartScheduleComboBox_SelectionChanged(
                RestartScheduleComboBox,
                new SelectionChangedEventArgs(
                    Selector.SelectionChangedEvent,
                    Array.Empty<object>(),
                    Array.Empty<object>()));

            if (_currentSettings is null)
            {
                return;
            }

            if (_preferences.AutoSaveEnabled)
            {
                int interval =
                    Math.Max(
                        1,
                        _preferences.AutoSaveIntervalMinutes);

                _autoSaveTimer.Interval =
                    TimeSpan.FromMinutes(
                        interval);

                _nextAutoSaveTime =
                    DateTime.Now.AddMinutes(
                        interval);

                _autoSaveTimer.Start();
                UpdateAutoSaveDisplay(true);

                AddActivity(
                    $"Automatic world saves restored: every {interval} minutes.");
            }

            if (_preferences.ScheduledBackupEnabled)
            {
                int interval =
                    Math.Max(
                        1,
                        _preferences.ScheduledBackupIntervalMinutes);

                _scheduledBackupTimer.Interval =
                    TimeSpan.FromMinutes(
                        interval);

                _nextScheduledBackupTime =
                    DateTime.Now.AddMinutes(
                        interval);

                _scheduledBackupTimer.Start();
                UpdateScheduledBackupDisplay(true);

                AddActivity(
                    $"Configuration backup schedule restored: every {interval} minutes.");
            }

            if (_preferences.ScheduledWorldBackupEnabled)
            {
                int interval =
                    Math.Max(
                        1,
                        _preferences.ScheduledWorldBackupIntervalMinutes);

                _scheduledWorldBackupTimer.Interval =
                    TimeSpan.FromMinutes(
                        interval);

                _nextScheduledWorldBackupTime =
                    DateTime.Now.AddMinutes(
                        interval);

                _scheduledWorldBackupTimer.Start();
                UpdateScheduledWorldBackupDisplay(true);

                AddActivity(
                    $"Full world backup schedule restored: every {interval} minutes.");
            }

            if (_preferences.RestartScheduleEnabled)
            {
                if (TryCalculateNextRestartTime(
                        out DateTime nextRestart,
                        showErrors: false))
                {
                    _restartWarningsSent.Clear();
                    _nextRestartTime = nextRestart;
                    _restartScheduleTimer.Start();
                    UpdateRestartScheduleDisplay(true);

                    AddActivity(
                        $"Restart schedule restored. Next restart: {nextRestart:MMM d, yyyy h:mm:ss tt}.");
                }
                else
                {
                    _preferences.RestartScheduleEnabled = false;
                    _preferencesService.Save(_preferences);
                }
            }
        }

        private static void SelectComboBoxItemByTag(
            ComboBox comboBox,
            string tag)
        {
            foreach (object item in comboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    string.Equals(
                        comboBoxItem.Tag?.ToString(),
                        tag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem =
                        comboBoxItem;

                    return;
                }
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

            StartScheduledWorldBackupButton.Click += StartScheduledWorldBackupButton_Click;
            StopScheduledWorldBackupButton.Click += StopScheduledWorldBackupButton_Click;

            EnableWorldBackupRetentionCheckBox.Checked += WorldBackupRetentionSettings_Changed;
            EnableWorldBackupRetentionCheckBox.Unchecked += WorldBackupRetentionSettings_Changed;
            WorldBackupRetentionComboBox.SelectionChanged += WorldBackupRetentionSettings_Changed;

            UpdateAutoSaveDisplay(false);
            UpdateRestartScheduleDisplay(false);
            UpdateScheduledBackupDisplay(false);
            UpdateScheduledWorldBackupDisplay(false);
            RestartScheduleComboBox_SelectionChanged(
                RestartScheduleComboBox,
                new SelectionChangedEventArgs(
                    Selector.SelectionChangedEvent,
                    Array.Empty<object>(),
                    Array.Empty<object>()));
        }

        private void WorldBackupRetentionSettings_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_preferences is null)
            {
                return;
            }

            SaveWorldBackupRetentionPreferences();

            try
            {
                _preferencesService.Save(
                    _preferences);
            }
            catch
            {
                // Preference persistence failure is non-fatal here.
            }
        }

        private void SaveWorldBackupRetentionPreferences()
        {
            _preferences.WorldBackupRetentionEnabled =
                EnableWorldBackupRetentionCheckBox.IsChecked == true;

            if (WorldBackupRetentionComboBox.SelectedItem
                    is ComboBoxItem selectedItem &&
                int.TryParse(
                    selectedItem.Tag?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int retentionCount) &&
                retentionCount > 0)
            {
                _preferences.WorldBackupRetentionCount =
                    retentionCount;
            }
        }

        private void EnforceWorldBackupRetention()
        {
            if (!_preferences.WorldBackupRetentionEnabled ||
                string.IsNullOrWhiteSpace(
                    _settingsFilePath))
            {
                return;
            }

            try
            {
                IReadOnlyList<string> deleted =
                    _worldBackupService.EnforceRetention(
                        _settingsFilePath,
                        Math.Max(
                            1,
                            _preferences.WorldBackupRetentionCount));

                if (deleted.Count > 0)
                {
                    AddActivity(
                        $"World backup retention removed {deleted.Count} old backup{(deleted.Count == 1 ? "" : "s")}.");
                }
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"World backup retention cleanup failed: {ex.Message}");
            }
        }

        private void StartScheduledWorldBackupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    _settingsFilePath))
            {
                MessageBox.Show(
                    "Load the active PalWorldSettings.ini first.",
                    "Settings File Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (!TryGetSelectedScheduledWorldBackupInterval(
                    out int intervalMinutes))
            {
                return;
            }

            try
            {
                // Validate that the active world folder can be resolved now,
                // rather than waiting until the first scheduled run.
                _worldBackupService.GetWorldDirectory(
                    _settingsFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The active Palworld world-save folder could not be found.\n\n" +
                    ex.Message,
                    "World Backup Schedule Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            _scheduledWorldBackupTimer.Stop();
            _crashRecoveryTimer.Stop();

            _scheduledWorldBackupTimer.Interval =
                TimeSpan.FromMinutes(
                    intervalMinutes);

            _nextScheduledWorldBackupTime =
                DateTime.Now.AddMinutes(
                    intervalMinutes);

            _scheduledWorldBackupTimer.Start();

            _preferences.ScheduledWorldBackupEnabled =
                true;

            _preferences.ScheduledWorldBackupIntervalMinutes =
                intervalMinutes;

            SaveWorldBackupRetentionPreferences();

            _preferencesService.Save(
                _preferences);

            UpdateScheduledWorldBackupDisplay(
                true);

            AddActivity(
                $"Full world backup schedule enabled every {intervalMinutes} minutes.");
        }

        private void StopScheduledWorldBackupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _scheduledWorldBackupTimer.Stop();

            _nextScheduledWorldBackupTime =
                null;

            _preferences.ScheduledWorldBackupEnabled =
                false;

            _preferencesService.Save(
                _preferences);

            UpdateScheduledWorldBackupDisplay(
                false);

            AddActivity(
                "Full world backup schedule disabled.");
        }

        private async void ScheduledWorldBackupTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (_scheduledWorldBackupInProgress)
            {
                return;
            }

            _scheduledWorldBackupInProgress =
                true;

            try
            {
                if (string.IsNullOrWhiteSpace(
                        _settingsFilePath))
                {
                    AddActivity(
                        "Scheduled world backup skipped because no settings file is loaded.");

                    return;
                }

                if (_serverProcessService.IsRunning)
                {
                    if (!TryGetRestApiConnectionSettings(
                            out int restApiPort,
                            out string adminPassword))
                    {
                        AddActivity(
                            "Scheduled world backup skipped because REST API settings are unavailable.");

                        return;
                    }

                    AddActivity(
                        "Scheduled world backup: requesting world save.");

                    await _restApiService.SaveWorldAsync(
                        restApiPort,
                        adminPassword);

                    _lastWorldSaveTime =
                        DateTime.Now;

                    LastWorldSaveText.Text =
                        $"Last World Save: {_lastWorldSaveTime:MMM d, yyyy h:mm:ss tt}";

                    await Task.Delay(
                        1500);
                }

                string backupPath =
                    _worldBackupService.CreateWorldBackup(
                        _settingsFilePath);

                AddActivity(
                    $"Scheduled full world backup created: {System.IO.Path.GetFileName(backupPath)}");

                EnforceWorldBackupRetention();

                RefreshWorldBackupList();
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"Scheduled full world backup failed: {ex.Message}");
            }
            finally
            {
                _scheduledWorldBackupInProgress =
                    false;

                ScheduleNextWorldBackup();
            }
        }

        private void ScheduleNextWorldBackup()
        {
            if (!_scheduledWorldBackupTimer.IsEnabled)
            {
                return;
            }

            _nextScheduledWorldBackupTime =
                DateTime.Now.Add(
                    _scheduledWorldBackupTimer.Interval);

            UpdateScheduledWorldBackupDisplay(
                true);
        }

        private bool TryGetSelectedScheduledWorldBackupInterval(
            out int intervalMinutes)
        {
            intervalMinutes =
                0;

            if (ScheduledWorldBackupIntervalComboBox.SelectedItem
                    is not ComboBoxItem selectedItem ||
                !int.TryParse(
                    selectedItem.Tag?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out intervalMinutes) ||
                intervalMinutes <= 0)
            {
                MessageBox.Show(
                    "Select a valid full world backup interval.",
                    "Invalid World Backup Interval",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        private void UpdateScheduledWorldBackupDisplay(
            bool enabled)
        {
            StartScheduledWorldBackupButton.IsEnabled =
                !enabled;

            StopScheduledWorldBackupButton.IsEnabled =
                enabled;

            ScheduledWorldBackupIntervalComboBox.IsEnabled =
                !enabled;

            ScheduledWorldBackupStatusDot.Fill =
                enabled
                    ? GetBrush("SuccessColor")
                    : GetBrush("WarningColor");

            ScheduledWorldBackupStatusTitle.Text =
                enabled
                    ? "Scheduled world backups enabled"
                    : "Scheduled world backups disabled";

            if (enabled &&
                _nextScheduledWorldBackupTime.HasValue)
            {
                ScheduledWorldBackupStatusDescription.Text =
                    $"The complete active world will be backed up every {_scheduledWorldBackupTimer.Interval.TotalMinutes:0} minutes.";

                NextScheduledWorldBackupText.Text =
                    _nextScheduledWorldBackupTime.Value.ToString(
                        "MMM d h:mm tt",
                        CultureInfo.CurrentCulture);
            }
            else
            {
                ScheduledWorldBackupStatusDescription.Text =
                    "Choose an interval and click Start World Backup Schedule.";

                NextScheduledWorldBackupText.Text =
                    "—";
            }
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
            _scheduledWorldBackupTimer.Stop();

            _scheduledBackupTimer.Interval =
                TimeSpan.FromMinutes(
                    intervalMinutes);

            _nextScheduledBackupTime =
                DateTime.Now.AddMinutes(
                    intervalMinutes);

            _scheduledBackupTimer.Start();

            _preferences.ScheduledBackupEnabled = true;
            _preferences.ScheduledBackupIntervalMinutes = intervalMinutes;
            _preferencesService.Save(_preferences);

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

            _preferences.ScheduledBackupEnabled = false;
            _preferencesService.Save(_preferences);

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

            _preferences.RestartScheduleEnabled = true;
            _preferences.RestartScheduleTag =
                (RestartScheduleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "360";
            _preferences.DailyRestartTime =
                DailyRestartTimeTextBox.Text.Trim();
            _preferencesService.Save(_preferences);

            UpdateRestartScheduleDisplay(true);

            AddActivity(
                $"Restart schedule enabled. Next restart: {_nextRestartTime:MMM d, yyyy h:mm:ss tt}.");
        }

        private void CancelRestartScheduleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _restartScheduleTimer.Stop();
            _nextRestartTime = null;
            _restartWarningsSent.Clear();

            _preferences.RestartScheduleEnabled = false;
            _preferencesService.Save(_preferences);

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

                _intentionalServerStopInProgress = true;
                CancelCrashRecovery();

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
                _previousServerRunning = _serverProcessService.IsRunning;
                _intentionalServerStopInProgress = false;
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
            out DateTime nextRestart,
            bool showErrors = true)
        {
            nextRestart = default;

            if (RestartScheduleComboBox.SelectedItem
                is not ComboBoxItem selectedItem)
            {
                if (showErrors)
                {
                    MessageBox.Show(
                        "Select a restart schedule.",
                        "Restart Schedule Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

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
                    if (showErrors)
                    {
                        MessageBox.Show(
                            "Enter the daily restart time using 24-hour HH:mm format, for example 04:00 or 23:30.",
                            "Invalid Restart Time",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

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
                if (showErrors)
                {
                    MessageBox.Show(
                        "The selected restart interval is invalid.",
                        "Invalid Restart Schedule",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

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

            _preferences.AutoSaveEnabled = true;
            _preferences.AutoSaveIntervalMinutes = intervalMinutes;
            _preferencesService.Save(_preferences);

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

            _preferences.AutoSaveEnabled = false;
            _preferencesService.Save(_preferences);

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
            _serverProcessService.OutputReceived -= ServerProcessService_OutputReceived;
            _serverProcessService.ErrorReceived -= ServerProcessService_ErrorReceived;
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
                _playerRefreshTickCount = 0;
                _ = RefreshPlayersAsync(
                    preserveSelection: true,
                    logResult: false);
            }
            else
            {
                _playerRefreshTickCount = 0;
            }

            if (selectedPage == 4)
            {
                RefreshBackupList();
                RefreshWorldBackupList();
            }

            if (selectedPage == 6)
            {
                RefreshModsList();
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
            RefreshWorldBackupList();
            _ = RefreshLiveMetricsAsync();

            _preferences.LastSettingsFilePath = filePath;
            _preferencesService.Save(_preferences);

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
            _preferences.ServerExecutablePath =
                ServerExecutablePathTextBox.Text.Trim();

            _preferences.LaunchArguments =
                ServerLaunchArgumentsTextBox.Text.Trim();

            _preferences.AttachToRunningServerOnStartup =
                AttachToRunningServerCheckBox.IsChecked == true;

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
                CancelCrashRecovery();
                _serverProcessService.StartServer(_preferences.ServerExecutablePath, _preferences.LaunchArguments);
                AddActivity("Palworld server started.");
                UpdateServerProcessDisplay();
                _previousServerRunning = true;
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

            _intentionalServerStopInProgress = true;
            CancelCrashRecovery();

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
                _previousServerRunning = _serverProcessService.IsRunning;
                _intentionalServerStopInProgress = false;
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

            _intentionalServerStopInProgress = true;
            CancelCrashRecovery();

            try
            {
                _serverProcessService.ForceStopServer();
                AddActivity("Server force stopped.");
                UpdateServerProcessDisplay();
                _previousServerRunning = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not force stop the server.\n\n{ex.Message}", "Force Stop Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _previousServerRunning = _serverProcessService.IsRunning;
                _intentionalServerStopInProgress = false;
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

            _intentionalServerStopInProgress = true;
            CancelCrashRecovery();

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
                _previousServerRunning = true;
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
                _previousServerRunning = _serverProcessService.IsRunning;
                _intentionalServerStopInProgress = false;
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
            bool runningBeforeUpdate =
                _previousServerRunning;

            UpdateServerProcessDisplay();

            bool runningNow =
                _serverProcessService.IsRunning;

            if (runningBeforeUpdate &&
                !runningNow &&
                !_intentionalServerStopInProgress)
            {
                _lastCrashTime =
                    DateTime.Now;

                _crashCount++;

                CrashCountText.Text =
                    _crashCount.ToString(
                        CultureInfo.InvariantCulture);

                LastCrashText.Text =
                    _lastCrashTime.Value.ToString(
                        "MMM d, yyyy h:mm:ss tt",
                        CultureInfo.CurrentCulture);

                AddActivity(
                    "PalServer stopped unexpectedly or exited.");

                UpdateServerHealthDisplay(
                    "Critical",
                    "PalServer crashed or exited unexpectedly.",
                    true);

                ScheduleCrashRecovery();
            }

            _previousServerRunning =
                runningNow;

            _serverStatusTickCount++;

            if (_serverStatusTickCount >= 5)
            {
                _serverStatusTickCount = 0;
                _ = RefreshLiveMetricsAsync();
            }

            if (MainNavigationTabControl.SelectedIndex == 2 &&
                runningNow)
            {
                _playerRefreshTickCount++;

                if (_playerRefreshTickCount >= 3)
                {
                    _playerRefreshTickCount = 0;

                    _ = RefreshPlayersAsync(
                        preserveSelection: true,
                        logResult: false);
                }
            }
            else
            {
                _playerRefreshTickCount = 0;
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

        private void EvaluateServerHealth(
            PalworldServerMetrics metrics)
        {
            if (!_serverProcessService.IsRunning)
            {
                UpdateServerHealthDisplay(
                    "Critical",
                    "PalServer is offline.",
                    true);

                return;
            }

            double cpuUsage =
                _serverProcessService.GetCpuUsagePercent();

            double memoryGb =
                _serverProcessService.MemoryUsageBytes /
                1024d /
                1024d /
                1024d;

            if (metrics.ServerFps > 0 &&
                metrics.ServerFps < 20)
            {
                UpdateServerHealthDisplay(
                    "Critical",
                    $"Server FPS is critically low at {metrics.ServerFps}.",
                    true);

                return;
            }

            if (cpuUsage >= 95)
            {
                UpdateServerHealthDisplay(
                    "Critical",
                    $"CPU usage is critically high at {cpuUsage:0.0}%.",
                    true);

                return;
            }

            if (memoryGb >= 24)
            {
                UpdateServerHealthDisplay(
                    "Critical",
                    $"PalServer memory usage is critically high at {memoryGb:0.0} GB.",
                    true);

                return;
            }

            if (metrics.ServerFps > 0 &&
                metrics.ServerFps < 40)
            {
                UpdateServerHealthDisplay(
                    "Warning",
                    $"Server FPS is low at {metrics.ServerFps}.",
                    true);

                return;
            }

            if (cpuUsage >= 80)
            {
                UpdateServerHealthDisplay(
                    "Warning",
                    $"CPU usage is elevated at {cpuUsage:0.0}%.",
                    true);

                return;
            }

            if (memoryGb >= 16)
            {
                UpdateServerHealthDisplay(
                    "Warning",
                    $"PalServer memory usage is elevated at {memoryGb:0.0} GB.",
                    true);

                return;
            }

            UpdateServerHealthDisplay(
                "Healthy",
                $"Server is responding normally • {metrics.ServerFps} FPS • {metrics.CurrentPlayerCount}/{metrics.MaximumPlayerCount} players.",
                false);
        }

        private void UpdateServerHealthDisplay(
            string state,
            string description,
            bool warning)
        {
            bool stateChanged =
                !string.Equals(
                    _lastHealthState,
                    state,
                    StringComparison.OrdinalIgnoreCase);

            _lastHealthState =
                state;

            ServerHealthStatusTitle.Text =
                $"Server health: {state}";

            ServerHealthStatusDescription.Text =
                description;

            ServerHealthStatusDot.Fill =
                state switch
                {
                    "Healthy" =>
                        GetBrush("SuccessColor"),

                    "Warning" =>
                        GetBrush("WarningColor"),

                    "Critical" =>
                        GetBrush("WarningColor"),

                    _ =>
                        GetBrush("MutedTextColor")
                };

            if (warning)
            {
                if (stateChanged)
                {
                    _lastHealthWarningTime =
                        DateTime.Now;

                    LastHealthWarningText.Text =
                        $"Last Health Warning: {_lastHealthWarningTime:MMM d, yyyy h:mm:ss tt}";

                    AddActivity(
                        $"HEALTH {state.ToUpperInvariant()}: {description}");
                }
            }
            else if (stateChanged &&
                     string.Equals(
                         state,
                         "Healthy",
                         StringComparison.OrdinalIgnoreCase))
            {
                AddActivity(
                    "Server health returned to Healthy.");
            }
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

        // World save backup management
        private async void BackupWorldNowButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!EnsureSettingsFileLoaded())
            {
                return;
            }

            BackupWorldNowButton.IsEnabled = false;

            try
            {
                if (_serverProcessService.IsRunning)
                {
                    if (!TryGetRestApiConnectionSettings(
                            out int restApiPort,
                            out string adminPassword))
                    {
                        return;
                    }

                    WorldBackupStatusText.Text =
                        "Saving world through the REST API before creating the backup...";

                    await _restApiService.SaveWorldAsync(
                        restApiPort,
                        adminPassword);

                    await Task.Delay(1000);
                }

                WorldBackupStatusText.Text =
                    "Creating compressed world backup...";

                string backupPath =
                    _worldBackupService.CreateWorldBackup(
                        _settingsFilePath!);

                AddActivity(
                    $"World backup created: {System.IO.Path.GetFileName(backupPath)}");

                EnforceWorldBackupRetention();

                RefreshWorldBackupList();

                MessageBox.Show(
                    $"World backup created successfully.\n\n{backupPath}",
                    "World Backup Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"World backup failed: {ex.Message}");

                MessageBox.Show(
                    "Could not create the world backup.\n\n" +
                    ex.Message,
                    "World Backup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                BackupWorldNowButton.IsEnabled =
                    true;
            }
        }

        private void RefreshWorldBackupsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshWorldBackupList();
        }

        private void OpenWorldSaveFolderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!EnsureSettingsFileLoaded())
            {
                return;
            }

            try
            {
                _worldBackupService.OpenWorldSaveFolder(
                    _settingsFilePath!);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open the Palworld save folder.\n\n" +
                    ex.Message,
                    "Save Folder Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenWorldBackupFolderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!EnsureSettingsFileLoaded())
            {
                return;
            }

            try
            {
                _worldBackupService.OpenWorldBackupFolder(
                    _settingsFilePath!);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open the world backup folder.\n\n" +
                    ex.Message,
                    "Backup Folder Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void WorldBackupListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            bool selected =
                WorldBackupListView.SelectedItem
                is WorldBackupItem;

            RestoreWorldBackupButton.IsEnabled =
                selected;

            DeleteWorldBackupButton.IsEnabled =
                selected;

            if (WorldBackupListView.SelectedItem
                is WorldBackupItem backup)
            {
                SelectedWorldBackupText.Text =
                    $"Selected: {backup.FileName}";
            }
            else
            {
                SelectedWorldBackupText.Text =
                    "Select a world backup to restore or delete.";
            }
        }

        private void RestoreWorldBackupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!EnsureSettingsFileLoaded())
            {
                return;
            }

            if (_serverProcessService.IsRunning)
            {
                MessageBox.Show(
                    "Stop PalServer before restoring a world backup.\n\n" +
                    "Restoring while the server is running could corrupt save data.",
                    "Server Must Be Stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (WorldBackupListView.SelectedItem
                is not WorldBackupItem selectedBackup)
            {
                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    $"Restore this world backup?\n\n" +
                    $"{selectedBackup.FileName}\n\n" +
                    "A safety backup of the current world will be created automatically before restoration.",
                    "Confirm World Restore",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            RestoreWorldBackupButton.IsEnabled =
                false;

            try
            {
                WorldBackupStatusText.Text =
                    "Restoring world backup...";

                string safetyBackup =
                    _worldBackupService.RestoreWorldBackup(
                        _settingsFilePath!,
                        selectedBackup);

                AddActivity(
                    $"World backup restored: {selectedBackup.FileName}");

                AddActivity(
                    $"Pre-restore safety backup created: {System.IO.Path.GetFileName(safetyBackup)}");

                RefreshWorldBackupList();

                MessageBox.Show(
                    "World restored successfully.\n\n" +
                    $"Safety backup:\n{safetyBackup}",
                    "World Restored",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddActivity(
                    $"World restore failed: {ex.Message}");

                MessageBox.Show(
                    "Could not restore the world backup.\n\n" +
                    ex.Message,
                    "World Restore Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                WorldBackupListView_SelectionChanged(
                    WorldBackupListView,
                    new SelectionChangedEventArgs(
                        Selector.SelectionChangedEvent,
                        Array.Empty<object>(),
                        Array.Empty<object>()));
            }
        }

        private void DeleteWorldBackupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (WorldBackupListView.SelectedItem
                is not WorldBackupItem selectedBackup)
            {
                return;
            }

            MessageBoxResult result =
                MessageBox.Show(
                    $"Permanently delete this world backup?\n\n" +
                    $"{selectedBackup.FileName}\n\n" +
                    "This cannot be undone.",
                    "Delete World Backup",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _worldBackupService.DeleteWorldBackup(
                    selectedBackup);

                AddActivity(
                    $"World backup deleted: {selectedBackup.FileName}");

                RefreshWorldBackupList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not delete the world backup.\n\n" +
                    ex.Message,
                    "Delete World Backup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshWorldBackupList()
        {
            WorldBackupListView.ItemsSource =
                null;

            WorldBackupListView.Visibility =
                Visibility.Collapsed;

            NoWorldBackupsPanel.Visibility =
                Visibility.Visible;

            RestoreWorldBackupButton.IsEnabled =
                false;

            DeleteWorldBackupButton.IsEnabled =
                false;

            SelectedWorldBackupText.Text =
                "Select a world backup to restore or delete.";

            if (string.IsNullOrWhiteSpace(
                    _settingsFilePath))
            {
                WorldBackupStatusText.Text =
                    "Load the active PalWorldSettings.ini to manage full world backups.";

                NoWorldBackupsText.Text =
                    "Load the active server settings file first.";

                return;
            }

            try
            {
                var backups =
                    _worldBackupService.GetWorldBackups(
                        _settingsFilePath);

                if (backups.Count == 0)
                {
                    WorldBackupStatusText.Text =
                        "No full world backups have been created yet.";

                    NoWorldBackupsText.Text =
                        "Click Backup World Now to create the first full save-data backup.";

                    return;
                }

                WorldBackupListView.ItemsSource =
                    backups;

                WorldBackupListView.Visibility =
                    Visibility.Visible;

                NoWorldBackupsPanel.Visibility =
                    Visibility.Collapsed;

                WorldBackupStatusText.Text =
                    $"{backups.Count} world backup{(backups.Count == 1 ? "" : "s")} available.";
            }
            catch (Exception ex)
            {
                WorldBackupStatusText.Text =
                    $"World backups unavailable: {ex.Message}";

                NoWorldBackupsText.Text =
                    "The loaded settings file may not be the active Palworld server configuration.";
            }
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
            WorldBackupListView.ItemsSource = null;
            WorldBackupListView.Visibility = Visibility.Collapsed;
            NoWorldBackupsPanel.Visibility = Visibility.Visible;
            WorldBackupStatusText.Text =
                "Load the active PalWorldSettings.ini to manage full world backups.";
            NoWorldBackupsText.Text =
                "Load the active server settings file first.";
            RestoreWorldBackupButton.IsEnabled = false;
            DeleteWorldBackupButton.IsEnabled = false;
            SelectedWorldBackupText.Text =
                "Select a world backup to restore or delete.";

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