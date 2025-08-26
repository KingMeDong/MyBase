using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace MyBase.Data {
    public static class FileHelper {
        private static IConfiguration? _configuration;

        public static void Initialize(IConfiguration configuration) => _configuration = configuration;

        public static string UploadDirectory {
            get {
                var basePath = _configuration?["SystemSettings:FileManagerPath"];
                return string.IsNullOrWhiteSpace(basePath)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserFiles")
                    : basePath;
            }
        }

        public static string ImagesDirectory {
            get {
                var imgPath = _configuration?["SystemSettings:ImagesPath"];
                return !string.IsNullOrWhiteSpace(imgPath)
                    ? imgPath
                    : Path.Combine(UploadDirectory, "Images");
            }
        }


        public static string ImagesThumbCacheDirectory
            => Path.Combine(ImagesDirectory, "_cache", "thumbs");

        public static void EnsureUploadDirectoryExists() => EnsureDirectoryExists(UploadDirectory);
        public static void EnsureImageDirectoryExists() => EnsureDirectoryExists(ImagesDirectory);
        public static void EnsureImagesThumbCacheDirectoryExists() => EnsureDirectoryExists(ImagesThumbCacheDirectory);

        public static string GetFilePath(string fileName)
            => Path.Combine(UploadDirectory, Path.GetFileName(fileName));

        public static string GetImagePath(string fileName)
            => Path.Combine(ImagesDirectory, Path.GetFileName(fileName));


        public static string GetImageThumbCachePath(string fileName, int w, int h) {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var safeW = Math.Clamp(w, 32, 2000);
            var safeH = h <= 0 ? 0 : Math.Clamp(h, 32, 2000);
            var name = $"{baseName}_{safeW}x{safeH}.jpg";
            return Path.Combine(ImagesThumbCacheDirectory, name);
        }

        private static void EnsureDirectoryExists(string path) {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
