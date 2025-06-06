﻿using BackgroundChanger.Models;
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
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using wpf_animatedimage;
using Path = System.IO.Path;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Logique d'interaction pour ImagesManager.xaml
    /// </summary>
    public partial class ImagesManager : UserControl
    {
        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        private GameBackgroundImages GameBackgroundImages { get; set; }
        private List<ItemImage> BackgroundImages { get; set; }
        private List<ItemImage> BackgroundImagesEdited { get; set; }
        private bool IsCover { get; set; }


        public ImagesManager(GameBackgroundImages gameBackgroundImages, bool isCover)
        {
            GameBackgroundImages = gameBackgroundImages;
            BackgroundImages = Serialization.GetClone(gameBackgroundImages.Items.Where(x => x.IsCover == isCover && x.Exist).ToList());
            BackgroundImagesEdited = Serialization.GetClone(BackgroundImages);
            IsCover = isCover;

            InitializeComponent();

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;

            PART_BackgroundImage.UseAnimated = true;
        }


        private void PART_BtCancel_Click(object sender, RoutedEventArgs e)
        {
            ((Window)this.Parent).Close();
        }

        private void PART_BtOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ItemImage originalDefault = BackgroundImages.FirstOrDefault(x => x.IsDefault);

                // Delete removed
                BackgroundImages.Where(x => !x.Name.IsEqual(originalDefault?.Name) && x.IsCover == IsCover)?.ForEach(y =>
                {
                    if (BackgroundImagesEdited.FirstOrDefault(x => x.FullPath == y.FullPath) == null)
                    {
                        FileSystem.DeleteFileSafe(y.FullPath);
                    }
                });

                // Add newed
                for (int index = 0; index < BackgroundImagesEdited.Count; index++)
                {
                    ItemImage itemImage = BackgroundImagesEdited[index];

                    if (itemImage.FolderName.IsNullOrEmpty() && !itemImage.Name.IsEqual(originalDefault?.Name))
                    {
                        Guid ImageGuid = Guid.NewGuid();
                        string OriginalPath = itemImage.Name;
                        string ext = Path.GetExtension(OriginalPath);

                        itemImage.Name = ImageGuid.ToString() + ext;
                        itemImage.FolderName = GameBackgroundImages.Id.ToString();
                        itemImage.IsCover = IsCover;

                        string Dir = Path.GetDirectoryName(itemImage.FullPath);
                        FileSystem.CreateDirectory(Dir);
                        File.Copy(OriginalPath, itemImage.FullPath);
                    }
                }

                // Default
                ItemImage newDefault = BackgroundImagesEdited.FirstOrDefault(x => x.IsDefault);
                if (!originalDefault?.Name.IsEqual(newDefault?.Name) ?? false)
                {
                    // Copy old in exention data
                    string FolderName = GameBackgroundImages.Id.ToString();
                    string newPath = Path.Combine(
                        PluginDatabase.Paths.PluginUserDataPath,
                        "Images",
                        FolderName,
                        Path.GetFileName(originalDefault.Name)
                    );
                    File.Copy(originalDefault.Name, newPath);
                    FileSystem.DeleteFileSafe(originalDefault.Name);

                    // Copy new in game data
                    Game game = GameBackgroundImages.Game;
                    string filePath = API.Instance.Database.AddFile(newDefault.FullPath, game.Id);
                    FileSystem.DeleteFileSafe(newDefault.FullPath);

                    ItemImage originalNew = BackgroundImages.FirstOrDefault(x => x.Name.IsEqual(newDefault.Name));
                    ItemImage newOld = BackgroundImagesEdited.FirstOrDefault(x => x.Name.IsEqual(originalDefault.Name));

                    newOld.Name = Path.GetFileName(originalDefault.Name);
                    newOld.FolderName = FolderName;

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
                tmpList.AddRange(BackgroundImagesEdited);
                GameBackgroundImages.Items = tmpList;
                BackgroundChanger.PluginDatabase.Update(GameBackgroundImages);

                ((Window)this.Parent).Close();
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
                BackgroundImagesEdited.RemoveAt(index);

                PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
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
                List<string> SelectedFiles = API.Instance.Dialogs.SelectFiles("(*.jpg, *.jpeg, *.png)|*.jpg; *.jpeg; *.png|(*.webp)|*.webp|(*.mp4)|*.mp4");

                if (SelectedFiles != null && SelectedFiles.Count > 0)
                {
                    foreach(string FilePath in SelectedFiles)
                    {
                        BackgroundImagesEdited.Add(new ItemImage
                        {
                            Name = FilePath
                        });
                    }
                }

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
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

                SteamGridDbView ViewExtension = new SteamGridDbView(GameBackgroundImages.Name, steamGridDbType);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow("SteamGridDB", ViewExtension);
                _ = windowExtension.ShowDialog();

                if (ViewExtension.SteamGridDbResults != null)
                {
                    GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                        ResourceProvider.GetString("LOCCommonGettingData"),
                        false
                    );
                    globalProgressOptions.IsIndeterminate = true;

                    GlobalProgressResult ProgressDownload = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                    {
                        ViewExtension.SteamGridDbResults.ForEach(x =>
                        {
                            try
                            {
                                string cachedFile = HttpFileCache.GetWebFile(x.Url);
                                BackgroundImagesEdited.Add(new ItemImage
                                {
                                    Name = cachedFile
                                });
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                            }
                        });
                    }, globalProgressOptions);


                    _ = Task.Run(() =>
                    {
                        while (!(bool)ProgressDownload.Result)
                        {

                        }
                    }).ContinueWith(antecedant => 
                    {
                        _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                        {
                            PART_LbBackgroundImages.ItemsSource = null;
                            PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
                        });
                    });
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
                string FilePath = ((ItemImage)PART_LbBackgroundImages.SelectedItem).FullPath;
                if (File.Exists(FilePath))
                {
                    if (Path.GetExtension(FilePath).ToLower().Contains("mp4"))
                    {
                        PART_BackgroundImage.Source = null;
                        PART_Video.Source = new Uri(FilePath);
                    }
                    else
                    {
                        PART_BackgroundImage.Source = FilePath;
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
                BackgroundImagesEdited.Insert(index - 1, BackgroundImagesEdited[index]);
                BackgroundImagesEdited.RemoveAt(index + 1);

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
            }
        }

        private void PART_BtDown_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());
            if (index < BackgroundImagesEdited.Count - 1)
            {
                BackgroundImagesEdited.Insert(index + 2, BackgroundImagesEdited[index]);
                BackgroundImagesEdited.RemoveAt(index);

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
            }
        }

        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                MediaElement Video = sender as MediaElement;
                if (Video.NaturalDuration.HasTimeSpan && Video.NaturalDuration.TimeSpan.TotalSeconds > 2)
                {
                    Video.LoadedBehavior = MediaState.Play;
                    Video.LoadedBehavior = MediaState.Pause;
                    Video.Position = new TimeSpan(0, 0, (int)Video.NaturalDuration.TimeSpan.TotalSeconds / 2);
                }

                FrameworkElement ElementParent = (FrameworkElement)((FrameworkElement)sender).Parent;
                if (ElementParent != null)
                {
                    var ElementWidth = ElementParent.FindName("PART_Width");
                    var ElementHeight = ElementParent.FindName("PART_Height");
                    ((Label)ElementWidth).Content = ((MediaElement)sender).NaturalVideoWidth;
                    ((Label)ElementHeight).Content = ((MediaElement)sender).NaturalVideoHeight;
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

            bool newValue = !BackgroundImagesEdited[index].IsFavorite;
            BackgroundImagesEdited.ForEach(c => c.IsFavorite = false);
            BackgroundImagesEdited[index].IsFavorite = newValue;

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
        }


        private string ExtractAnimatedImageAndConvert(string FilePath)
        {
            string VideoPath = string.Empty;

            FileSystem.CreateDirectory(PluginDatabase.Paths.PluginCachePath, true);

            try
            {
                if (FilePath != null && FilePath != string.Empty)
                {
                    if (Path.GetExtension(FilePath).ToLower().IndexOf("webp") > -1)
                    {
                        WebpAnim webPAnim = new WebpAnim();
                        webPAnim.Load(FilePath);

                        string FileName = Path.GetFileNameWithoutExtension(FilePath);
                        int ActualFrame = 0;
                        while (ActualFrame < webPAnim.FramesCount())
                        {
                            string PathTemp = Path.Combine(PluginDatabase.Paths.PluginCachePath, $"FileName_{ActualFrame:D4}.png");

                            System.Drawing.Image img = System.Drawing.Image.FromStream(webPAnim.GetFrameStream(ActualFrame));
                            img.Save(PathTemp, ImageFormat.Png);

                            ActualFrame++;
                        }


                        double Width = webPAnim.GetFrameBitmapSource(0).Width;
                        double Height = webPAnim.GetFrameBitmapSource(0).Height;

                        string ffmpeg = $"-r 25 -f "
                            + $"image2 -s {Width}x{Height} -i \"{PluginDatabase.Paths.PluginCachePath}\\FileName_%4d.png\" " 
                            + $"-vcodec libx264 -crf 25 -pix_fmt yuv420p \"{PluginDatabase.Paths.PluginCachePath}\\{FileName}.mp4\"";

                        Process process = new Process();
                        process.StartInfo.FileName = PluginDatabase.PluginSettings.Settings.ffmpegFile;
                        process.StartInfo.Arguments = ffmpeg;
                        process.Start();
                        process.WaitForExit();


                        VideoPath = $"{PluginDatabase.Paths.PluginCachePath}\\{FileName}.mp4";
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return VideoPath;
        }

        private void PART_BtConvert_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());
            string FilePath = BackgroundImagesEdited[index].FullPath;


            string VideoPath = string.Empty;

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                ResourceProvider.GetString("LOCCommonConverting"),
                false
            );
            globalProgressOptions.IsIndeterminate = true;

            GlobalProgressResult ProgressDownload = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                try
                {
                    VideoPath = ExtractAnimatedImageAndConvert(FilePath);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }, globalProgressOptions);


            if (!VideoPath.IsNullOrEmpty() && File.Exists(VideoPath))
            {
                PART_BtDelete_Click(sender, e);


                BackgroundImagesEdited.Add(new ItemImage
                {
                    Name = VideoPath
                });

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
            }
        }

        private void PART_BtDefault_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            BackgroundImagesEdited.ForEach(c => c.IsDefault = false);
            BackgroundImagesEdited[index].IsDefault = true;

            BackgroundImagesEdited.Insert(0, BackgroundImagesEdited[index]);
            BackgroundImagesEdited.RemoveAt(index + 1);

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
        }

        private void PART_BtAddGoogleImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GoogleImageView ViewExtension = new GoogleImageView(GameBackgroundImages.Name);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow("Google Image", ViewExtension);
                _ = windowExtension.ShowDialog();

                if (ViewExtension.GoogleImageResults?.Count > 0)
                {
                    GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                        ResourceProvider.GetString("LOCCommonGettingData"),
                        false
                    );
                    globalProgressOptions.IsIndeterminate = true;

                    GlobalProgressResult ProgressDownload = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                    {
                        ViewExtension.GoogleImageResults.ForEach(x =>
                        {
                            try
                            {
                                string cachedFile = HttpFileCache.GetWebFile(x.ImageUrl);
                                BackgroundImagesEdited.Add(new ItemImage
                                {
                                    Name = cachedFile
                                });
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                            }
                        });
                    }, globalProgressOptions);


                    _ = Task.Run(() =>
                    {
                        while (!(bool)ProgressDownload.Result)
                        {

                        }
                    }).ContinueWith(antecedant =>
                    {
                        _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                        {
                            PART_LbBackgroundImages.ItemsSource = null;
                            PART_LbBackgroundImages.ItemsSource = BackgroundImagesEdited;
                        });
                    });
                }
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
                    if (Path.GetExtension(@string).ToLower().Contains("mp4"))
                    {
                        return "\ueb13";
                    }

                    if (Path.GetExtension(@string).ToLower().Contains("webp"))
                    {
                        return "\ueb16 \ueb13";
                    }

                    if (Path.GetExtension(@string).ToLower().Contains("png"))
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
