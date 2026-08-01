using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace PalWorldServerManager.Services
{
    public sealed class WindowsStartupService
    {
        private const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string StartupValueName =
            "PalWorldServerManager";

        public void SetStartWithWindows(bool enabled)
        {
            using RegistryKey key =
                Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException(
                    "Could not open the Windows startup registry key.");

            if (!enabled)
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
                return;
            }

            string executablePath =
                Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException(
                    "Could not determine the application executable path.");

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException(
                    "The application executable could not be found.",
                    executablePath);
            }

            key.SetValue(
                StartupValueName,
                $"\"{executablePath}\"",
                RegistryValueKind.String);
        }
    }
}