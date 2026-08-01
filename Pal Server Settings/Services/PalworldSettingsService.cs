using PalWorldServerManager.Models;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace PalWorldServerManager.Services
{
    public sealed class PalworldSettingsService
    {
        public string LoadFileContents(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "A settings file path is required.",
                    nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "The Palworld settings file could not be found.",
                    filePath);
            }

            string contents = File.ReadAllText(filePath);

            ValidateFileContents(contents);

            return contents;
        }

        public ServerSettings ParseSettings(string fileContents)
        {
            ValidateFileContents(fileContents);

            return new ServerSettings
            {
                // Server
                ServerName =
                    GetString(fileContents, "ServerName"),

                ServerDescription =
                    GetString(fileContents, "ServerDescription"),

                AdminPassword =
                    GetString(fileContents, "AdminPassword"),

                ServerPassword =
                    GetString(fileContents, "ServerPassword"),

                ServerPlayerMaxNum =
                    GetInteger(
                        fileContents,
                        "ServerPlayerMaxNum",
                        32),

                // Gameplay
                ExpRate =
                    GetDecimal(
                        fileContents,
                        "ExpRate",
                        1.0),

                PalCaptureRate =
                    GetDecimal(
                        fileContents,
                        "PalCaptureRate",
                        1.0),

                PalSpawnNumRate =
                    GetDecimal(
                        fileContents,
                        "PalSpawnNumRate",
                        1.0),

                DayTimeSpeedRate =
                    GetDecimal(
                        fileContents,
                        "DayTimeSpeedRate",
                        1.0),

                NightTimeSpeedRate =
                    GetDecimal(
                        fileContents,
                        "NightTimeSpeedRate",
                        1.0),

                WorkSpeedRate =
                    GetDecimal(
                        fileContents,
                        "WorkSpeedRate",
                        1.0),

                // PvP and hardcore
                IsPvP =
                    GetBoolean(
                        fileContents,
                        "bIsPvP"),

                IsHardcore =
                    GetBoolean(
                        fileContents,
                        "bHardcore"),

                EnableFriendlyFire =
                    GetBoolean(
                        fileContents,
                        "bEnableFriendlyFire"),

                CharacterRecreateInHardcore =
                    GetBoolean(
                        fileContents,
                        "bCharacterRecreateInHardcore"),

                DeathPenalty =
                    GetString(
                        fileContents,
                        "DeathPenalty",
                        "All"),

                // Bases and guilds
                BaseCampWorkerMaxNum =
                    GetInteger(
                        fileContents,
                        "BaseCampWorkerMaxNum",
                        15),

                GuildPlayerMaxNum =
                    GetInteger(
                        fileContents,
                        "GuildPlayerMaxNum",
                        20),

                BaseCampMaxNumInGuild =
                    GetInteger(
                        fileContents,
                        "BaseCampMaxNumInGuild",
                        3),

                BaseCampMaxNum =
                    GetInteger(
                        fileContents,
                        "BaseCampMaxNum",
                        128),

                // Network
                PublicIP =
                    GetString(
                        fileContents,
                        "PublicIP"),

                PublicPort =
                    GetInteger(
                        fileContents,
                        "PublicPort",
                        8211),

                RconEnabled =
                    GetBoolean(
                        fileContents,
                        "RCONEnabled"),

                RconPort =
                    GetInteger(
                        fileContents,
                        "RCONPort",
                        25575),

                RestApiEnabled =
                    GetBoolean(
                        fileContents,
                        "RESTAPIEnabled"),

                RestApiPort =
                    GetInteger(
                        fileContents,
                        "RESTAPIPort",
                        8212),

                // Advanced
                UseAuth =
                    GetBoolean(
                        fileContents,
                        "bUseAuth"),

                AllowClientMod =
                    GetBoolean(
                        fileContents,
                        "bAllowClientMod"),

                IsUseBackupSaveData =
                    GetBoolean(
                        fileContents,
                        "bIsUseBackupSaveData"),

                ShowPlayerList =
                    GetBoolean(
                        fileContents,
                        "bShowPlayerList"),

                ChatPostLimitPerMinute =
                    GetInteger(
                        fileContents,
                        "ChatPostLimitPerMinute",
                        10)
            };
        }

        public string ApplySettings(
            string fileContents,
            ServerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            ValidateFileContents(fileContents);

            string updatedContents = fileContents;

            // Server
            updatedContents = SetString(
                updatedContents,
                "ServerName",
                settings.ServerName);

            updatedContents = SetString(
                updatedContents,
                "ServerDescription",
                settings.ServerDescription);

            updatedContents = SetInteger(
                updatedContents,
                "ServerPlayerMaxNum",
                settings.ServerPlayerMaxNum);

            updatedContents = SetString(
                updatedContents,
                "AdminPassword",
                settings.AdminPassword);

            updatedContents = SetString(
                updatedContents,
                "ServerPassword",
                settings.ServerPassword);

            // Gameplay
            updatedContents = SetDecimal(
                updatedContents,
                "ExpRate",
                settings.ExpRate);

            updatedContents = SetDecimal(
                updatedContents,
                "PalCaptureRate",
                settings.PalCaptureRate);

            updatedContents = SetDecimal(
                updatedContents,
                "PalSpawnNumRate",
                settings.PalSpawnNumRate);

            updatedContents = SetDecimal(
                updatedContents,
                "DayTimeSpeedRate",
                settings.DayTimeSpeedRate);

            updatedContents = SetDecimal(
                updatedContents,
                "NightTimeSpeedRate",
                settings.NightTimeSpeedRate);

            updatedContents = SetDecimal(
                updatedContents,
                "WorkSpeedRate",
                settings.WorkSpeedRate);

            // PvP and hardcore
            updatedContents = SetBoolean(
                updatedContents,
                "bIsPvP",
                settings.IsPvP);

            updatedContents = SetBoolean(
                updatedContents,
                "bHardcore",
                settings.IsHardcore);

            updatedContents = SetBoolean(
                updatedContents,
                "bEnableFriendlyFire",
                settings.EnableFriendlyFire);

            updatedContents = SetBoolean(
                updatedContents,
                "bCharacterRecreateInHardcore",
                settings.CharacterRecreateInHardcore);

            updatedContents = SetUnquotedString(
                updatedContents,
                "DeathPenalty",
                settings.DeathPenalty);

            // Bases and guilds
            updatedContents = SetInteger(
                updatedContents,
                "BaseCampWorkerMaxNum",
                settings.BaseCampWorkerMaxNum);

            updatedContents = SetInteger(
                updatedContents,
                "GuildPlayerMaxNum",
                settings.GuildPlayerMaxNum);

            updatedContents = SetInteger(
                updatedContents,
                "BaseCampMaxNumInGuild",
                settings.BaseCampMaxNumInGuild);

            updatedContents = SetInteger(
                updatedContents,
                "BaseCampMaxNum",
                settings.BaseCampMaxNum);

            // Network
            updatedContents = SetString(
                updatedContents,
                "PublicIP",
                settings.PublicIP);

            updatedContents = SetInteger(
                updatedContents,
                "PublicPort",
                settings.PublicPort);

            updatedContents = SetBoolean(
                updatedContents,
                "RCONEnabled",
                settings.RconEnabled);

            updatedContents = SetInteger(
                updatedContents,
                "RCONPort",
                settings.RconPort);

            updatedContents = SetBoolean(
                updatedContents,
                "RESTAPIEnabled",
                settings.RestApiEnabled);

            updatedContents = SetInteger(
                updatedContents,
                "RESTAPIPort",
                settings.RestApiPort);

            // Advanced
            updatedContents = SetBoolean(
                updatedContents,
                "bUseAuth",
                settings.UseAuth);

            updatedContents = SetBoolean(
                updatedContents,
                "bAllowClientMod",
                settings.AllowClientMod);

            updatedContents = SetBoolean(
                updatedContents,
                "bIsUseBackupSaveData",
                settings.IsUseBackupSaveData);

            updatedContents = SetBoolean(
                updatedContents,
                "bShowPlayerList",
                settings.ShowPlayerList);

            updatedContents = SetInteger(
                updatedContents,
                "ChatPostLimitPerMinute",
                settings.ChatPostLimitPerMinute);

            return updatedContents;
        }

        public void SaveFile(
            string filePath,
            string updatedContents)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "A settings file path is required.",
                    nameof(filePath));
            }

            ValidateFileContents(updatedContents);

            File.WriteAllText(
                filePath,
                updatedContents);
        }

        public string CreateBackup(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "A settings file path is required.",
                    nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "The file being backed up could not be found.",
                    filePath);
            }

            string directory =
                Path.GetDirectoryName(filePath)
                ?? throw new InvalidOperationException(
                    "The settings directory could not be determined.");

            string fileNameWithoutExtension =
                Path.GetFileNameWithoutExtension(filePath);

            string extension =
                Path.GetExtension(filePath);

            string timestamp =
                DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture);

            string backupPath =
                Path.Combine(
                    directory,
                    $"{fileNameWithoutExtension}.{timestamp}.backup{extension}");

            int duplicateNumber = 1;

            while (File.Exists(backupPath))
            {
                backupPath =
                    Path.Combine(
                        directory,
                        $"{fileNameWithoutExtension}.{timestamp}-{duplicateNumber}.backup{extension}");

                duplicateNumber++;
            }

            File.Copy(
                filePath,
                backupPath,
                overwrite: false);

            return backupPath;
        }

        private static void ValidateFileContents(
            string fileContents)
        {
            if (string.IsNullOrWhiteSpace(fileContents))
            {
                throw new InvalidDataException(
                    "The settings file is empty.");
            }

            if (!fileContents.Contains(
                    "OptionSettings=(",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The file does not contain a Palworld OptionSettings section.");
            }
        }

        private static string GetString(
            string fileContents,
            string settingName,
            string defaultValue = "")
        {
            string value =
                GetRawValue(
                    fileContents,
                    settingName);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return RemoveQuotesAndUnescape(value);
        }

        private static int GetInteger(
            string fileContents,
            string settingName,
            int defaultValue)
        {
            string value =
                GetRawValue(
                    fileContents,
                    settingName);

            return int.TryParse(
                RemoveQuotesAndUnescape(value),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result)
                    ? result
                    : defaultValue;
        }

        private static double GetDecimal(
            string fileContents,
            string settingName,
            double defaultValue)
        {
            string value =
                GetRawValue(
                    fileContents,
                    settingName);

            return double.TryParse(
                RemoveQuotesAndUnescape(value),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double result)
                    ? result
                    : defaultValue;
        }

        private static bool GetBoolean(
            string fileContents,
            string settingName)
        {
            string value =
                RemoveQuotesAndUnescape(
                    GetRawValue(
                        fileContents,
                        settingName));

            return value.Equals(
                "True",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRawValue(
            string fileContents,
            string settingName)
        {
            string pattern =
                $@"(?:^|,){Regex.Escape(settingName)}=(?<value>""(?:\\.|[^""])*""|[^,\)]*)";

            Match match =
                Regex.Match(
                    fileContents,
                    pattern);

            if (!match.Success)
            {
                return "";
            }

            return match.Groups["value"]
                .Value
                .Trim();
        }

        private static string SetString(
            string fileContents,
            string settingName,
            string value)
        {
            string formattedValue =
                $"\"{EscapeText(value)}\"";

            return ReplaceRawValue(
                fileContents,
                settingName,
                formattedValue);
        }

        private static string SetUnquotedString(
            string fileContents,
            string settingName,
            string value)
        {
            return ReplaceRawValue(
                fileContents,
                settingName,
                value.Trim());
        }

        private static string SetInteger(
            string fileContents,
            string settingName,
            int value)
        {
            return ReplaceRawValue(
                fileContents,
                settingName,
                value.ToString(
                    CultureInfo.InvariantCulture));
        }

        private static string SetDecimal(
            string fileContents,
            string settingName,
            double value)
        {
            return ReplaceRawValue(
                fileContents,
                settingName,
                value.ToString(
                    "0.000000",
                    CultureInfo.InvariantCulture));
        }

        private static string SetBoolean(
            string fileContents,
            string settingName,
            bool value)
        {
            return ReplaceRawValue(
                fileContents,
                settingName,
                value ? "True" : "False");
        }

        private static string ReplaceRawValue(
            string fileContents,
            string settingName,
            string formattedValue)
        {
            string pattern =
                $@"(?<prefix>(?:^|,){Regex.Escape(settingName)}=)(?:""(?:\\.|[^""])*""|[^,\)]*)";

            Match match =
                Regex.Match(
                    fileContents,
                    pattern);

            if (!match.Success)
            {
                throw new InvalidDataException(
                    $"The setting '{settingName}' was not found.");
            }

            string replacement =
                match.Groups["prefix"].Value +
                formattedValue;

            return fileContents
                .Remove(
                    match.Index,
                    match.Length)
                .Insert(
                    match.Index,
                    replacement);
        }

        private static string EscapeText(string value)
        {
            return (value ?? "")
                .Replace(
                    "\\",
                    "\\\\")
                .Replace(
                    "\"",
                    "\\\"");
        }

        private static string RemoveQuotesAndUnescape(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            string result = value.Trim();

            if (result.Length >= 2 &&
                result.StartsWith(
                    "\"",
                    StringComparison.Ordinal) &&
                result.EndsWith(
                    "\"",
                    StringComparison.Ordinal))
            {
                result = result[1..^1];
            }

            return result
                .Replace(
                    "\\\"",
                    "\"")
                .Replace(
                    "\\\\",
                    "\\");
        }
    }
}