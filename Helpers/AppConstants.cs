namespace BulkCropAndResizeTool.Helpers
{
public static class AppConstants
{
    #region Default Text

    public const string DefaultSrcBoxText = "Write/Paste or Browse the source path of a file/folder ----->";
    public const string DefaultDstBoxText = "Write/Paste or Browse if different from Source folder ------>";

    public const string DefaultFilename = "filename";
    public const string DefaultExtension = ".jpg";

    #endregion

    #region Filename Prefixes

    public const string DefaultCropPrefix = "cropped_";
    public const string DefaultCropSuffix = "_cropped";

    public const string DefaultResizePrefix = "resized_";
    public const string DefaultResizeSuffix = "_resized";

    #endregion

    #region Filename Prefixes

    public const string DefaultSingleCrop = "CROP IMAGE";
    public const string DefaultMultiCrop = "CROP IMAGE(S)";

    public const string DefaultSingleResize = "RESIZE IMAGE";
    public const string DefaultMultiResize = "RESIZE IMAGE(S)";

    #endregion
        
    #region Image Types

    public static readonly string[] ImageExtensions = [ "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff" ];

    #endregion
    }
}