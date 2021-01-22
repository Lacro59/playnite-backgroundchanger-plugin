using BackgroundChanger.Models;
using CommonPluginsShared;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
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
    /// Logique d'interaction pour BackgroudImagesManager.xaml
    /// </summary>
    public partial class BackgroudImagesManager : UserControl
    {
        private IPlayniteAPI _PlayniteApi;

        private GameBackgroundImages _gameBackgroundImages;
        private List<BackgroundImage> _backgroundImages;
        private List<BackgroundImage> _backgroundImagesEdited;


        public BackgroudImagesManager(IPlayniteAPI PlayniteApi, GameBackgroundImages gameBackgroundImages)
        {
            _PlayniteApi = PlayniteApi;
            _gameBackgroundImages = gameBackgroundImages;
            _backgroundImages = gameBackgroundImages.Items;
            _backgroundImagesEdited = new List<BackgroundImage>(_backgroundImages);

            InitializeComponent();

            PART_BackgroundImage.ImgWidth = 600;

            PART_LbBackgroundImages.ItemsSource = null;
            PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
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
                foreach (BackgroundImage backgroundImage in _backgroundImages.Where(x => x.IsDefault == false).ToList())
                {
                    if (_backgroundImagesEdited.IndexOf(backgroundImage) == -1)
                    {
                        File.Delete(backgroundImage.FullPath);
                    }
                }

                // Add newed
                for (int index = 0; index < _backgroundImagesEdited.Count; index++)
                {
                    BackgroundImage backgroundImage = _backgroundImagesEdited[index];

                    if (backgroundImage.FolderName.IsNullOrEmpty() && !backgroundImage.IsDefault)
                    {
                        Guid ImageGuid = Guid.NewGuid();
                        string OriginalPath = backgroundImage.Name;
                        string ext = Path.GetExtension(OriginalPath);

                        backgroundImage.Name = ImageGuid.ToString() + ext;
                        backgroundImage.FolderName = _gameBackgroundImages.Id.ToString();

                        string Dir = Path.GetDirectoryName(backgroundImage.FullPath);
                        if (!Directory.Exists(Dir))
                        {
                            Directory.CreateDirectory(Dir);
                        }

                        File.Copy(OriginalPath, backgroundImage.FullPath);
                    }
                }

                // Saved
                _gameBackgroundImages.Items = _backgroundImagesEdited;
                BackgroundChanger.PluginDatabase.Update(_gameBackgroundImages);

                ((Window)this.Parent).Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "BackgroundChanger");
            }
        }


        private void PART_BtDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PART_BackgroundImage.Source = null;
                PART_LbBackgroundImages.SelectedIndex = -1;
                PART_LbBackgroundImages.ItemsSource = null;

                int index = int.Parse(((Button)sender).Tag.ToString());
                _backgroundImagesEdited.RemoveAt(index);

                PART_LbBackgroundImages.ItemsSource = _backgroundImagesEdited;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "BackgroundChanger");
            }
        }

        private void PART_BtAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> SelectedFiles = _PlayniteApi.Dialogs.SelectFiles("(*jpg, *.jpeg, *.png)|*jpg; *.jpeg; *.png");

                if (SelectedFiles != null && SelectedFiles.Count > 0)
                {
                    foreach(string FilePath in SelectedFiles)
                    {
                        _backgroundImagesEdited.Add(new BackgroundImage
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
                Common.LogError(ex, "BackgroundChanger");
            }
        }


        private void PART_LbBackgroundImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                string FilePath = ((BackgroundImage)PART_LbBackgroundImages.SelectedItem).FullPath;

                if (File.Exists(FilePath))
                {
                    //PART_BackgroundImage.Source = BitmapExtensions.BitmapFromFile(FilePath);

                    PART_BackgroundImage.Source = FilePath;
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
    }
}
