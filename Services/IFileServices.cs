using BulkCropAndResizeTool.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BulkCropAndResizeTool.Services
{
    public interface IFileServices
    {
        List<string> GetImageFiles(string folderPath);
        string? GetValidDirectoryPath(string path);
        bool IsValidPath(string path);
        string GetUniqueFileName(string folderPath, string baseName, string extension);
    }

    public class FileService : IFileServices
    {
        public List<string> GetImageFiles(string folderPath)
        {
            var files = new List<string>();
            foreach (var ext in AppConstants.ImageExtensions)
                files.AddRange(Directory.GetFiles(folderPath, ext, SearchOption.TopDirectoryOnly));
            return files;
        }

        public string? GetValidDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return AppConstants.DefaultDstBoxText;

            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !string.IsNullOrEmpty(Path.GetExtension(path)))
            {
                string? directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory)) return null;
                path = directory;
            }

            if (!Path.IsPathRooted(path)) return null;

            try
            {
                string fullPath = Path.GetFullPath(path);
                string directoryName = Path.GetFileName(fullPath);

                if (!string.IsNullOrEmpty(directoryName))
                {
                    string[] reserved = ["CON", "PRN", "AUX", "NUL"];

                    // FIX: Use Any() with StringComparison
                    if (reserved.Any(r => string.Equals(r, directoryName, StringComparison.OrdinalIgnoreCase)))
                        return null;

                    // Additional validation
                    if (directoryName.TrimEnd(' ', '.').Length != directoryName.Length)
                        return null;
                }

                string? root = Path.GetPathRoot(fullPath);
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return null;

                return fullPath;
            }
            catch
            {
                return null;
            }
        }

        public bool IsValidPath(string path)
        {
            return GetValidDirectoryPath(path) != null;
        }

        public string GetUniqueFileName(string folderPath, string baseName, string extension)
        {
            string fileName = baseName + extension;
            string fullPath = Path.Combine(folderPath, fileName);
            int count = 1;

            while (File.Exists(fullPath))
            {
                fileName = $"{baseName}_{count}{extension}";
                fullPath = Path.Combine(folderPath, fileName);
                count++;
            }

            return fileName;
        }
    }
}