using System;

namespace PalWorldServerManager.Models
{
    public sealed class BackupItem
    {
        public required string FilePath { get; init; }

        public required string FileName { get; init; }

        public DateTime Created { get; init; }

        public long SizeBytes { get; init; }

        public string CreatedDisplay =>
            Created.ToString("MMM d, yyyy h:mm:ss tt");

        public string SizeDisplay
        {
            get
            {
                if (SizeBytes >= 1024 * 1024)
                {
                    return $"{SizeBytes / (1024d * 1024d):0.00} MB";
                }

                if (SizeBytes >= 1024)
                {
                    return $"{SizeBytes / 1024d:0.00} KB";
                }

                return $"{SizeBytes} bytes";
            }
        }
    }
}
