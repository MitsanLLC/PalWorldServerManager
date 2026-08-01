using PalWorldServerManager.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PalWorldServerManager.Services
{
    public sealed class BackupService
    {
        public string CreateBackup(string settingsFilePath, string reason = "backup")
        {
            ValidateSettingsPath(settingsFilePath);

            string directory =
                Path.GetDirectoryName(settingsFilePath)
                ?? throw new InvalidOperationException(
                    "The settings directory could not be determined.");

            string fileNameWithoutExtension =
                Path.GetFileNameWithoutExtension(settingsFilePath);

            string extension =
                Path.GetExtension(settingsFilePath);

            string timestamp =
                DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss-fff",
                    CultureInfo.InvariantCulture);

            string safeReason =
                string.IsNullOrWhiteSpace(reason)
                    ? "backup"
                    : string.Concat(
                        reason.Where(character =>
                            char.IsLetterOrDigit(character) ||
                            character == '-' ||
                            character == '_'));

            string backupPath =
                Path.Combine(
                    directory,
                    $"{fileNameWithoutExtension}.{timestamp}.{safeReason}.backup{extension}");

            File.Copy(
                settingsFilePath,
                backupPath,
                overwrite: false);

            return backupPath;
        }

        public IReadOnlyList<BackupItem> GetBackups(string settingsFilePath)
        {
            ValidateSettingsPath(settingsFilePath);

            string directory =
                Path.GetDirectoryName(settingsFilePath)
                ?? throw new InvalidOperationException(
                    "The settings directory could not be determined.");

            string fileNameWithoutExtension =
                Path.GetFileNameWithoutExtension(settingsFilePath);

            string extension =
                Path.GetExtension(settingsFilePath);

            string searchPattern =
                $"{fileNameWithoutExtension}.*.backup{extension}";

            return Directory
                .EnumerateFiles(
                    directory,
                    searchPattern,
                    SearchOption.TopDirectoryOnly)
                .Select(path =>
                {
                    FileInfo file = new FileInfo(path);

                    return new BackupItem
                    {
                        FilePath = file.FullName,
                        FileName = file.Name,
                        Created = file.LastWriteTime,
                        SizeBytes = file.Length
                    };
                })
                .OrderByDescending(item => item.Created)
                .ToList();
        }

        public string RestoreBackup(
            string settingsFilePath,
            string selectedBackupPath)
        {
            ValidateSettingsPath(settingsFilePath);

            if (string.IsNullOrWhiteSpace(selectedBackupPath) ||
                !File.Exists(selectedBackupPath))
            {
                throw new FileNotFoundException(
                    "The selected backup file could not be found.",
                    selectedBackupPath);
            }

            string safetyBackup =
                CreateBackup(
                    settingsFilePath,
                    "before-restore");

            File.Copy(
                selectedBackupPath,
                settingsFilePath,
                overwrite: true);

            return safetyBackup;
        }

        public string GetBackupFolder(string settingsFilePath)
        {
            ValidateSettingsPath(settingsFilePath);

            return Path.GetDirectoryName(settingsFilePath)
                ?? throw new InvalidOperationException(
                    "The settings directory could not be determined.");
        }

        private static void ValidateSettingsPath(string settingsFilePath)
        {
            if (string.IsNullOrWhiteSpace(settingsFilePath))
            {
                throw new InvalidOperationException(
                    "No settings file is loaded.");
            }

            if (!File.Exists(settingsFilePath))
            {
                throw new FileNotFoundException(
                    "The loaded settings file no longer exists.",
                    settingsFilePath);
            }
        }
    }
}
