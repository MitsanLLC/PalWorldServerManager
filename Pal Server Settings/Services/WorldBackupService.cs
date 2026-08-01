using PalWorldServerManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PalWorldServerManager.Services
{
    public sealed class WorldBackupService
    {
        public string GetSaveGamesRoot(
            string settingsFilePath)
        {
            if (string.IsNullOrWhiteSpace(
                    settingsFilePath))
            {
                throw new ArgumentException(
                    "A settings file path is required.",
                    nameof(settingsFilePath));
            }

            string? windowsServerDirectory =
                Path.GetDirectoryName(
                    settingsFilePath);

            if (windowsServerDirectory is null)
            {
                throw new InvalidOperationException(
                    "Could not determine the Palworld configuration directory.");
            }

            DirectoryInfo windowsServerInfo =
                new DirectoryInfo(
                    windowsServerDirectory);

            DirectoryInfo? configDirectory =
                windowsServerInfo.Parent;

            if (configDirectory is null ||
                !configDirectory.Name.Equals(
                    "Config",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The loaded settings file must be the active PalWorldSettings.ini inside Pal\\Saved\\Config\\WindowsServer.");
            }

            DirectoryInfo? savedDirectory =
                configDirectory.Parent;

            if (savedDirectory is null ||
                !savedDirectory.Name.Equals(
                    "Saved",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Could not determine the Palworld Saved directory.");
            }

            string saveGamesRoot =
                Path.Combine(
                    savedDirectory.FullName,
                    "SaveGames",
                    "0");

            if (!Directory.Exists(
                    saveGamesRoot))
            {
                throw new DirectoryNotFoundException(
                    $"Palworld save directory was not found:\n{saveGamesRoot}");
            }

            return saveGamesRoot;
        }

        public string GetWorldDirectory(
            string settingsFilePath)
        {
            string saveGamesRoot =
                GetSaveGamesRoot(
                    settingsFilePath);

            DirectoryInfo root =
                new DirectoryInfo(
                    saveGamesRoot);

            DirectoryInfo[] candidates =
                root.GetDirectories()
                    .Where(
                        directory =>
                            !directory.Name.StartsWith(
                                ".",
                                StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(
                        directory =>
                            directory.LastWriteTimeUtc)
                    .ToArray();

            if (candidates.Length == 0)
            {
                throw new DirectoryNotFoundException(
                    "No Palworld world save folder was found.");
            }

            return candidates[0].FullName;
        }

        public string GetBackupDirectory(
            string settingsFilePath)
        {
            string saveGamesRoot =
                GetSaveGamesRoot(
                    settingsFilePath);

            DirectoryInfo root =
                new DirectoryInfo(
                    saveGamesRoot);

            DirectoryInfo? saveGamesDirectory =
                root.Parent;

            DirectoryInfo? savedDirectory =
                saveGamesDirectory?.Parent;

            if (savedDirectory is null)
            {
                throw new InvalidOperationException(
                    "Could not determine the Palworld Saved directory.");
            }

            string backupDirectory =
                Path.Combine(
                    savedDirectory.FullName,
                    "PalWorldServerManagerBackups",
                    "World");

            Directory.CreateDirectory(
                backupDirectory);

            return backupDirectory;
        }

        public string CreateWorldBackup(
            string settingsFilePath)
        {
            string worldDirectory =
                GetWorldDirectory(
                    settingsFilePath);

            string backupDirectory =
                GetBackupDirectory(
                    settingsFilePath);

            string worldName =
                Path.GetFileName(
                    worldDirectory);

            string timestamp =
                DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss");

            string backupPath =
                Path.Combine(
                    backupDirectory,
                    $"World-{worldName}-{timestamp}.zip");

            int duplicateNumber = 1;

            while (File.Exists(
                       backupPath))
            {
                backupPath =
                    Path.Combine(
                        backupDirectory,
                        $"World-{worldName}-{timestamp}-{duplicateNumber}.zip");

                duplicateNumber++;
            }

            ZipFile.CreateFromDirectory(
                worldDirectory,
                backupPath,
                CompressionLevel.Fastest,
                includeBaseDirectory: false);

            return backupPath;
        }

        public IReadOnlyList<WorldBackupItem> GetWorldBackups(
            string settingsFilePath)
        {
            string backupDirectory =
                GetBackupDirectory(
                    settingsFilePath);

            return Directory
                .EnumerateFiles(
                    backupDirectory,
                    "*.zip",
                    SearchOption.TopDirectoryOnly)
                .Select(
                    filePath =>
                    {
                        FileInfo info =
                            new FileInfo(
                                filePath);

                        return new WorldBackupItem
                        {
                            FilePath =
                                info.FullName,

                            FileName =
                                info.Name,

                            CreatedAt =
                                info.CreationTime,

                            SizeInBytes =
                                info.Length
                        };
                    })
                .OrderByDescending(
                    item =>
                        item.CreatedAt)
                .ToList();
        }

        public string RestoreWorldBackup(
            string settingsFilePath,
            WorldBackupItem backup)
        {
            ArgumentNullException.ThrowIfNull(
                backup);

            if (!File.Exists(
                    backup.FilePath))
            {
                throw new FileNotFoundException(
                    "The selected world backup could not be found.",
                    backup.FilePath);
            }

            string worldDirectory =
                GetWorldDirectory(
                    settingsFilePath);

            string safetyBackup =
                CreateWorldBackup(
                    settingsFilePath);

            string temporaryDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "PalWorldServerManager",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                temporaryDirectory);

            try
            {
                ZipFile.ExtractToDirectory(
                    backup.FilePath,
                    temporaryDirectory);

                DeleteDirectoryContents(
                    worldDirectory);

                CopyDirectory(
                    temporaryDirectory,
                    worldDirectory);
            }
            finally
            {
                if (Directory.Exists(
                        temporaryDirectory))
                {
                    Directory.Delete(
                        temporaryDirectory,
                        recursive: true);
                }
            }

            return safetyBackup;
        }

        public void DeleteWorldBackup(
            WorldBackupItem backup)
        {
            ArgumentNullException.ThrowIfNull(
                backup);

            if (File.Exists(
                    backup.FilePath))
            {
                File.Delete(
                    backup.FilePath);
            }
        }

        public void OpenWorldBackupFolder(
            string settingsFilePath)
        {
            string directory =
                GetBackupDirectory(
                    settingsFilePath);

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        directory,

                    UseShellExecute =
                        true
                });
        }

        public void OpenWorldSaveFolder(
            string settingsFilePath)
        {
            string directory =
                GetWorldDirectory(
                    settingsFilePath);

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        directory,

                    UseShellExecute =
                        true
                });
        }

        private static void DeleteDirectoryContents(
            string directory)
        {
            DirectoryInfo directoryInfo =
                new DirectoryInfo(
                    directory);

            foreach (FileInfo file
                     in directoryInfo.GetFiles())
            {
                file.IsReadOnly =
                    false;

                file.Delete();
            }

            foreach (DirectoryInfo childDirectory
                     in directoryInfo.GetDirectories())
            {
                childDirectory.Delete(
                    recursive: true);
            }
        }

        private static void CopyDirectory(
            string sourceDirectory,
            string destinationDirectory)
        {
            Directory.CreateDirectory(
                destinationDirectory);

            foreach (string sourceFile
                     in Directory.GetFiles(
                         sourceDirectory))
            {
                string destinationFile =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(
                            sourceFile));

                File.Copy(
                    sourceFile,
                    destinationFile,
                    overwrite: true);
            }

            foreach (string sourceSubdirectory
                     in Directory.GetDirectories(
                         sourceDirectory))
            {
                string destinationSubdirectory =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(
                            sourceSubdirectory));

                CopyDirectory(
                    sourceSubdirectory,
                    destinationSubdirectory);
            }
        }
    }
}