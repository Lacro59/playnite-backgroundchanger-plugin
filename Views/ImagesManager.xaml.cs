using APNG;
using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPluginsPlaynite;
using CommonPluginsPlaynite.Common;
using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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

        private GameBackgroundImages _gameBackgroundImages;
        private List<ItemImage> _backgroundImages;
        private List<ItemImage> _backgroundImagesEdited;
        private bool _IsCover;


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
                var tmpActualList = _backgroundImages.Where(x => !x.IsDefault && x.IsCover == _IsCover).ToList();
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
                var tmpList = Serialization.GetClone(_gameBackgroundImages.Items.Where(x => x.IsCover != _IsCover).ToList());
                tmpList.AddRange(_backgroundImagesEdited);
                _gameBackgroundImages.Items = tmpList;
                BackgroundChanger.PluginDatabase.Update(_gameBackgroundImages);

                ((Window)this.Parent).Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
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
                Common.LogError(ex, false);
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
                Common.LogError(ex, false);
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

                var ViewExtension = new SteamGridDbView(_gameBackgroundImages.Name, steamGridDbType);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, "SteamGridDB", ViewExtension);
                windowExtension.ShowDialog();

                if (ViewExtension.steamGridDbResult != null)
                {
                    GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                        resources.GetString("LOCCommonGettingData"),
                        false
                    );
                    globalProgressOptions.IsIndeterminate = true;

                    var ProgressDownload = PluginDatabase.PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                    {
                        try
                        {
                            var cachedFile = HttpFileCache.GetWebFile(ViewExtension.steamGridDbResult.url);
                            _backgroundImagesEdited.Add(new ItemImage
                            {
                                Name = cachedFile
                            });
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, false);
                        }
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
                Common.LogError(ex, false);
            }
        }


        private void PART_LbBackgroundImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
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
                        //PART_BackgroundImage.Source = BitmapExtensions.BitmapFromFile(FilePath);
                        PART_BackgroundImage.Source = FilePath;
                        PART_Video.Source = null;
                    }
                }
            }
            catch
            {

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
                var Video = sender as MediaElement;
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
                Common.LogError(ex, false);
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
                        CPng_Reader pngr = new CPng_Reader();
                        using (var fStream = FileSystem.OpenReadFileStreamSafe((string)value))
                        {
                            var m_Apng = pngr.Open(fStream).SpltAPng();
                        }

                        // Animated
                        if (pngr.Chunks.Count != 0)
                        {
                            return "\ueb16 \ueb13";
                        }
                    } 

                    return "\ueb16";
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
