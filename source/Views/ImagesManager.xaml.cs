using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using QSoft.Apng;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

        private readonly MediaConversionService _mediaConversionService = new MediaConversionService();

        private const string ImportLogPrefix = "[ImagesManager]";

        private static readonly string[] ValidImportExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", ".webm"
        };


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

            PART_BackgroundImage.UseAnimated = false;
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

        private bool IsValidImportExtension(string extension)
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
        /// Converts animated media to MP4 when required. Caller must ensure FFmpeg is configured.
        /// </summary>
        private string PrepareImportedMediaPathSync(string sourcePath)
        {
            if (sourcePath.IsNullOrWhiteSpace() || !File.Exists(sourcePath))
            {
                LogImportDebug(string.Format("Import conversion skipped, source missing: {0}", sourcePath));
                return null;
            }

            if (!_mediaConversionService.ShouldConvert(sourcePath))
            {
                return sourcePath;
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
                    LogImportDebug(string.Format("Import conversion failed: {0} -> {1}", sourcePath, outputPath));
                    return null;
                }

                FileSystem.DeleteFileSafe(sourcePath);
                LogImportDebug(string.Format("Import converted to MP4: {0} -> {1}", sourcePath, convertedPath));
                return convertedPath;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                return null;
            }
        }

        private void TryAddImportedItem(string sourcePath, ref bool ffmpegErrorShown)
        {
            if (sourcePath.IsNullOrEmpty() || !File.Exists(sourcePath))
            {
                LogImportDebug(string.Format("Import skipped, file missing: {0}", sourcePath));
                return;
            }

            if (_mediaConversionService.ShouldConvert(sourcePath) && !_mediaConversionService.IsFfmpegConfigured())
            {
                LogImportDebug(string.Format("Import blocked, FFmpeg not configured: {0}", sourcePath));
                ShowFfmpegNotFoundIfNeeded(ref ffmpegErrorShown);
                return;
            }

            string preparedPath = _mediaConversionService.ShouldConvert(sourcePath)
                ? PrepareImportedMediaPathSync(sourcePath)
                : sourcePath;

            if (!preparedPath.IsNullOrEmpty())
            {
                EditedImages.Add(new ItemImage
                {
                    Name = preparedPath
                });

                if (Path.GetExtension(sourcePath).IsEqual(".webp") && preparedPath.IsEqual(sourcePath))
                {
                    LogImportDebug(string.Format("Import added as static WebP (no conversion): {0}", sourcePath));
                }
            }
            else
            {
                LogImportDebug(string.Format("Import item skipped, no output produced: {0}", sourcePath));
            }
        }

        private static void LogImportDebug(string message)
        {
            Common.LogDebug(false, ImportLogPrefix + " " + message);
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
                        if (_mediaConversionService.ShouldConvert(itemImage.Name))
                        {
                            if (!_mediaConversionService.IsFfmpegConfigured())
                            {
                                API.Instance.Dialogs.ShowErrorMessage(
                                    ResourceProvider.GetString("LOCBcFfmpegNotFound"),
                                    PluginDatabase.PluginName);
                                return;
                            }

                            string convertedPath = PrepareImportedMediaPathSync(itemImage.Name);
                            if (convertedPath.IsNullOrEmpty())
                            {
                                return;
                            }

                            itemImage.Name = convertedPath;
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
                List<string> pendingFiles = new List<string>();
                foreach (string filePath in selectedFiles)
                {
                    if (_mediaConversionService.ShouldConvert(filePath) && !_mediaConversionService.IsFfmpegConfigured())
                    {
                        ShowFfmpegNotFoundIfNeeded(ref ffmpegErrorShown);
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
                    if (_mediaConversionService.ShouldConvert(filePath))
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
                        foreach (string filePath in pendingFiles)
                        {
                            TryAddImportedItem(filePath, ref unusedFfmpegErrorShown);
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
                        viewExtension.SteamGridDbResults.ForEach(x =>
                        {
                            try
                            {
                                string cachedFile = HttpFileCache.GetWebFile(x.Url);
                                TryAddImportedItem(cachedFile, ref ffmpegErrorShown);
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
                        viewExtension.GoogleImageResults.ForEach(x =>
                        {
                            try
                            {
                                string cachedFile = HttpFileCache.GetWebFile(x.ImageUrl);
                                TryAddImportedItem(cachedFile, ref ffmpegErrorShown);
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
                        if (!IsValidImportExtension(extension))
                        {
                            Logger.Warn($"The file {cachedFile} is not a valid image.");
                            return;
                        }

                        bool ffmpegErrorShown = false;
                        TryAddImportedItem(cachedFile, ref ffmpegErrorShown);
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

    public class GetMediaTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string @string)
                {
                    if (Path.GetExtension(@string).IsEqual("mp4"))
                    {
                        return "\ueb13";
                    }

                    if (Path.GetExtension(@string).IsEqual("webp"))
                    {
                        return "\ueb16 \ueb13";
                    }

                    if (Path.GetExtension(@string).IsEqual("png"))
                    {
                        try
                        {
                            Png_Reader pngr = new Png_Reader();
                            Dictionary<fcTL, MemoryStream> m_Apng;
                            using (Stream fStream = FileSystem.OpenReadFileStreamSafe(@string))
                            {
                                m_Apng = pngr.Open(fStream).SpltAPng();
                            }

                            // Animated
                            if (m_Apng.Count > 0)
                            {
                                return "\ueb16 \ueb13";
                            }
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, true);
                        }
                    }

                    return "\ueb16";
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}