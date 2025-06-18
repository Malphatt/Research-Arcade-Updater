using System;

namespace Research_Arcade_Updater.Models
{
    public class LauncherInfo
    {
        public string VersionNumber { get; set; } = null!;
        public string FileUrl { get; set; } = null!;
        public DateTime UploadedAt { get; set; }
    }
}
