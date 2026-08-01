namespace PalWorldServerManager.Models
{
    public sealed class AppPreferences
    {
        public string ServerExecutablePath { get; set; } = "";

        public string LaunchArguments { get; set; } =
            "-publiclobby -port=8211";

        public bool AttachToRunningServerOnStartup { get; set; } = true;
    }
}