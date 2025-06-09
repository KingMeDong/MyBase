using Microsoft.Extensions.Configuration;
using System.IO;

namespace MyBase.Models {
    public class SystemStatusModel {
        public string DriveLetter { get; set; }
        public long TotalSpace { get; set; }
        public long FreeSpace { get; set; }
        public long UsedSpace { get; set; }
        public double UsedPercentage { get; set; }

        public SystemStatusModel(IConfiguration configuration) {
            DriveLetter = configuration["SystemSettings:StorageDriveLetter"];

            var drive = new DriveInfo(DriveLetter);

            TotalSpace = drive.TotalSize;
            FreeSpace = drive.AvailableFreeSpace;
            UsedSpace = TotalSpace - FreeSpace;
            UsedPercentage = (double)UsedSpace / TotalSpace * 100.0;
        }
    }
}
