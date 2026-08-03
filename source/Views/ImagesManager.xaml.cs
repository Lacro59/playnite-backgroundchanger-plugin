using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Path = System.IO.Path;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Logique d'interaction pour ImagesManager.xaml
    /// </summary>
    public partial class ImagesManager : UserControl
    {
        private static ILogger Logger => LogManager.GetLogger();

        private BackgroundChanger Plugin { get; }
        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        private GameBackgroundImages GameBackgroundImages { get; set; }
        private List<ItemImage> CurrentImages { get; set; }
        private List<ItemImage> EditedImages { get; set; }
        private ItemImage OpeningDefaultSnapshot { get; set; }
        private BackgroundChangerDatabase.PluginMediaKind MediaKind { get; set; }

        private readonly MediaImportService _mediaImportService = new MediaImportService();


        public ImagesManager(GameBackgroundImages gameBackgroundImages, BackgroundChangerDatabase.PluginMediaKind mediaKind, BackgroundChanger plugin)
        {
            GameBackgroundImages = gameBackgroundImages;
            CurrentImages = Serialization.GetClone(
                gameBackgroundImages.Items.Where(x => BackgroundChangerDatabase.ItemMatchesMediaKind(x, mediaKind) && x.Exist).ToList());
            EditedImages = Serialization.GetClone(CurrentImages);
            OpeningDefaultSnapshot = Serialization.GetClone(EditedImages.FirstOrDefault(x => x.IsDefault));
            MediaKind = mediaKind;
            Plugin = plugin;

            InitializeComponent();

            PART_BackgroundImage.SizeChanged += PART_BackgroundImage_SizeChanged;
            ConfigureSteamOfficialButton(mediaKind);

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = EditedImages;
        }

        private void ConfigureSteamOfficialButton(BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            if (mediaKind == BackgroundChangerDatabase.PluginMediaKind.Icon)
            {
                PART_BtAddSteamOfficial.Content = ResourceProvider.GetString("LOCBcSteamGet");
            }
            else
            {
                PART_BtAddSteamOfficial.Content = ResourceProvider.GetString("LOCBcSteamSelect");
            }
        }

        private void PART_BackgroundImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(PART_BackgroundImage?.Source))
            {
                return;
            }

            UpdatePreviewDecodePixelHeight();
        }


        #region Media import

        private void RefreshEditedImagesList()
        {
            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = EditedImages;
        }

        private void WaitProgressAndRefreshList(GlobalProgressResult progressDownload)
        {
            _ = Task.Run(() =>
            {
                while (!(bool)progressDownload.Result)
                {
                }
            }).ContinueWith(antecedent =>
            {
                _ = API.Instance.MainView.UIDispatcher?.BeginInvoke((Action)delegate
                {
                    RefreshEditedImagesList();
                });
            });
        }

        private void ShowFfmpegNotFoundIfNeeded(ref bool ffmpegErrorShown)
        {
            if (ffmpegErrorShown)
            {
                return;
            }

            API.Instance.Dialogs.ShowErrorMessage(
                ResourceProvider.GetString("LOCBcFfmpegNotFound"),
                PluginDatabase.PluginName);
            ffmpegErrorShown = true;
        }

        private void ShowFfmpegOutdatedIfNeeded(ref bool ffmpegOutdatedShown)
        {
            if (ffmpegOutdatedShown)
            {
                return;
            }

            string message = string.Format(
                ResourceProvider.GetString("LOCBcMediaToolkitOutdatedNotification"),
                _mediaImportService.GetMinimumMediaToolkitVersionDisplay(),
                _mediaImportService.GetMediaToolkitVersionSummary());

            API.Instance.Dialogs.ShowErrorMessage(message, PluginDatabase.PluginName);
            ffmpegOutdatedShown = true;
        }

        private void ShowFfmpegConversionFailedIfNeeded(ref bool conversionFailedShown)
        {
            if (conversionFailedShown)
            {
                return;
            }

            string message = string.Format(
                ResourceProvider.GetString("LOCBcFfmpegConversionFailed"),
                _mediaImportService.GetMediaToolkitVersionSummary(),
                _mediaImportService.GetMinimumMediaToolkitVersionDisplay());

            API.Instance.Dialogs.ShowErrorMessage(message, PluginDatabase.PluginName);
            conversionFailedShown = true;
        }

        private void TryAddImportedItem(string sourcePath, ref bool ffmpegErrorShown, ref bool ffmpegOutdatedShown, ref bool conversionFailedShown)
        {
            MediaImportService.ImportPrepareResult result = _mediaImportService.TryPrepareImportPath(sourcePath);

            switch (result.Status)
            {
                case MediaImportService.ImportPrepareStatus.BlockedMissingToolkit:
                    Common.LogDebug(false, string.Format("[ImagesManager] Import blocked, media toolkit not configured: {0}", sourcePath));
                    ShowFfmpegNotFoundIfNeeded(ref ffmpegErrorShown);
                    return;

                case MediaImportService.ImportPrepareStatus.BlockedOutdatedToolkit:
                    Common.LogDebug(false, string.Format("[ImagesManager] Import blocked, media toolkit version outdated: {0}", sourcePath));
                    ShowFfmpegOutdatedIfNeeded(ref ffmpegOutdatedShown);
                    return;

                case MediaImportService.ImportPrepareStatus.MissingSource:
                    Common.LogDebug(false, string.Format("[ImagesManager] Import skipped, file missing: {0}", sourcePath));
                    return;

                case MediaImportService.ImportPrepareStatus.ConversionFailed:
                    Common.LogDebug(false, string.Format("[ImagesManager] Import item skipped, no output produced: {0}", sourcePath));
                    ShowFfmpegConversionFailedIfNeeded(ref conversionFailedShown);
                    return;

                case MediaImportService.ImportPrepareStatus.Success:
                    if (!result.PreparedPath.IsNullOrEmpty())
                    {
                        EditedImages.Add(new ItemImage
                        {
                            Name = result.PreparedPath
                        });

                        if (Path.GetExtension(sourcePath).IsEqual(".webp") && result.PreparedPath.IsEqual(sourcePath))
                        {
                            Common.LogDebug(false, string.Format("[ImagesManager] Import added as static WebP (no conversion): {0}", sourcePath));
                        }
                    }
                    break;
            }
        }

        #endregion


        private void PART_BtCancel_Click(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }

        private static bool ItemImageIdentityMatches(ItemImage left, ItemImage right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (!left.Name.IsEqual(right.Name))
            {
                return false;
            }

            // IsEqual("", "") is false — treat both missing folder names as the same identity
            // (Playnite mirrors and pending imports use an empty FolderName).
            string leftFolder = left.FolderName ?? string.Empty;
            string rightFolder = right.FolderName ?? string.Empty;
            if (leftFolder.Length == 0 && rightFolder.Length == 0)
            {
                return true;
            }

            return leftFolder.IsEqual(rightFolder);
        }

        private static bool EditedImagesContains(List<ItemImage> editedImages, ItemImage candidate)
        {
            return editedImages.Exists(x => ItemImageIdentityMatches(x, candidate));
        }

        private static bool IsPlayniteLibraryMirror(ItemImage itemImage)
        {
            return BackgroundChangerDatabase.IsPlayniteLibraryMirror(itemImage);
        }

        private static string FormatSaveItemLine(ItemImage item, int index)
        {
            if (item == null)
            {
                return string.Format("  [{0}] null", index);
            }

            return string.Format(
                "  [{0}] name={1}, folder={2}, cover={3}, icon={4}, default={5}, fav={6}, exist={7}, mirror={8}",
                index,
                item.Name ?? string.Empty,
                item.FolderName ?? string.Empty,
                item.IsCover,
                item.IsIcon,
                item.IsDefault,
                item.IsFavorite,
                item.Exist,
                IsPlayniteLibraryMirror(item));
        }

        private static void LogSaveItemsSnapshot(string phase, string gameName, BackgroundChangerDatabase.PluginMediaKind mediaKind, IList<ItemImage> items)
        {
            int count = items?.Count ?? 0;
            Common.LogDebug(
                false,
                string.Format("[ImagesManager] Save {0} — game='{1}', mediaKind={2}, count={3}", phase, gameName, mediaKind, count));

            if (items == null || count == 0)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                Common.LogDebug(false, string.Format("[ImagesManager] Save {0} {1}", phase, FormatSaveItemLine(items[i], i)));
            }
        }

        private void PART_BtOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string gameName = GameBackgroundImages?.Name ?? GameBackgroundImages?.Id.ToString() ?? "?";
                LogSaveItemsSnapshot("before-current", gameName, MediaKind, CurrentImages);
                LogSaveItemsSnapshot("before-edited", gameName, MediaKind, EditedImages);

                ItemImage originalDefault = OpeningDefaultSnapshot;
                string playniteMediaReference = null;
                int importedCount = 0;
                int skippedMirrorCount = 0;

                // Delete removed — never delete Playnite library files (owned by Playnite metadata)
                CurrentImages.Where(x => !ItemImageIdentityMatches(x, originalDefault) && BackgroundChangerDatabase.ItemMatchesMediaKind(x, MediaKind))?.ForEach(y =>
                {
                    if (IsPlayniteLibraryMirror(y))
                    {
                        return;
                    }

                    if (!EditedImagesContains(EditedImages, y))
                    {
                        Common.LogDebug(
                            false,
                            string.Format("[ImagesManager] Save delete removed file: {0}", y.FullPath ?? string.Empty));
                        FileSystem.DeleteFileSafe(y.FullPath);
                    }
                });

                // Add newed
                for (int index = 0; index < EditedImages.Count; index++)
                {
                    ItemImage itemImage = EditedImages[index];

                    if (itemImage.FolderName.IsNullOrEmpty() && !ItemImageIdentityMatches(itemImage, originalDefault))
                    {
                        // Playnite library\files paths are never materialized into plugin Images/.
                        // Do not skip merely because IsDefault is set — file-picker imports often
                        // mark default before FolderName is assigned (that used to look like a mirror).
                        if (BackgroundChangerDatabase.IsUnderPlayniteLibraryFiles(itemImage.Name))
                        {
                            skippedMirrorCount++;
                            Common.LogDebug(
                                false,
                                string.Format(
                                    "[ImagesManager] Save import skipped (Playnite library file): {0}",
                                    itemImage.Name ?? string.Empty));
                            continue;
                        }

                        if (_mediaImportService.RequiresConversion(itemImage.Name))
                        {
                            if (_mediaImportService.IsBlockedByMissingToolkit(itemImage.Name))
                            {
                                API.Instance.Dialogs.ShowErrorMessage(
                                    ResourceProvider.GetString("LOCBcFfmpegNotFound"),
                                    PluginDatabase.PluginName);
                                return;
                            }

                            if (_mediaImportService.IsBlockedByOutdatedToolkit(itemImage.Name))
                            {
                                API.Instance.Dialogs.ShowErrorMessage(
                                    string.Format(
                                        ResourceProvider.GetString("LOCBcMediaToolkitOutdatedNotification"),
                                        _mediaImportService.GetMinimumMediaToolkitVersionDisplay(),
                                        _mediaImportService.GetMediaToolkitVersionSummary()),
                                    PluginDatabase.PluginName);
                                return;
                            }

                            MediaImportService.ImportPrepareResult prepareResult =
                                _mediaImportService.TryPrepareImportPath(itemImage.Name);
                            if (prepareResult.Status == MediaImportService.ImportPrepareStatus.ConversionFailed)
                            {
                                API.Instance.Dialogs.ShowErrorMessage(
                                    string.Format(
                                        ResourceProvider.GetString("LOCBcFfmpegConversionFailed"),
                                        _mediaImportService.GetMediaToolkitVersionSummary(),
                                        _mediaImportService.GetMinimumMediaToolkitVersionDisplay()),
                                    PluginDatabase.PluginName);
                                return;
                            }

                            if (prepareResult.Status != MediaImportService.ImportPrepareStatus.Success
                                || prepareResult.PreparedPath.IsNullOrEmpty())
                            {
                                return;
                            }

                            itemImage.Name = prepareResult.PreparedPath;
                        }

                        Guid imageGuid = Guid.NewGuid();
                        string originalPath = itemImage.Name;
                        string ext = Path.GetExtension(originalPath);

                        itemImage.Name = imageGuid.ToString() + ext;
                        itemImage.FolderName = GameBackgroundImages.Id.ToString();
                        BackgroundChangerDatabase.SetItemMediaKind(itemImage, MediaKind);

                        string dir = Path.GetDirectoryName(itemImage.FullPath);
                        FileSystem.CreateDirectory(dir);
                        File.Copy(originalPath, itemImage.FullPath);
                        importedCount++;
                        Common.LogDebug(
                            false,
                            string.Format(
                                "[ImagesManager] Save import copied: {0} -> {1}",
                                originalPath,
                                itemImage.FullPath));
                    }
                }

                // Default — originalDefault is the snapshot at open; newDefault is the user selection
                ItemImage newDefault = EditedImages.FirstOrDefault(x => x.IsDefault);
                if (newDefault != null && !ItemImageIdentityMatches(originalDefault, newDefault))
                {
                    string folderName = GameBackgroundImages.Id.ToString();

                    if (originalDefault != null && originalDefault.Exist)
                    {
                        bool playniteOwnedMirror = IsPlayniteLibraryMirror(originalDefault);
                        string sourcePath = originalDefault.FullPath;
                        string archivedFileName = playniteOwnedMirror
                            ? Guid.NewGuid().ToString() + Path.GetExtension(sourcePath)
                            : Path.GetFileName(sourcePath);
                        string archivePath = Path.Combine(
                            PluginDatabase.Paths.PluginUserDataPath,
                            "Images",
                            folderName,
                            archivedFileName);
                        FileSystem.CreateDirectory(Path.GetDirectoryName(archivePath));
                        File.Copy(sourcePath, archivePath, overwrite: true);

                        // Playnite owns library\files — never delete those. Plugin-owned former
                        // defaults are moved (copy then delete) into the Images folder.
                        if (!playniteOwnedMirror)
                        {
                            FileSystem.DeleteFileSafe(sourcePath);
                        }

                        ItemImage archivedDefault = EditedImages.FirstOrDefault(x => ItemImageIdentityMatches(x, originalDefault));
                        if (archivedDefault != null)
                        {
                            archivedDefault.Name = archivedFileName;
                            archivedDefault.FolderName = folderName;
                            archivedDefault.IsDefault = false;
                            BackgroundChangerDatabase.SetItemMediaKind(archivedDefault, MediaKind);
                        }
                        else
                        {
                            ItemImage archivedItem = new ItemImage
                            {
                                Name = archivedFileName,
                                FolderName = folderName
                            };
                            BackgroundChangerDatabase.SetItemMediaKind(archivedItem, MediaKind);
                            EditedImages.Add(archivedItem);
                        }

                        Common.LogDebug(
                            false,
                            string.Format(
                                "[ImagesManager] Save default swap — archived former default: {0} -> {1} (playniteOwned={2})",
                                sourcePath,
                                archivePath,
                                playniteOwnedMirror));
                    }

                    if (!newDefault.FolderName.IsNullOrEmpty() && newDefault.Exist)
                    {
                        playniteMediaReference = API.Instance.Database.AddFile(newDefault.FullPath, GameBackgroundImages.Game.Id);
                        FileSystem.DeleteFileSafe(newDefault.FullPath);
                        EditedImages.Remove(newDefault);
                    }
                }

                LogSaveItemsSnapshot("after-edited", gameName, MediaKind, EditedImages);

                // Saved
                List<ItemImage> otherMediaItems = GameBackgroundImages.Items
                    .Where(x => !BackgroundChangerDatabase.ItemMatchesMediaKind(x, MediaKind))
                    .ToList();
                List<ItemImage> tmpList = Serialization.GetClone(otherMediaItems);
                tmpList.AddRange(EditedImages);
                GameBackgroundImages.Items = tmpList;

                int savedMediaCount = EditedImages.Count;
                int otherMediaCount = otherMediaItems.Count;
                Common.LogDebug(
                    false,
                    string.Format(
                        "[ImagesManager] Save merge — game='{1}', mediaKind={2}, edited={0}, otherKinds={3}, total={4}, imported={5}, skippedMirror={6}, defaultSwap={7}",
                        savedMediaCount,
                        gameName,
                        MediaKind,
                        otherMediaCount,
                        tmpList.Count,
                        importedCount,
                        skippedMirrorCount,
                        !playniteMediaReference.IsNullOrEmpty()));

                LogSaveItemsSnapshot("after-merged", gameName, MediaKind, EditedImages);

                BackgroundChanger.PluginDatabase.Update(GameBackgroundImages);

                Common.LogDebug(
                    false,
                    string.Format(
                        "[ImagesManager] Save persisted — game='{0}', mediaKind={1}, totalItems={2}, playniteRef={3}",
                        gameName,
                        MediaKind,
                        GameBackgroundImages.Items?.Count ?? 0,
                        playniteMediaReference ?? "(none)"));

                if (!playniteMediaReference.IsNullOrEmpty())
                {
                    Game game = GameBackgroundImages.Game;
                    switch (MediaKind)
                    {
                        case BackgroundChangerDatabase.PluginMediaKind.Cover:
                            game.CoverImage = playniteMediaReference;
                            break;
                        case BackgroundChangerDatabase.PluginMediaKind.Icon:
                            game.Icon = playniteMediaReference;
                            break;
                        default:
                            game.BackgroundImage = playniteMediaReference;
                            break;
                    }

                    BackgroundChangerDatabase.SuppressGamesItemUpdatedPersist = true;
                    try
                    {
                        API.Instance.Database.Games.Update(game);
                    }
                    finally
                    {
                        BackgroundChangerDatabase.SuppressGamesItemUpdatedPersist = false;
                    }
                }

                ((Window)Parent).Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }


        private void PART_BtDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PART_BackgroundImage.Source = null;
                PART_Video.Source = null;
                PART_LbBackgroundImages.SelectedIndex = -1;
                PART_LbBackgroundImages.ItemsSource = null;

                int index = int.Parse(((Button)sender).Tag.ToString());
                EditedImages.RemoveAt(index);

                PART_LbBackgroundImages.ItemsSource = EditedImages;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void PART_BtAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> selectedFiles = API.Instance.Dialogs.SelectFiles("(*.jpg, *.jpeg, *.png)|*.jpg; *.jpeg; *.png|(*.webp)|*.webp|(*.gif)|*.gif|(*.webm)|*.webm|(*.mp4)|*.mp4");

                if (selectedFiles == null || selectedFiles.Count == 0)
                {
                    return;
                }

                bool ffmpegErrorShown = false;
                bool ffmpegOutdatedShown = false;
                List<string> pendingFiles = new List<string>();
                foreach (string filePath in selectedFiles)
                {
                    if (_mediaImportService.IsBlockedByMissingToolkit(filePath))
                    {
                        ShowFfmpegNotFoundIfNeeded(ref ffmpegErrorShown);
                        continue;
                    }

                    if (_mediaImportService.IsBlockedByOutdatedToolkit(filePath))
                    {
                        ShowFfmpegOutdatedIfNeeded(ref ffmpegOutdatedShown);
                        continue;
                    }

                    pendingFiles.Add(filePath);
                }

                if (pendingFiles.Count == 0)
                {
                    return;
                }

                bool needsConversion = false;
                foreach (string filePath in pendingFiles)
                {
                    if (_mediaImportService.RequiresConversion(filePath))
                    {
                        needsConversion = true;
                        break;
                    }
                }

                if (needsConversion)
                {
                    GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString("LOCCommonConverting"))
                    {
                        Cancelable = false,
                        IsIndeterminate = true
                    };

                    GlobalProgressResult progressDownload = API.Instance.Dialogs.ActivateGlobalProgress(activateGlobalProgress =>
                    {
                        bool unusedFfmpegErrorShown = false;
                        bool unusedFfmpegOutdatedShown = false;
                        bool unusedConversionFailedShown = false;
                        foreach (string filePath in pendingFiles)
                        {
                            TryAddImportedItem(
                                filePath,
                                ref unusedFfmpegErrorShown,
                                ref unusedFfmpegOutdatedShown,
                                ref unusedConversionFailedShown);
                        }
                    }, globalProgressOptions);

                    WaitProgressAndRefreshList(progressDownload);
                }
                else
                {
                    foreach (string filePath in pendingFiles)
                    {
                        EditedImages.Add(new ItemImage
                        {
                            Name = filePath
                        });
                    }

                    RefreshEditedImagesList();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void ImportSteamOfficialCandidates(IList<SteamOfficialMediaCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return;
            }

            Common.LogDebug(false, string.Format(
                "[ImagesManager] Steam official import start game={0} mediaKind={1} count={2}",
                GameBackgroundImages?.Name,
                MediaKind,
                candidates.Count));

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString("LOCCommonGettingData"))
            {
                Cancelable = false,
                IsIndeterminate = true
            };

            GlobalProgressResult progressDownload = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                bool ffmpegErrorShown = false;
                bool ffmpegOutdatedShown = false;
                bool conversionFailedShown = false;
                foreach (SteamOfficialMediaCandidate candidate in candidates)
                {
                    try
                    {
                        Common.LogDebug(false, string.Format(
                            "[ImagesManager] Steam official import item kind={0} url={1}",
                            candidate.AssetKind,
                            candidate.Url));

                        string cachedFile = HttpFileCache.GetWebFile(candidate.Url);
                        TryAddImportedItem(
                            cachedFile,
                            ref ffmpegErrorShown,
                            ref ffmpegOutdatedShown,
                            ref conversionFailedShown);

                        Common.LogDebug(false, string.Format(
                            "[ImagesManager] Steam official import cached kind={0} file={1}",
                            candidate.AssetKind,
                            cachedFile));
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }
                }
            }, globalProgressOptions);

            WaitProgressAndRefreshList(progressDownload);
        }

        private void PART_BtAddSteamOfficial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Game game = GameBackgroundImages?.Game;
                if (game == null)
                {
                    API.Instance.Dialogs.ShowErrorMessage(
                        ResourceProvider.GetString("LOCBcSteamNoAppId"),
                        PluginDatabase.PluginName);
                    return;
                }

                Common.LogDebug(false, string.Format(
                    "[ImagesManager] Steam official open search game={0} mediaKind={1}",
                    GameBackgroundImages.Name,
                    MediaKind));

                SteamOfficialMediaService steamMediaService = new SteamOfficialMediaService();
                SteamSelectView viewExtension = new SteamSelectView(
                    GameBackgroundImages.Name,
                    MediaKind,
                    steamMediaService);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(
                    ResourceProvider.GetString("LOCCommonStoreSteam"),
                    viewExtension);
                _ = windowExtension.ShowDialog();

                if (viewExtension.SelectedResults == null || viewExtension.SelectedResults.Count == 0)
                {
                    Common.LogDebug(false, string.Format(
                        "[ImagesManager] Steam official cancelled game={0} mediaKind={1}",
                        GameBackgroundImages.Name,
                        MediaKind));
                    return;
                }

                Common.LogDebug(false, string.Format(
                    "[ImagesManager] Steam official confirmed game={0} mediaKind={1} count={2}",
                    GameBackgroundImages.Name,
                    MediaKind,
                    viewExtension.SelectedResults.Count));

                ImportSteamOfficialCandidates(viewExtension.SelectedResults);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void PART_BtAddSteamGridDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Common.LogDebug(false, string.Format(
                    "[ImagesManager] Open SteamGridDB game={0} mediaKind={1}",
                    GameBackgroundImages.Name,
                    MediaKind));

                SteamGridDbView viewExtension = new SteamGridDbView(GameBackgroundImages.Name, MediaKind, Plugin);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow("SteamGridDB", viewExtension);
                _ = windowExtension.ShowDialog();

                if (viewExtension.SteamGridDbResults != null)
                {
                    GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString("LOCCommonGettingData"))
                    {
                        Cancelable = false,
                        IsIndeterminate = true
                    };

                    GlobalProgressResult ProgressDownload = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                    {
                        bool ffmpegErrorShown = false;
                        bool ffmpegOutdatedShown = false;
                        bool conversionFailedShown = false;
                        viewExtension.SteamGridDbResults.ForEach(x =>
                        {
                            try
                            {
                                string cachedFile = HttpFileCache.GetWebFile(x.Url);
                                TryAddImportedItem(
                                    cachedFile,
                                    ref ffmpegErrorShown,
                                    ref ffmpegOutdatedShown,
                                    ref conversionFailedShown);
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                            }
                        });
                    }, globalProgressOptions);


                    WaitProgressAndRefreshList(ProgressDownload);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }


        private void PART_LbBackgroundImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PART_LbBackgroundImages?.SelectedItem != null)
            {
                string filePath = ((ItemImage)PART_LbBackgroundImages.SelectedItem).FullPath;
                if (File.Exists(filePath))
                {
                    if (Path.GetExtension(filePath).IsEqual(".mp4"))
                    {
                        PART_BackgroundImage.Source = null;
                        PART_Video.Source = new Uri(filePath);
                    }
                    else
                    {
                        UpdatePreviewDecodePixelHeight();
                        PART_BackgroundImage.Source = filePath;
                        PART_Video.Source = null;
                    }
                }
            }
        }

        /// <summary>
        /// Sets <see cref="CommonPluginsShared.Controls.ImageAsync.DecodePixelHeight"/> from the preview panel size
        /// so <c>Parameter="0"</c> uses a resolution bucket matching the display area (avoids the default 200 px stretch blur).
        /// </summary>
        private void UpdatePreviewDecodePixelHeight()
        {
            if (PART_BackgroundImage == null)
            {
                return;
            }

            double displayHeight = PART_BackgroundImage.ActualHeight;
            if (double.IsNaN(displayHeight) || displayHeight <= 0)
            {
                displayHeight = PART_BackgroundImage.RenderSize.Height;
            }

            if (displayHeight <= 0)
            {
                FrameworkElement parent = PART_BackgroundImage.Parent as FrameworkElement;
                if (parent != null)
                {
                    displayHeight = parent.ActualHeight;
                }
            }

            if (displayHeight <= 0)
            {
                return;
            }

            PART_BackgroundImage.DecodePixelHeight = Math.Max(256, Math.Round(displayHeight));
        }


        private void PART_BtUp_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());
            if (index > 1)
            {
                EditedImages.Insert(index - 1, EditedImages[index]);
                EditedImages.RemoveAt(index + 1);

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = EditedImages;
            }
        }

        private void PART_BtDown_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());
            if (index < EditedImages.Count - 1)
            {
                EditedImages.Insert(index + 2, EditedImages[index]);
                EditedImages.RemoveAt(index);

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = EditedImages;
            }
        }

        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                MediaElement video = sender as MediaElement;
                if (video.NaturalDuration.HasTimeSpan && video.NaturalDuration.TimeSpan.TotalSeconds > 2)
                {
                    video.LoadedBehavior = MediaState.Play;
                    video.LoadedBehavior = MediaState.Pause;
                    video.Position = new TimeSpan(0, 0, (int)video.NaturalDuration.TimeSpan.TotalSeconds / 2);
                }

                FrameworkElement elementParent = (FrameworkElement)((FrameworkElement)sender).Parent;
                if (elementParent != null)
                {
                    var elementWidth = elementParent.FindName("PART_Width");
                    var elementHeight = elementParent.FindName("PART_Height");
                    ((Label)elementWidth).Content = ((MediaElement)sender).NaturalVideoWidth;
                    ((Label)elementHeight).Content = ((MediaElement)sender).NaturalVideoHeight;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }


        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int index = int.Parse(((TextBlock)sender).Tag.ToString());

            if (!EditedImages[index].Exist)
            {
                return;
            }

            bool newValue = !EditedImages[index].IsFavorite;
            EditedImages.ForEach(c => c.IsFavorite = false);
            EditedImages[index].IsFavorite = newValue;

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = EditedImages;
        }


        private void PART_BtDefault_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            EditedImages.ForEach(c => c.IsDefault = false);
            EditedImages[index].IsDefault = true;

            EditedImages.Insert(0, EditedImages[index]);
            EditedImages.RemoveAt(index + 1);

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = EditedImages;
        }

        private void PART_BtAddGoogleImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GoogleImageView viewExtension = new GoogleImageView(GameBackgroundImages.Name);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow("Google Image", viewExtension);
                _ = windowExtension.ShowDialog();

                if (viewExtension.GoogleImageResults?.Count > 0)
                {
                    GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString("LOCCommonGettingData"))
                    {
                        Cancelable = false,
                        IsIndeterminate = true
                    };

                    GlobalProgressResult ProgressDownload = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                    {
                        bool ffmpegErrorShown = false;
                        bool ffmpegOutdatedShown = false;
                        bool conversionFailedShown = false;
                        viewExtension.GoogleImageResults.ForEach(x =>
                        {
                            try
                            {
                                string cachedFile = HttpFileCache.GetWebFile(x.ImageUrl);
                                TryAddImportedItem(
                                    cachedFile,
                                    ref ffmpegErrorShown,
                                    ref ffmpegOutdatedShown,
                                    ref conversionFailedShown);
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                            }
                        });
                    }, globalProgressOptions);


                    WaitProgressAndRefreshList(ProgressDownload);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void PART_BtAddFromUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StringSelectionDialogResult urlSelection = API.Instance.Dialogs.SelectString(
                    ResourceProvider.GetString("LOCBcAddFromUrlDescription"),
                    ResourceProvider.GetString("LOCBcAddFromUrl"),
                    string.Empty);

                if (!urlSelection.Result)
                {
                    return;
                }

                // Validate URL format
                if (!Uri.TryCreate(urlSelection.SelectedString, UriKind.Absolute, out Uri uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    API.Instance.Dialogs.ShowErrorMessage(
                        ResourceProvider.GetString("LOCBcInvalidUrlFormat"),
                        PluginDatabase.PluginName);
                    return;
                }

                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString("LOCCommonGettingData"))
                {
                    Cancelable = false,
                    IsIndeterminate = true
                };

                GlobalProgressResult ProgressDownload = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                {
                    try
                    {
                        string cachedFile = HttpFileCache.GetWebFile(urlSelection.SelectedString);
                        if (cachedFile.IsNullOrEmpty())
                        {
                            return;
                        }

                        string extension = Path.GetExtension(cachedFile).ToLower();
                        if (!_mediaImportService.IsValidImportExtension(extension))
                        {
                            Logger.Warn($"The file {cachedFile} is not a valid image.");
                            return;
                        }

                        bool ffmpegErrorShown = false;
                        bool ffmpegOutdatedShown = false;
                        bool conversionFailedShown = false;
                        TryAddImportedItem(
                            cachedFile,
                            ref ffmpegErrorShown,
                            ref ffmpegOutdatedShown,
                            ref conversionFailedShown);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }
                }, globalProgressOptions);


                WaitProgressAndRefreshList(ProgressDownload);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }
    }
}