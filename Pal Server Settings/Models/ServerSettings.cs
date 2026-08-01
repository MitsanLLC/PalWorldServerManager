namespace PalWorldServerManager.Models
{
    public sealed class ServerSettings
    {
        // Server
        public string ServerName { get; set; } = "";
        public string ServerDescription { get; set; } = "";
        public string AdminPassword { get; set; } = "";
        public string ServerPassword { get; set; } = "";
        public int ServerPlayerMaxNum { get; set; } = 32;

        // Gameplay
        public double ExpRate { get; set; } = 1.0;
        public double PalCaptureRate { get; set; } = 1.0;
        public double PalSpawnNumRate { get; set; } = 1.0;
        public double DayTimeSpeedRate { get; set; } = 1.0;
        public double NightTimeSpeedRate { get; set; } = 1.0;
        public double WorkSpeedRate { get; set; } = 1.0;

        // PvP and hardcore
        public bool IsPvP { get; set; }
        public bool IsHardcore { get; set; }
        public bool EnableFriendlyFire { get; set; }
        public bool CharacterRecreateInHardcore { get; set; }
        public string DeathPenalty { get; set; } = "All";

        // Bases and guilds
        public int BaseCampWorkerMaxNum { get; set; } = 15;
        public int GuildPlayerMaxNum { get; set; } = 20;
        public int BaseCampMaxNumInGuild { get; set; } = 3;
        public int BaseCampMaxNum { get; set; } = 128;

        // Network
        public string PublicIP { get; set; } = "";
        public int PublicPort { get; set; } = 8211;
        public bool RconEnabled { get; set; }
        public int RconPort { get; set; } = 25575;
        public bool RestApiEnabled { get; set; }
        public int RestApiPort { get; set; } = 8212;

        // Advanced
        public bool UseAuth { get; set; }
        public bool AllowClientMod { get; set; }
        public bool IsUseBackupSaveData { get; set; }
        public bool ShowPlayerList { get; set; }
        public int ChatPostLimitPerMinute { get; set; } = 10;

        public string GameMode
        {
            get
            {
                if (IsHardcore)
                {
                    return "Hardcore";
                }

                if (IsPvP)
                {
                    return "PvP";
                }

                return "Standard";
            }
        }
    }
}