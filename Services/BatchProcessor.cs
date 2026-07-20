using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BulkCropAndResizeTool.Services
{
    public interface IBatchProcessor
    {
        Task ProcessBatchAsync(
            List<string> files,
            string outputFolder,
            bool isResize,
            string unit,
            double angle,
            int outputWidth,
            int outputHeight,
            int marginLeft,
            int marginTop,
            int sourceWidth,
            int sourceHeight,
            string prefixText,
            bool isPrefixMode,
            bool overwrite,
            IProgress<int> progress,
            Action<string> logAction,
            Func<string, string, bool> shouldSkipFile,
            CancellationToken cancellationToken = default);
    }
    public class BatchProcessor(LoggingService logger) : IBatchProcessor
    {
        private readonly LoggingService _logger = logger;

        public async Task ProcessBatchAsync(
            List<string> files, string outputFolder, bool isResize, string unit, double angle,
            int outputWidth, int outputHeight, int marginLeft, int marginTop,
            int sourceWidth, int sourceHeight, string prefixText, bool isPrefixMode, bool overwrite,
            IProgress<int> progress, Action<string> logAction, Func<string, string, bool> shouldSkipFile,
            CancellationToken cancellationToken = default)
        {
            int processed = 0;
            int total = files.Count;

            double? percentW = null;
            double? percentH = null;
            if (unit == "%")
            {
                percentW = outputWidth * 100.0 / sourceWidth;
                percentH = outputHeight * 100.0 / sourceHeight;
            }

            await Task.Run(() =>
            {
                foreach (string filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var image = ImageProcessingService.LoadImageFromFile(filePath);
                    if (image == null)
                    {
                        logAction?.Invoke($"Failed to load: {System.IO.Path.GetFileName(filePath)}");
                        processed++;
                        progress?.Report(processed);
                        continue;
                    }

                    var processedImage = ImageProcessingService.ProcessImage(
                        image, isResize, unit, angle, outputWidth, outputHeight,
                        marginLeft, marginTop, percentW, percentH);

                    if (processedImage == null)
                    {
                        logAction?.Invoke($"Failed to process: {System.IO.Path.GetFileName(filePath)}");
                        processed++;
                        progress?.Report(processed);
                        continue;
                    }

                    var (saveFileName, savePath, ext) = FileService.GetOutputFileInfo(
                        filePath, outputFolder, prefixText ?? "", overwrite, isPrefixMode);

                    if (shouldSkipFile?.Invoke(saveFileName, savePath) == true)
                    {
                        logAction?.Invoke($"Skipped: {saveFileName}");
                        processed++;
                        progress?.Report(processed);
                        continue;
                    }

                    try
                    {
                        ImageProcessingService.SaveImage(processedImage, savePath, ext);
                        logAction?.Invoke($"Saved: {saveFileName}");
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke($"Error saving {saveFileName}: {ex.Message}");
                    }

                    processed++;
                    progress?.Report(processed);
                }
            }, cancellationToken);

            progress?.Report(total);
            logAction?.Invoke($"Batch processing completed. {processed} files processed.");
        }

    }
}