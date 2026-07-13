using BackgroundChanger.Models;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Runs the one-time startup batch that converts existing animated library media to MP4.
    /// </summary>
    public class MediaConversionStartupService
    {
        private const string FfmpegNotificationId = "BackgroundChanger-FfmpegMissing";
        private const string FfmpegOutdatedNotificationId = "BackgroundChanger-FfmpegOutdated";
        private const string MigrationMarkerFileName = ".animated-media-migration.done";
        private const string LogPrefix = "[MediaConversionStartup]";

        private readonly MediaConversionService _mediaConversionService = new MediaConversionService();

        /// <summary>
        /// Converts legacy animated media once at application startup or notifies when FFmpeg is missing.
        /// Subsequent startups skip the batch when the migration marker file exists.
        /// </summary>
        /// <param name="database">Plugin database instance.</param>
        /// <param name="pluginId">Plugin identifier used to open settings from the notification.</param>
        public void RunStartupBatch(BackgroundChangerDatabase database, Guid pluginId)
        {
            if (database == null)
            {
                return;
            }

            if (!database.IsDatabaseReady())
            {
                LogDebugMessage("Database not ready, skipping startup batch.");
                return;
            }

            string migrationMarkerPath = GetMigrationMarkerPath(database);
            bool migrationComplete = IsMigrationComplete(migrationMarkerPath);

            if (!_mediaConversionService.IsMediaToolkitConfigured())
            {
                List<PendingMediaConversion> pendingItems = CollectPendingConversions(database);
                bool hasConfiguredPath = !database.PluginSettings.ffmpegFile.IsNullOrWhiteSpace()
                    || !database.PluginSettings.ffprobeFile.IsNullOrWhiteSpace();

                if (pendingItems.Count > 0 || hasConfiguredPath)
                {
                    string ffmpegPath = FormatConfiguredPath(
                        database.PluginSettings.ffmpegFile,
                        _mediaConversionService.IsFfmpegConfigured());
                    string ffprobePath = FormatConfiguredPath(
                        database.PluginSettings.ffprobeFile,
                        _mediaConversionService.IsFfprobeConfigured(),
                        _mediaConversionService.ResolveFfprobePath());

                    LogDebugMessage(string.Format(
                        "Media toolkit incomplete (FFmpeg: {0}, ffprobe: {1}). Batch aborted with {2} pending item(s). Migration complete: {3}.",
                        ffmpegPath,
                        ffprobePath,
                        pendingItems.Count,
                        migrationComplete));

                    NotifyMediaToolkitMissing(database.PluginName, ffmpegPath, ffprobePath, pluginId);
                }

                return;
            }

            if (!_mediaConversionService.IsMediaToolkitVersionSupported())
            {
                List<PendingMediaConversion> pendingItems = CollectPendingConversions(database);
                bool hasConfiguredPath = !database.PluginSettings.ffmpegFile.IsNullOrWhiteSpace()
                    || !database.PluginSettings.ffprobeFile.IsNullOrWhiteSpace();

                if (pendingItems.Count > 0 || hasConfiguredPath)
                {
                    LogDebugMessage(string.Format(
                        "Media toolkit version unsupported (detected: {0}, minimum: {1}). Batch aborted with {2} pending item(s). Migration complete: {3}.",
                        _mediaConversionService.GetMediaToolkitVersionSummary(),
                        _mediaConversionService.GetMinimumMediaToolkitVersionDisplay(),
                        pendingItems.Count,
                        migrationComplete));

                    NotifyMediaToolkitOutdated(database.PluginName, pluginId);
                }

                return;
            }

            if (migrationComplete)
            {
                LogDebugMessage("Migration already complete, skipping startup batch.");
                return;
            }

            List<PendingMediaConversion> pendingConversions = CollectPendingConversions(database);
            LogDebugMessage(string.Format("Found {0} library media item(s) requiring conversion.", pendingConversions.Count));

            if (pendingConversions.Count == 0)
            {
                LogDebugMessage("No animated media to convert, marking migration complete.");
                MarkMigrationComplete(migrationMarkerPath);
                return;
            }

            LogDebugMessage(string.Format("Starting startup batch conversion of {0} item(s).", pendingConversions.Count));

            GlobalProgressOptions progressOptions = new GlobalProgressOptions(
                string.Format("{0} - {1}", database.PluginName, ResourceProvider.GetString("LOCCommonConverting")))
            {
                Cancelable = false,
                IsIndeterminate = false
            };

            API.Instance.Dialogs.ActivateGlobalProgress(progress =>
            {
                progress.ProgressMaxValue = pendingConversions.Count;
                HashSet<Guid> updatedGameIds = new HashSet<Guid>();
                int convertedCount = 0;
                int failedCount = 0;

                foreach (PendingMediaConversion pending in pendingConversions)
                {
                    try
                    {
                        if (TryConvertLibraryItem(pending.Item))
                        {
                            convertedCount++;
                            updatedGameIds.Add(pending.GameImages.Id);
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        Common.LogError(ex, false, true, database.PluginName);
                    }
                    finally
                    {
                        progress.CurrentProgressValue++;
                    }
                }

                foreach (Guid gameId in updatedGameIds)
                {
                    GameBackgroundImages gameImages = database.GetOnlyCache(gameId);
                    if (gameImages != null)
                    {
                        database.Update(gameImages);
                    }
                }

                if (failedCount == 0)
                {
                    MarkMigrationComplete(migrationMarkerPath);
                }
                else
                {
                    LogDebugMessage(string.Format(
                        "Migration marker not written: {0} conversion(s) failed.",
                        failedCount));
                }

                LogDebugMessage(string.Format(
                    "Startup batch complete: {0} converted, {1} failed, {2} total.",
                    convertedCount,
                    failedCount,
                    pendingConversions.Count));
            }, progressOptions);
        }

        private static string GetMigrationMarkerPath(BackgroundChangerDatabase database)
        {
            return Path.Combine(database.Paths.PluginUserDataPath, MigrationMarkerFileName);
        }

        private static bool IsMigrationComplete(string migrationMarkerPath)
        {
            return File.Exists(migrationMarkerPath);
        }

        private static void MarkMigrationComplete(string migrationMarkerPath)
        {
            string markerDirectory = Path.GetDirectoryName(migrationMarkerPath);
            if (!markerDirectory.IsNullOrEmpty())
            {
                FileSystem.CreateDirectory(markerDirectory);
            }

            File.WriteAllText(migrationMarkerPath, DateTime.UtcNow.ToString("o"));
        }

        private List<PendingMediaConversion> CollectPendingConversions(BackgroundChangerDatabase database)
        {
            List<PendingMediaConversion> pendingItems = new List<PendingMediaConversion>();

            foreach (GameBackgroundImages gameImages in database.GetAllCache())
            {
                if (gameImages?.Items == null)
                {
                    continue;
                }

                foreach (ItemImage item in gameImages.Items)
                {
                    if (!item.Exist)
                    {
                        continue;
                    }

                    if (_mediaConversionService.ShouldConvert(item.FullPath))
                    {
                        pendingItems.Add(new PendingMediaConversion(gameImages, item));
                    }
                }
            }

            return pendingItems;
        }

        private bool TryConvertLibraryItem(ItemImage item)
        {
            string sourcePath = item.FullPath;
            if (sourcePath.IsNullOrWhiteSpace() || !File.Exists(sourcePath))
            {
                LogDebugMessage(string.Format("Conversion skipped, source file missing: {0}", sourcePath));
                return false;
            }

            string outputDirectory = Path.GetDirectoryName(sourcePath);
            if (outputDirectory.IsNullOrEmpty())
            {
                LogDebugMessage(string.Format("Conversion skipped, output directory missing for: {0}", sourcePath));
                return false;
            }

            string outputPath = Path.Combine(outputDirectory, Guid.NewGuid().ToString() + ".mp4");
            string convertedPath = _mediaConversionService.ConvertToMp4Async(sourcePath, outputPath)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (convertedPath.IsNullOrEmpty() || !File.Exists(convertedPath))
            {
                LogDebugMessage(string.Format("Conversion failed for library item: {0} -> {1}", sourcePath, outputPath));
                return false;
            }

            FileSystem.DeleteFileSafe(sourcePath);

            if (item.FolderName.IsNullOrEmpty())
            {
                item.Name = convertedPath;
            }
            else
            {
                item.Name = Path.GetFileName(convertedPath);
            }

            return true;
        }

        private static void NotifyMediaToolkitOutdated(string pluginName, Guid pluginId)
        {
            MediaConversionService mediaConversionService = new MediaConversionService();
            string message = string.Format(
                ResourceProvider.GetString("LOCBcMediaToolkitOutdatedNotification"),
                mediaConversionService.GetMinimumMediaToolkitVersionDisplay(),
                mediaConversionService.GetMediaToolkitVersionSummary());

            API.Instance.Notifications.Add(new NotificationMessage(
                FfmpegOutdatedNotificationId,
                pluginName + Environment.NewLine + message,
                NotificationType.Error,
                () =>
                {
                    Plugin plugin = API.Instance.Addons.Plugins.FirstOrDefault(x => x.Id == pluginId);
                    if (plugin != null)
                    {
                        _ = plugin.OpenSettingsView();
                    }
                }));
        }

        private static void NotifyMediaToolkitMissing(string pluginName, string ffmpegPath, string ffprobePath, Guid pluginId)
        {
            string message = string.Format(
                ResourceProvider.GetString("LOCBcMediaToolkitStartupNotification"),
                ffmpegPath,
                ffprobePath);

            API.Instance.Notifications.Add(new NotificationMessage(
                FfmpegNotificationId,
                pluginName + Environment.NewLine + message,
                NotificationType.Error,
                () =>
                {
                    Plugin plugin = API.Instance.Addons.Plugins.FirstOrDefault(x => x.Id == pluginId);
                    if (plugin != null)
                    {
                        _ = plugin.OpenSettingsView();
                    }
                }));
        }

        private static string FormatConfiguredPath(string configuredPath, bool isValid, string resolvedPath = null)
        {
            if (!configuredPath.IsNullOrWhiteSpace())
            {
                return isValid ? configuredPath : configuredPath + " (not found)";
            }

            if (!resolvedPath.IsNullOrWhiteSpace())
            {
                return resolvedPath + (isValid ? " (auto)" : " (not found)");
            }

            return ResourceProvider.GetString("LOCBcFfmpegPathNotConfigured");
        }

        private static void LogDebugMessage(string message)
        {
            Common.LogDebug(false, LogPrefix + " " + message);
        }

        private sealed class PendingMediaConversion
        {
            public PendingMediaConversion(GameBackgroundImages gameImages, ItemImage item)
            {
                GameImages = gameImages;
                Item = item;
            }

            public GameBackgroundImages GameImages { get; }

            public ItemImage Item { get; }
        }
    }
}
