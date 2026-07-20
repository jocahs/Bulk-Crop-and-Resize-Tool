using BulkCropAndResizeTool.Helpers;
using BulkCropAndResizeTool.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
    public class BatchProcessor(ImageProcessingService imageService, LoggingService logger) : IBatchProcessor
    {
        private readonly ImageProcessingService _imageService = imageService;
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

                    var image = _imageService.LoadImageFromFile(filePath);
                    if (image == null)
                    {
                        logAction?.Invoke($"Failed to load: {System.IO.Path.GetFileName(filePath)}");
                        processed++;
                        progress?.Report(processed);
                        continue;
                    }

                    var processedImage = _imageService.ProcessImage(
                        image, isResize, unit, angle, outputWidth, outputHeight,
                        marginLeft, marginTop, percentW, percentH);

                    if (processedImage == null)
                    {
                        logAction?.Invoke($"Failed to process: {System.IO.Path.GetFileName(filePath)}");
                        processed++;
                        progress?.Report(processed);
                        continue;
                    }

                    var (saveFileName, savePath, ext) = FilenameGenerator.GetOutputFileInfo(
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
                        _imageService.SaveImage(processedImage, savePath, ext);
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