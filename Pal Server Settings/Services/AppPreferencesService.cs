using PalWorldServerManager.Models;
using System;
using System.IO;
using System.Text.Json;

namespace PalWorldServerManager.Services
{
    public sealed class AppPreferencesService
    {
        private readonly string _preferencesFilePath;

        public AppPreferencesService()
        {
            string appDataDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "PalWorldServerManager");

            Directory.CreateDirectory(
                appDataDirectory);

            _preferencesFilePath =
                Path.Combine(
                    appDataDirectory,
                    "preferences.json");
        }

        public AppPreferences Load()
        {
            if (!File.Exists(
                    _preferencesFilePath))
            {
                return new AppPreferences();
            }

            try
            {
                string json =
                    File.ReadAllText(
                        _preferencesFilePath);

                AppPreferences? preferences =
                    JsonSerializer.Deserialize<AppPreferences>(
                        json);

                return preferences
                    ?? new AppPreferences();
            }
            catch
            {
                return new AppPreferences();
            }
        }

        public void Save(
            AppPreferences preferences)
        {
            ArgumentNullException.ThrowIfNull(
                preferences);

            JsonSerializerOptions options =
                new JsonSerializerOptions
                {
                    WriteIndented = true
                };

            string json =
                JsonSerializer.Serialize(
                    preferences,
                    options);

            File.WriteAllText(
                _preferencesFilePath,
                json);
        }

        public string GetPreferencesFilePath()
        {
            return _preferencesFilePath;
        }
    }
}