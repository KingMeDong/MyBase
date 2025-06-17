using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace MyBase.Data {
    public static class FileHelper {
        private static IConfiguration? _configuration;

        public static void Initialize(IConfiguration configuration) {
            _configuration = configuration;
        }

        public static string UploadDirectory {
            get {
                var basePath = _configuration?["SystemSettings:FileManagerPath"];
                if (string.IsNullOrWhiteSpace(basePath)) {
                    // Fallback
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserFiles");
                }
                return basePath;
            }
        }

        public static void EnsureUploadDirectoryExists() {
            if (!Directory.Exists(UploadDirectory)) {
                Directory.CreateDirectory(UploadDirectory);
            }
        }

        public static string GetFilePath(string fileName) {
            return Path.Combine(UploadDirectory, fileName);
        }
    }
}
