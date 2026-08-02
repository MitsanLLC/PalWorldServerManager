using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PalWorldServerManager.Services
{
    public sealed class PersistentLogService : IDisposable
    {
        private readonly object _syncRoot = new object();

        public string LogsDirectory { get; }

        public PersistentLogService()
        {
            LogsDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "PalWorldServerManager",
                    "Logs");

            Directory.CreateDirectory(LogsDirectory);
        }

        public void Write(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string logPath =
                Path.Combine(
                    LogsDirectory,
                    $"{DateTime.Now:yyyy-MM-dd}.log");

            string line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

            lock (_syncRoot)
            {
                File.AppendAllText(
                    logPath,
                    line,
                    Encoding.UTF8);
            }
        }

        public void OpenLogsFolder()
        {
            Directory.CreateDirectory(LogsDirectory);

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = LogsDirectory,
                    UseShellExecute = true
                });
        }

        public void Dispose()
        {
        }
    }
}