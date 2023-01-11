using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Data;
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
        private IPlayniteAPI _PlayniteApi;
        protected static IResourceProvider resources = new ResourceProvider();

        private BackgroundChangerDatabase PluginDatabase = BackgroundChanger.PluginDatabase;

        private GameBackgroundImages _gameBackgroundImages { get; set; }
        private List<ItemImage> _backgroundImages { get; set; }
        private List<ItemImage> _backgroundImagesEdited { get; set; }
        private bool _IsCover { get; set; }


        public ImagesManager(IPlayniteAPI PlayniteApi, GameBackgroundImages gameBackgroundImages, bool IsCover)
        {
            _PlayniteApi = PlayniteApi;
            _gameBackgroundImages = gameBackgroundImages;
            _backgroundImages = Serialization.GetClone(gameBackgroundImages.Items.Where(x => x.IsCover == IsCover).ToList());
            _backgroundImagesEdited = Serialization.GetClone(_backgroundImages);
            _IsCover = IsCover;

            InitializeComponent();

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;

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
                // Delete removed
                List<ItemImage> tmpActualList = _backgroundImages.Where(x => !x.IsDefault && x.IsCover == _IsCover).ToList();
                foreach (ItemImage itemImage in tmpActualList)
                {
                    if (_backgroundImagesEdited.Where(x => x.FullPath == itemImage.FullPath).FirstOrDefault() == null)
                    {
                        FileSystem.DeleteFileSafe(itemImage.FullPath);
                    }
                }

                // Add newed
                for (int index = 0; index < _backgroundImagesEdited.Count; index++)
                {
                    ItemImage itemImage = _backgroundImagesEdited[index];

                    if (itemImage.FolderName.IsNullOrEmpty() && !itemImage.IsDefault)
                    {
                        Guid ImageGuid = Guid.NewGuid();
                        string OriginalPath = itemImage.Name;
                        string ext = Path.GetExtension(OriginalPath);

                        itemImage.Name = ImageGuid.ToString() + ext;
                        itemImage.FolderName = _gameBackgroundImages.Id.ToString();
                        itemImage.IsCover = _IsCover;

                        string Dir = Path.GetDirectoryName(itemImage.FullPath);
                        if (!Directory.Exists(Dir))
                        {
                            Directory.CreateDirectory(Dir);
                        }

                        File.Copy(OriginalPath, itemImage.FullPath);
                    }
                }

                // Saved
                List<ItemImage> tmpList = Serialization.GetClone(_gameBackgroundImages.Items.Where(x => x.IsCover != _IsCover).ToList());
                tmpList.AddRange(_backgroundImagesEdited);
                _gameBackgroundImages.Items = tmpList;
                BackgroundChanger.PluginDatabase.Update(_gameBackgroundImages);

                ((Window)this.Parent).Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
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
                _backgroundImagesEdited.RemoveAt(index);

                PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
            }
        }

        private void PART_BtAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> SelectedFiles = _PlayniteApi.Dialogs.SelectFiles("(*.jpg, *.jpeg, *.png)|*.jpg; *.jpeg; *.png|(*.webp)|*.webp|(*.mp4)|*.mp4");

                if (SelectedFiles != null && SelectedFiles.Count > 0)
                {
                    foreach(string FilePath in SelectedFiles)
                    {
                        _backgroundImagesEdited.Add(new ItemImage
                        {
                            Name = FilePath
                        });
                    }
                }

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
            }
        }

        private void PART_BtAddSteamGridDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SteamGridDbType steamGridDbType = SteamGridDbType.heroes;
                if (_IsCover)
                {
                    steamGridDbType = SteamGridDbType.grids;
                }

                SteamGridDbView ViewExtension = new SteamGridDbView(_gameBackgroundImages.Name, steamGridDbType);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, "SteamGridDB", ViewExtension);
                windowExtension.ShowDialog();

                if (ViewExtension.steamGridDbResults != null)
                {
                    GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                        resources.GetString("LOCCommonGettingData"),
                        false
                    );
                    globalProgressOptions.IsIndeterminate = true;

                    GlobalProgressResult ProgressDownload = PluginDatabase.PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                    {
                        ViewExtension.steamGridDbResults.ForEach(x =>
                        {
                            try
                            {
                                string cachedFile = HttpFileCache.GetWebFile(x.url);
                                _backgroundImagesEdited.Add(new ItemImage
                                {
                                    Name = cachedFile
                                });
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, true, "BackgroundChanger");
                            }
                        });
                    }, globalProgressOptions);


                    Task.Run(() =>
                    {
                        while (!(bool)ProgressDownload.Result)
                        {

                        }
                    }).ContinueWith(antecedant => 
                    {
                        this.Dispatcher.BeginInvoke((Action)delegate
                        {
                            PART_LbBackgroundImages.ItemsSource = null;
                            PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
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
                _backgroundImagesEdited.Insert(index - 1, _backgroundImagesEdited[index]);
                _backgroundImagesEdited.RemoveAt(index + 1);

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
            }
        }

        private void PART_BtDown_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            if (index < _backgroundImagesEdited.Count - 1)
            {
                _backgroundImagesEdited.Insert(index + 2, _backgroundImagesEdited[index]);
                _backgroundImagesEdited.RemoveAt(index);

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
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
                    Video.Position = new TimeSpan(0, 0, ((int)Video.NaturalDuration.TimeSpan.TotalSeconds / 2));
                }

                FrameworkElement ElementParent = (FrameworkElement)((FrameworkElement)sender).Parent;
                var ElementWidth = ElementParent.FindName("PART_Width");
                var ElementHeight = ElementParent.FindName("PART_Height");
                ((Label)ElementWidth).Content = ((MediaElement)sender).NaturalVideoWidth;
                ((Label)ElementHeight).Content = ((MediaElement)sender).NaturalVideoHeight;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
            }
        }


        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int index = int.Parse(((TextBlock)sender).Tag.ToString());

            bool newValue = !_backgroundImagesEdited[index].IsFavorite;
            _backgroundImagesEdited.ForEach(c => c.IsFavorite = false);
            _backgroundImagesEdited[index].IsFavorite = newValue;

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
        }


        private string ExtractAnimatedImageAndConvert(string FilePath)
        {
            string VideoPath = string.Empty;

            FileSystem.DeleteDirectory(PluginDatabase.Paths.PluginCachePath);
            FileSystem.CreateDirectory(PluginDatabase.Paths.PluginCachePath);

            try
            {
                if (FilePath != null && FilePath != string.Empty)
                {
                    if (System.IO.Path.GetExtension(FilePath).ToLower().IndexOf("webp") > -1)
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

                        var ffmpeg = $"-r 25 -f "
                            + $"image2 -s {Width}x{Height} -i \"{PluginDatabase.Paths.PluginCachePath}\\FileName_%4d.png\" " 
                            + $"-vcodec libx264 -crf 25 -pix_fmt yuv420p \"{PluginDatabase.Paths.PluginCachePath}\\{FileName}.mp4\"";

                        var process = new Process();
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
                Common.LogError(ex, false, true, "BackgroundChanger");
            }

            return VideoPath;
        }

        private void PART_BtConvert_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());
            string FilePath = _backgroundImagesEdited[index].FullPath;


            string VideoPath = string.Empty;

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                resources.GetString("LOCCommonConverting"),
                false
            );
            globalProgressOptions.IsIndeterminate = true;

            var ProgressDownload = PluginDatabase.PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                try
                {
                    VideoPath = ExtractAnimatedImageAndConvert(FilePath);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "BackgroundChanger");
                }
            }, globalProgressOptions);


            if (!VideoPath.IsNullOrEmpty() && File.Exists(VideoPath))
            {
                PART_BtDelete_Click(sender, e);


                _backgroundImagesEdited.Add(new ItemImage
                {
                    Name = VideoPath
                });

                PART_LbBackgroundImages.ItemsSource = null;
                PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
            }
        }
    }

    public class GetMediaTypeConverter : IValueConverter
    {
        private static ILogger logger = LogManager.GetLogger();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string)
                {
                    if (System.IO.Path.GetExtension((string)value).ToLower().Contains("mp4"))
                    {
                        return "\ueb13";
                    }

                    if (System.IO.Path.GetExtension((string)value).ToLower().Contains("webp"))
                    {
                        return "\ueb16 \ueb13";
                    } 

                    if (System.IO.Path.GetExtension((string)value).ToLower().Contains("png"))
                    {
                        try
                        {
                            Png_Reader pngr = new Png_Reader();
                            Dictionary<fcTL, MemoryStream> m_Apng;
                            using (var fStream = FileSystem.OpenReadFileStreamSafe((string)value))
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
