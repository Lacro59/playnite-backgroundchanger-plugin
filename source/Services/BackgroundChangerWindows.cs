using BackgroundChanger.Models;
using BackgroundChanger.Views;
using CommonPluginsShared;
using CommonPluginsShared.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Windows;

namespace BackgroundChanger.Services
{
    public class BackgroundChangerWindows : PluginWindows
    {
        public BackgroundChangerWindows(string pluginName, BackgroundChangerDatabase pluginDatabase) : base(pluginName, pluginDatabase)
        {
        }

        public void ShowImagesManagerWindow(Game game, BackgroundChangerDatabase.PluginMediaKind mediaKind, BackgroundChanger plugin)
        {
            if (game == null)
            {
                return;
            }

            BackgroundChangerDatabase database = (BackgroundChangerDatabase)PluginDatabase;
            GameBackgroundImages gameData = database.Get(game);
            if (gameData == null)
            {
                return;
            }

            ImagesManager viewExtension = new ImagesManager(gameData, mediaKind, plugin);
            string mediaTitleKey;
            switch (mediaKind)
            {
                case BackgroundChangerDatabase.PluginMediaKind.Cover:
                    mediaTitleKey = "LOCGameCoverImageTitle";
                    break;
                case BackgroundChangerDatabase.PluginMediaKind.Icon:
                    mediaTitleKey = "LOCGameIconTitle";
                    break;
                default:
                    mediaTitleKey = "LOCGameBackgroundTitle";
                    break;
            }

            string title = string.Format(
                "{0} - {1}",
                ResourceProvider.GetString("LOCBc"),
                ResourceProvider.GetString(mediaTitleKey));

            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(title, viewExtension);
            _ = windowExtension.ShowDialog();
        }

        /// <summary>
        /// Opens the images manager for background or cover (legacy bool discriminant).
        /// </summary>
        public void ShowImagesManagerWindow(Game game, bool isCover, BackgroundChanger plugin)
        {
            ShowImagesManagerWindow(
                game,
                isCover ? BackgroundChangerDatabase.PluginMediaKind.Cover : BackgroundChangerDatabase.PluginMediaKind.Background,
                plugin);
        }

        /// <summary>
        /// Opens the bulk options dialog (game filters + media options), then starts download.
        /// </summary>
        /// <param name="plugin">Plugin instance used to persist and resolve settings.</param>
        public void StartBulkMediaDownloadFlow(BackgroundChanger plugin)
        {
            if (plugin == null)
            {
                return;
            }

            BulkMediaDownloadOptions options = BulkMediaDownloadView.ShowDialog(plugin);
            if (options == null)
            {
                return;
            }

            List<Game> games = BulkGameSelectionHelper.ResolveGames(options, PluginDatabase);
            if (games.Count == 0)
            {
                API.Instance.Dialogs.ShowMessage(
                    ResourceProvider.GetString("LOCBcBulkNoGamesSelected"),
                    ResourceProvider.GetString("LOCBcBulkDownloadTitle"));
                return;
            }

            BeginBulkMediaDownload(games, options, plugin);
        }

        /// <summary>
        /// Starts the bulk media download for the selected games.
        /// </summary>
        /// <param name="games">Games to process.</param>
        /// <param name="options">Confirmed bulk options.</param>
        /// <param name="plugin">Plugin instance.</param>
        public void BeginBulkMediaDownload(
            List<Game> games,
            BulkMediaDownloadOptions options,
            BackgroundChanger plugin)
        {
            if (games == null || games.Count == 0 || options == null || plugin == null)
            {
                return;
            }

            options.EnsureInitialized();

            BulkMediaDownloadResult result = null;
            GlobalProgressOptions progressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCBcBulkDownloadTitle"),
                cancelable: true)
            {
                IsIndeterminate = false
            };

            API.Instance.Dialogs.ActivateGlobalProgress(args =>
            {
                BulkMediaDownloadService service = new BulkMediaDownloadService();
                result = service.Run(
                    games,
                    options,
                    args.CancelToken,
                    (current, total, label) =>
                    {
                        args.ProgressMaxValue = total;
                        args.CurrentProgressValue = current;
                        args.Text = string.Format(
                            "{0} ({1}/{2})",
                            label ?? string.Empty,
                            current,
                            total);
                    });
            }, progressOptions);

            if (result == null)
            {
                return;
            }

            string summary = string.Format(
                ResourceProvider.GetString("LOCBcBulkDownloadSummary"),
                result.ImportedCount,
                result.SkippedCount,
                result.ErrorCount);

            if (result.Cancelled)
            {
                summary = ResourceProvider.GetString("LOCBcBulkDownloadCancelled") + Environment.NewLine + summary;
            }

            API.Instance.Dialogs.ShowMessage(
                summary,
                ResourceProvider.GetString("LOCBcBulkDownloadTitle"));
        }
    }
}
