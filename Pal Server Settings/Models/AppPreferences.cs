namespace PalWorldServerManager.Models
{
    public sealed class AppPreferences
    {
        public string ServerExecutablePath { get; set; } = "";

        public string LaunchArguments { get; set; } =
            "-publiclobby -port=8211";

        public bool AttachToRunningServerOnStartup { get; set; } = true;

        // Last active Palworld configuration file
        public string LastSettingsFilePath { get; set; } = "";

        // Automatic world save scheduler
        public bool AutoSaveEnabled { get; set; }

        public int AutoSaveIntervalMinutes { get; set; } = 15;

        // Scheduled restart
        public bool RestartScheduleEnabled { get; set; }

        public string RestartScheduleTag { get; set; } = "360";

        public string DailyRestartTime { get; set; } = "04:00";

        // Scheduled PalWorldSettings.ini backups
        public bool ScheduledBackupEnabled { get; set; }

        public int ScheduledBackupIntervalMinutes { get; set; } = 60;

        // Scheduled full world-save backups
        public bool ScheduledWorldBackupEnabled { get; set; }

        public int ScheduledWorldBackupIntervalMinutes { get; set; } = 180;
    }
}