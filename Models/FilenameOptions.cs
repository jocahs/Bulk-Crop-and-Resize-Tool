using BulkCropAndResizeTool.Helpers;

namespace BulkCropAndResizeTool.Models
{
    public class FilenameOptions
    {
        public string Prefix { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public bool IsPrefixMode { get; set; } = true;
        public bool OverwriteExisting { get; set; } = false;
        public string CustomText { get; set; } = string.Empty;

        public string GetFinalName(string sourceName, string extension)
        {
            if (OverwriteExisting)
                return sourceName + extension;

            if (IsPrefixMode)
                return Prefix + sourceName + extension;
            else
                return sourceName + Suffix + extension;
        }

        public void Reset()
        {
            Prefix = AppConstants.DefaultCropPrefix;
            Suffix = AppConstants.DefaultCropSuffix;
            IsPrefixMode = true;
            OverwriteExisting = false;
            CustomText = string.Empty;
        }
    }
}