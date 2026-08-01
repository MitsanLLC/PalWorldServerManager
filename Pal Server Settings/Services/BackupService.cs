using PalWorldServerManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PalWorldServerManager.Services
{
    public sealed class BackupService
    {
        public IReadOnlyList<BackupItem> GetBackups(
            string settingsFilePath)
        {
            ValidateSettingsPath(
                settingsFilePath);

            string directory =
                Path.GetDirectoryName(
                    settingsFilePath)
                ?? throw new InvalidOperationException(
                    "The settings directory could not be determined.");

            string fileNameWithoutExtension =
                Path.GetFileNameWithoutExtension(
                    settingsFilePath);

            string extension =
                Path.GetExtension(
                    settingsFilePath);

            string searchPattern =
                $"{fileNameWithoutExtension}.*.backup{extension}";

            return Directory
                .EnumerateFiles(
                    directory,
                    searchPattern,
                    SearchOption.TopDirectoryOnly)
                .Select(
                    filePath =>
                    {
                        FileInfo fileInfo =
                            new FileInfo(
                                filePath);

                        return new BackupItem
                        {
                            FilePath =
                                fileInfo.FullName,

                            FileName =
                                fileInfo.Name,

                            CreatedAt =
                                fileInfo.CreationTime,

                            SizeInBytes =
                                fileInfo.Length
                        };
                    })
                .OrderByDescending(
                    backup =>
                        backup.CreatedAt)
                .ToList();
        }

        public string RestoreBackup(
            string settingsFilePath,
            BackupItem backup)
        {
            ValidateSettingsPath(
                settingsFilePath);

            ArgumentNullException.ThrowIfNull(
                backup);

            if (string.IsNullOrWhiteSpace(
                    backup.FilePath))
            {
                throw new InvalidOperationException(
                    "The selected backup does not have a valid file path.");
            }

            if (!File.Exists(
                    backup.FilePath))
            {
                throw new FileNotFoundException(
                    "The selected backup file could not be found.",
                    backup.FilePath);
            }

            string safetyBackupPath =
                CreateSafetyBackup(
                    settingsFilePath);

            File.Copy(
                backup.FilePath,
                settingsFilePath,
                overwrite: true);

            return safetyBackupPath;
        }

        public void DeleteBackup(
            BackupItem backup)
        {
            ArgumentNullException.ThrowIfNull(
                backup);

            if (string.IsNullOrWhiteSpace(
                    backup.FilePath))
            {
                throw new InvalidOperationException(
                    "The selected backup does not have a valid file path.");
            }

            if (!File.Exists(
                    backup.FilePath))
            {
                return;
            }

            File.Delete(
                backup.FilePath);
        }

        public void OpenBackupFolder(
            string settingsFilePath)
        {
            ValidateSettingsPath(
                settingsFilePath);

            string directory =
                Path.GetDirectoryName(
                    settingsFilePath)
                ?? throw new InvalidOperationException(
                    "The settings directory could not be determined.");

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        directory,

                    UseShellExecute =
                        true
                });
        }

        public string CreateSafetyBackup(
            string settingsFilePath)
        {
            ValidateSettingsPath(
                settingsFilePath);

            string directory =
                Path.GetDirectoryName(
                    settingsFilePath)
                ?? throw new InvalidOperationException(
                    "The settings directory could not be determined.");

            string fileNameWithoutExtension =
                Path.GetFileNameWithoutExtension(
                    settingsFilePath);

            string extension =
                Path.GetExtension(
                    settingsFilePath);

            string timestamp =
                DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss");

            string backupPath =
                Path.Combine(
                    directory,
                    $"{fileNameWithoutExtension}.{timestamp}.restore-safety.backup{extension}");

            int duplicateNumber = 1;

            while (File.Exists(
                       backupPath))
            {
                backupPath =
                    Path.Combine(
                        directory,
                        $"{fileNameWithoutExtension}.{timestamp}-{duplicateNumber}.restore-safety.backup{extension}");

                duplicateNumber++;
            }

            File.Copy(
                settingsFilePath,
                backupPath,
                overwrite: false);

            return backupPath;
        }

        private static void ValidateSettingsPath(
            string settingsFilePath)
        {
            if (string.IsNullOrWhiteSpace(
                    settingsFilePath))
            {
                throw new ArgumentException(
                    "A settings file path is required.",
                    nameof(settingsFilePath));
            }

            if (!File.Exists(
                    settingsFilePath))
            {
                throw new FileNotFoundException(
                    "The Palworld settings file could not be found.",
                    settingsFilePath);
            }
        }
    }
}