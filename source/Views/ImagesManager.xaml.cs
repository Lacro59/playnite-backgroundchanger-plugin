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
        private bool IsCover { get; set; }

        private readonly MediaImportService _mediaImportService = new MediaImportService();


        public ImagesManager(GameBackgroundImages gameBackgroundImages, bool isCover, BackgroundChanger plugin)
        {
            GameBackgroundImages = gameBackgroundImages;
            CurrentImages = Serialization.GetClone(gameBackgroundImages.Items.Where(x => x.IsCover == isCover && x.Exist).ToList());
            EditedImages = Serialization.GetClone(CurrentImages);
            IsCover = isCover;
            Plugin = plugin;

            InitializeComponent();

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = EditedImages;
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

        private void PART_BtOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ItemImage originalDefault = CurrentImages.FirstOrDefault(x => x.IsDefault);

                // Delete removed
                CurrentImages.Where(x => !x.Name.IsEqual(originalDefault?.Name) && x.IsCover == IsCover)?.ForEach(y =>
                {
                    if (EditedImages.FirstOrDefault(x => x.FullPath == y.FullPath) == null)
                    {
                        FileSystem.DeleteFileSafe(y.FullPath);
                    }
                });

                // Add newed
                for (int index = 0; index < EditedImages.Count; index++)
                {
                    ItemImage itemImage = EditedImages[index];

                    if (itemImage.FolderName.IsNullOrEmpty() && !itemImage.Name.IsEqual(originalDefault?.Name))
                    {
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
                        itemImage.IsCover = IsCover;

                        string dir = Path.GetDirectoryName(itemImage.FullPath);
                        FileSystem.CreateDirectory(dir);
                        File.Copy(originalPath, itemImage.FullPath);
                    }
                }

                // Default
                ItemImage newDefault = EditedImages.FirstOrDefault(x => x.IsDefault);
                if (!originalDefault?.Name.IsEqual(newDefault?.Name) ?? false)
                {
                    // Copy old in exention data
                    string folderName = GameBackgroundImages.Id.ToString();
                    string newPath = Path.Combine(
                        PluginDatabase.Paths.PluginUserDataPath,
                        "Images",
                        folderName,
                        Path.GetFileName(originalDefault.Name)
                    );
                    File.Copy(originalDefault.Name, newPath);
                    FileSystem.DeleteFileSafe(originalDefault.Name);

                    // Copy new in game data
                    Game game = GameBackgroundImages.Game;
                    string filePath = API.Instance.Database.AddFile(newDefault.FullPath, game.Id);
                    FileSystem.DeleteFileSafe(newDefault.FullPath);

                    ItemImage originalNew = CurrentImages.FirstOrDefault(x => x.Name.IsEqual(newDefault.Name));
                    ItemImage newOld = EditedImages.FirstOrDefault(x => x.Name.IsEqual(originalDefault.Name));

                    newOld.Name = Path.GetFileName(originalDefault.Name);
                    newOld.FolderName = folderName;

                    if (IsCover)
                    {
                        game.CoverImage = filePath;
                    }
                    else
                    {
                        game.BackgroundImage = filePath;
                    }
                    API.Instance.Database.Games.Update(game);
                    newDefault.Name = API.Instance.Database.GetFullFilePath(filePath);
                    newDefault.FolderName = null;
                }

                // Saved
                List<ItemImage> tmpList = Serialization.GetClone(GameBackgroundImages.Items.Where(x => x.IsCover != IsCover).ToList());
                tmpList.AddRange(EditedImages);
                GameBackgroundImages.Items = tmpList;
                BackgroundChanger.PluginDatabase.Update(GameBackgroundImages);

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

        private void PART_BtAddSteamGridDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SteamGridDbType steamGridDbType = SteamGridDbType.heroes;
                if (IsCover)
                {
                    steamGridDbType = SteamGridDbType.grids;
                }

                SteamGridDbView viewExtension = new SteamGridDbView(GameBackgroundImages.Name, steamGridDbType, Plugin);
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
                        PART_BackgroundImage.Source = filePath;
                        PART_Video.Source = null;
                    }
                }
            }
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