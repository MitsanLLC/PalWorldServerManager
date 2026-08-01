using System;
using System.Globalization;

namespace PalWorldServerManager.Models
{
    public sealed class BackupItem
    {
        public string FilePath { get; init; } = "";

        public string FileName { get; init; } = "";

        public DateTime CreatedAt { get; init; }

        public long SizeInBytes { get; init; }

        public string CreatedAtDisplay
        {
            get
            {
                return CreatedAt.ToString(
                    "MMM d, yyyy h:mm:ss tt",
                    CultureInfo.CurrentCulture);
            }
        }

        public string SizeDisplay
        {
            get
            {
                return FormatFileSize(
                    SizeInBytes);
            }
        }

        private static string FormatFileSize(
            long bytes)
        {
            const double kilobyte = 1024;
            const double megabyte = kilobyte * 1024;
            const double gigabyte = megabyte * 1024;

            if (bytes >= gigabyte)
            {
                return (bytes / gigabyte).ToString(
                    "0.00 GB",
                    CultureInfo.InvariantCulture);
            }

            if (bytes >= megabyte)
            {
                return (bytes / megabyte).ToString(
                    "0.00 MB",
                    CultureInfo.InvariantCulture);
            }

            if (bytes >= kilobyte)
            {
                return (bytes / kilobyte).ToString(
                    "0.00 KB",
                    CultureInfo.InvariantCulture);
            }

            return $"{bytes} bytes";
        }
    }
}