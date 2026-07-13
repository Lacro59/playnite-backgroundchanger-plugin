using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using System;
using System.IO;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Prepares local media files for library import (toolchain validation and animated-to-MP4 conversion).
    /// </summary>
    public class MediaImportService
    {
        private const string LogPrefix = "[MediaImportService]";

        private readonly MediaConversionService _mediaConversionService = new MediaConversionService();

        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        /// <summary>
        /// File extensions accepted when importing images into the library.
        /// </summary>
        public static readonly string[] ValidImportExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".webm"
        };

        /// <summary>
        /// Determines whether the given file extension is supported for import.
        /// </summary>
        /// <param name="extension">File extension with or without a leading dot.</param>
        /// <returns><c>true</c> when the extension is allowed for import.</returns>
        public bool IsValidImportExtension(string extension)
        {
            if (extension.IsNullOrEmpty())
            {
                return false;
            }

            foreach (string validExtension in ValidImportExtensions)
            {
                if (validExtension.IsEqual(extension))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the source file must be converted to MP4 before import.
        /// </summary>
        /// <param name="sourcePath">Local file path.</param>
        /// <returns><c>true</c> when animated media conversion is required.</returns>
        public bool RequiresConversion(string sourcePath)
        {
            return _mediaConversionService.ShouldConvert(sourcePath);
        }

        /// <summary>
        /// Determines whether import must be blocked because animated media requires a missing toolchain.
        /// </summary>
        /// <param name="sourcePath">Local file path.</param>
        /// <returns><c>true</c> when conversion is required but FFmpeg/ffprobe is not configured.</returns>
        public bool IsBlockedByMissingToolkit(string sourcePath)
        {
            return RequiresConversion(sourcePath) && !_mediaConversionService.IsMediaToolkitConfigured();
        }

        /// <summary>
        /// Determines whether import must be blocked because the configured FFmpeg/ffprobe version is too old.
        /// </summary>
        /// <param name="sourcePath">Local file path.</param>
        /// <returns><c>true</c> when conversion is required, the toolchain exists, but its version is unsupported.</returns>
        public bool IsBlockedByOutdatedToolkit(string sourcePath)
        {
            return RequiresConversion(sourcePath)
                && _mediaConversionService.IsMediaToolkitConfigured()
                && !_mediaConversionService.IsMediaToolkitVersionSupported();
        }

        /// <summary>
        /// Gets a short summary of detected FFmpeg and ffprobe versions for user-facing messages.
        /// </summary>
        /// <returns>Detected versions or <c>unknown</c> when parsing failed.</returns>
        public string GetMediaToolkitVersionSummary()
        {
            return _mediaConversionService.GetMediaToolkitVersionSummary();
        }

        /// <summary>
        /// Gets the minimum FFmpeg/ffprobe version required for animated WebP conversion.
        /// </summary>
        /// <returns>Minimum version as a display string.</returns>
        public string GetMinimumMediaToolkitVersionDisplay()
        {
            return _mediaConversionService.GetMinimumMediaToolkitVersionDisplay();
        }

        /// <summary>
        /// Prepares a local file for import. Converts animated media to MP4 when required.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>The preparation outcome and resulting path when successful.</returns>
        public ImportPrepareResult TryPrepareImportPath(string sourcePath)
        {
            if (sourcePath.IsNullOrWhiteSpace() || !File.Exists(sourcePath))
            {
                LogDebug(string.Format("Import conversion skipped, source missing: {0}", sourcePath));
                return ImportPrepareResult.MissingSource();
            }

            if (!RequiresConversion(sourcePath))
            {
                return ImportPrepareResult.Success(sourcePath);
            }

            if (!_mediaConversionService.IsMediaToolkitConfigured())
            {
                LogDebug(string.Format("Import blocked, media toolkit not configured: {0}", sourcePath));
                return ImportPrepareResult.BlockedMissingToolkit();
            }

            if (!_mediaConversionService.IsMediaToolkitVersionSupported())
            {
                LogDebug(string.Format("Import blocked, media toolkit version unsupported: {0}", sourcePath));
                return ImportPrepareResult.BlockedOutdatedToolkit();
            }

            FileSystem.CreateDirectory(PluginDatabase.Paths.PluginCachePath);
            string outputPath = Path.Combine(PluginDatabase.Paths.PluginCachePath, Guid.NewGuid().ToString() + ".mp4");

            try
            {
                string convertedPath = _mediaConversionService.ConvertToMp4Async(sourcePath, outputPath)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (convertedPath.IsNullOrEmpty() || !File.Exists(convertedPath))
                {
                    LogDebug(string.Format("Import conversion failed: {0} -> {1}", sourcePath, outputPath));
                    return ImportPrepareResult.ConversionFailed();
                }

                FileSystem.DeleteFileSafe(sourcePath);
                LogDebug(string.Format("Import converted to MP4: {0} -> {1}", sourcePath, convertedPath));
                return ImportPrepareResult.Success(convertedPath);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                return ImportPrepareResult.ConversionFailed();
            }
        }

        private static void LogDebug(string message)
        {
            Common.LogDebug(false, LogPrefix + " " + message);
        }

        /// <summary>
        /// Outcome of preparing a local file for import.
        /// </summary>
        public sealed class ImportPrepareResult
        {
            private ImportPrepareResult(ImportPrepareStatus status, string preparedPath)
            {
                Status = status;
                PreparedPath = preparedPath;
            }

            /// <summary>
            /// Gets the preparation status.
            /// </summary>
            public ImportPrepareStatus Status { get; }

            /// <summary>
            /// Gets the prepared import path when <see cref="Status"/> is <see cref="ImportPrepareStatus.Success"/>.
            /// </summary>
            public string PreparedPath { get; }

            /// <summary>
            /// Creates a successful result for the given import path.
            /// </summary>
            /// <param name="preparedPath">Path to add to the library.</param>
            /// <returns>A successful preparation result.</returns>
            public static ImportPrepareResult Success(string preparedPath)
            {
                return new ImportPrepareResult(ImportPrepareStatus.Success, preparedPath);
            }

            /// <summary>
            /// Creates a result indicating the source file was missing.
            /// </summary>
            /// <returns>A missing-source result.</returns>
            public static ImportPrepareResult MissingSource()
            {
                return new ImportPrepareResult(ImportPrepareStatus.MissingSource, null);
            }

            /// <summary>
            /// Creates a result indicating the media toolkit is not configured.
            /// </summary>
            /// <returns>A blocked result.</returns>
            public static ImportPrepareResult BlockedMissingToolkit()
            {
                return new ImportPrepareResult(ImportPrepareStatus.BlockedMissingToolkit, null);
            }

            /// <summary>
            /// Creates a result indicating the media toolkit version is too old.
            /// </summary>
            /// <returns>A blocked result.</returns>
            public static ImportPrepareResult BlockedOutdatedToolkit()
            {
                return new ImportPrepareResult(ImportPrepareStatus.BlockedOutdatedToolkit, null);
            }

            /// <summary>
            /// Creates a result indicating conversion failed.
            /// </summary>
            /// <returns>A conversion-failed result.</returns>
            public static ImportPrepareResult ConversionFailed()
            {
                return new ImportPrepareResult(ImportPrepareStatus.ConversionFailed, null);
            }
        }

        /// <summary>
        /// Status returned by <see cref="TryPrepareImportPath"/>.
        /// </summary>
        public enum ImportPrepareStatus
        {
            Success,
            MissingSource,
            BlockedMissingToolkit,
            BlockedOutdatedToolkit,
            ConversionFailed
        }
    }
}
