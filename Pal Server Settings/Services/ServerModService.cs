using PalWorldServerManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PalWorldServerManager.Services
{
    public sealed class ServerModService
    {
        private const string DisabledSuffix =
            ".disabled";

        public string GetPakDirectory(
            string serverExecutablePath)
        {
            if (string.IsNullOrWhiteSpace(
                    serverExecutablePath))
            {
                throw new ArgumentException(
                    "Select PalServer.exe first.",
                    nameof(serverExecutablePath));
            }

            if (!File.Exists(
                    serverExecutablePath))
            {
                throw new FileNotFoundException(
                    "PalServer.exe could not be found.",
                    serverExecutablePath);
            }

            string? serverRoot =
                Path.GetDirectoryName(
                    serverExecutablePath);

            if (serverRoot is null)
            {
                throw new InvalidOperationException(
                    "Could not determine the PalServer installation directory.");
            }

            string pakDirectory =
                Path.Combine(
                    serverRoot,
                    "Pal",
                    "Content",
                    "Paks");

            if (!Directory.Exists(
                    pakDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"The Palworld Paks directory was not found:\n{pakDirectory}");
            }

            return pakDirectory;
        }

        public IReadOnlyList<ServerModItem> GetInstalledMods(
            string serverExecutablePath)
        {
            string pakDirectory =
                GetPakDirectory(
                    serverExecutablePath);

            return Directory
                .EnumerateFiles(
                    pakDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(
                    path =>
                        path.EndsWith(
                            ".pak",
                            StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(
                            ".pak.disabled",
                            StringComparison.OrdinalIgnoreCase))
                .Select(
                    path =>
                    {
                        FileInfo info =
                            new FileInfo(
                                path);

                        bool enabled =
                            path.EndsWith(
                                ".pak",
                                StringComparison.OrdinalIgnoreCase);

                        string fileName =
                            info.Name;

                        string displayName =
                            enabled
                                ? Path.GetFileNameWithoutExtension(
                                    fileName)
                                : fileName[..^DisabledSuffix.Length];

                        if (displayName.EndsWith(
                                ".pak",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            displayName =
                                Path.GetFileNameWithoutExtension(
                                    displayName);
                        }

                        return new ServerModItem
                        {
                            FilePath =
                                info.FullName,

                            FileName =
                                info.Name,

                            DisplayName =
                                displayName,

                            IsEnabled =
                                enabled,

                            SizeInBytes =
                                info.Length,

                            LastModified =
                                info.LastWriteTime
                        };
                    })
                .OrderBy(
                    mod =>
                        mod.DisplayName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string InstallPakMod(
            string serverExecutablePath,
            string sourcePakPath)
        {
            if (string.IsNullOrWhiteSpace(
                    sourcePakPath))
            {
                throw new ArgumentException(
                    "A PAK file path is required.",
                    nameof(sourcePakPath));
            }

            if (!File.Exists(
                    sourcePakPath))
            {
                throw new FileNotFoundException(
                    "The selected PAK file could not be found.",
                    sourcePakPath);
            }

            if (!sourcePakPath.EndsWith(
                    ".pak",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Only .pak files can be installed with this feature.");
            }

            string pakDirectory =
                GetPakDirectory(
                    serverExecutablePath);

            string destinationPath =
                Path.Combine(
                    pakDirectory,
                    Path.GetFileName(
                        sourcePakPath));

            if (File.Exists(
                    destinationPath) ||
                File.Exists(
                    destinationPath + DisabledSuffix))
            {
                throw new IOException(
                    "A mod with this filename is already installed.");
            }

            File.Copy(
                sourcePakPath,
                destinationPath,
                overwrite: false);

            return destinationPath;
        }

        public void RemoveMod(
            ServerModItem mod)
        {
            ArgumentNullException.ThrowIfNull(
                mod);

            if (!File.Exists(
                    mod.FilePath))
            {
                return;
            }

            File.Delete(
                mod.FilePath);
        }

        public void SetEnabled(
            ServerModItem mod,
            bool enabled)
        {
            ArgumentNullException.ThrowIfNull(
                mod);

            if (!File.Exists(
                    mod.FilePath))
            {
                throw new FileNotFoundException(
                    "The selected mod file could not be found.",
                    mod.FilePath);
            }

            if (mod.IsEnabled == enabled)
            {
                return;
            }

            string destinationPath;

            if (enabled)
            {
                if (!mod.FilePath.EndsWith(
                        ".pak.disabled",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The selected disabled mod does not have the expected .pak.disabled extension.");
                }

                destinationPath =
                    mod.FilePath[..^DisabledSuffix.Length];
            }
            else
            {
                destinationPath =
                    mod.FilePath + DisabledSuffix;
            }

            if (File.Exists(
                    destinationPath))
            {
                throw new IOException(
                    $"A file already exists at:\n{destinationPath}");
            }

            File.Move(
                mod.FilePath,
                destinationPath);
        }

        public void OpenPakDirectory(
            string serverExecutablePath)
        {
            string directory =
                GetPakDirectory(
                    serverExecutablePath);

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        directory,

                    UseShellExecute =
                        true
                });
        }
    }
}