using BulkCropAndResizeTool.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkCropAndResizeTool.Services
{
    public class FilenameGenerator
    {
        public static (string saveFileName, string savePath, string extension) GetOutputFileInfo(string filePath, string outputFolder, string preSufText, bool overwrite, bool usePrefix)
        {
            string originalName = System.IO.Path.GetFileName(filePath);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(originalName);
            string ext = System.IO.Path.GetExtension(originalName);
            if (string.IsNullOrEmpty(ext)) ext = AppConstants.DefaultExtension;

            string finalBase;
            string preSuf = preSufText.Trim();

            if (overwrite)
            {
                finalBase = baseName;
            }
            else if (usePrefix)
            {
                finalBase = $"{preSuf}{baseName}";
            }
            else
            {
                finalBase = $"{baseName}{preSuf}";
            }

            string saveFileName = finalBase + ext;
            string savePath = System.IO.Path.Combine(outputFolder, saveFileName);
            return (saveFileName, savePath, ext);
        }

    }
}
