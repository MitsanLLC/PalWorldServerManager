using System;
using System.Globalization;

namespace PalWorldServerManager.Models
{
    public sealed class ServerModItem
    {
        public string FilePath { get; init; } = "";

        public string FileName { get; init; } = "";

        public string DisplayName { get; init; } = "";

        public bool IsEnabled { get; init; }

        public long SizeInBytes { get; init; }

        public DateTime LastModified { get; init; }

        public string StatusDisplay =>
            IsEnabled
                ? "Enabled"
                : "Disabled";

        public string SizeDisplay
        {
            get
            {
                const double kilobyte = 1024;
                const double megabyte = kilobyte * 1024;
                const double gigabyte = megabyte * 1024;

                if (SizeInBytes >= gigabyte)
                {
                    return $"{SizeInBytes / gigabyte:0.00} GB";
                }

                if (SizeInBytes >= megabyte)
                {
                    return $"{SizeInBytes / megabyte:0.00} MB";
                }

                if (SizeInBytes >= kilobyte)
                {
                    return $"{SizeInBytes / kilobyte:0.00} KB";
                }

                return $"{SizeInBytes} bytes";
            }
        }

        public string LastModifiedDisplay =>
            LastModified.ToString(
                "MMM d, yyyy h:mm tt",
                CultureInfo.CurrentCulture);
    }
}