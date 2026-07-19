using BulkCropAndResizeTool.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkCropAndResizeTool.Services
{
    public interface IFilenameService
    {
        string GenerateFilename(string sourcePath, string prefix, string suffix, bool isPrefix, bool overwrite);
        string GetDefaultText(bool isResize, bool isPrefix);
    }

    public class FilenameService : IFilenameService
    {
        public string GenerateFilename(string sourcePath, string prefix, string suffix, bool isPrefix, bool overwrite)
        {
            string baseName = string.IsNullOrEmpty(sourcePath) ? "image" : System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string ext = string.IsNullOrEmpty(sourcePath) ? ".jpg" : System.IO.Path.GetExtension(sourcePath);

            if (overwrite)
                return baseName + ext;

            return isPrefix ? $"{prefix}{baseName}{ext}" : $"{baseName}{suffix}{ext}";
        }

        public string GetDefaultText(bool isResize, bool isPrefix)
        {
            if (isResize)
                return isPrefix ? AppConstants.DefaultResizePrefix : AppConstants.DefaultResizeSuffix;
            return isPrefix ? AppConstants.DefaultCropPrefix : AppConstants.DefaultCropSuffix;
        }
    }
}
