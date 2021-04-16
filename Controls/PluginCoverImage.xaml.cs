using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Interfaces;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginCoverImage.xaml
    /// </summary>
    public partial class PluginCoverImage : PluginUserControlExtend
    {
        private BackgroundChangerDatabase PluginDatabase = BackgroundChanger.PluginDatabase;
        internal override IPluginDatabase _PluginDatabase
        {
            get
            {
                return PluginDatabase;
            }
            set
            {
                PluginDatabase = (BackgroundChangerDatabase)_PluginDatabase;
            }
        }

        private PluginCoverImageDataContext ControlDataContext;
        internal override IDataContext _ControlDataContext
        {
            get
            {
                return ControlDataContext;
            }
            set
            {
                ControlDataContext = (PluginCoverImageDataContext)_ControlDataContext;
            }
        }

        private System.Timers.Timer BcTimer;
        private int Counter = 0;
        private GameBackgroundImages gameBackgroundImages;


        public override void SetDefaultDataContext()
        {
            ControlDataContext = new PluginCoverImageDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.Settings.EnableCoverImage,
                UseAnimated = PluginDatabase.PluginSettings.Settings.EnableImageAnimatedCover,
                EnableRandomSelect = PluginDatabase.PluginSettings.Settings.EnableCoverImageRandomSelect,
                EnableAutoChanger = PluginDatabase.PluginSettings.Settings.EnableCoverImageAutoChanger
            };
        }


        public PluginCoverImage()
        {
            InitializeComponent();

            PluginDatabase.PluginSettings.PropertyChanged += PluginSettings_PropertyChanged;
            PluginDatabase.Database.ItemUpdated += Database_ItemUpdated;
            PluginDatabase.Database.ItemCollectionChanged += Database_ItemCollectionChanged;
            PluginDatabase.PlayniteApi.Database.Games.ItemUpdated += Games_ItemUpdated;

            // Apply settings
            PluginSettings_PropertyChanged(null, null);
        }

        private void GetFadeImageProperties()
        {
            System.Threading.SpinWait.SpinUntil(() =>
            {
                FrameworkElement PART_ImageCover = IntegrationUI.SearchElementByName("PART_ImageCover", false, false);

                if (PART_ImageCover != null)
                {
                    PropertyInfo[] ImageCoverProperties = PART_ImageCover.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    PropertyInfo[] backChangerImageProperties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    List<string> UsedProperties = new List<string>
                    {
                        "Stretch", "StretchDirection"
                    };

                    foreach (PropertyInfo propImageBackground in ImageCoverProperties)
                    {
                        if (propImageBackground.CanWrite)
                        {
                            if (UsedProperties.Contains(propImageBackground.Name))
                            {
                                var propBackChangerImage = backChangerImageProperties.Where(x => x.Name == propImageBackground.Name).FirstOrDefault();

                                try
                                {
                                    var value = propImageBackground.GetValue(PART_ImageCover, null);
                                    propBackChangerImage.SetValue(this, value, null);

                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, false);
                                }
                            }
                        }
                    }
                }

                return PART_ImageCover != null;
            }, 5000);
        }


        public override Task<bool> SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            return Task.Run(() =>
            {
                gameBackgroundImages = (GameBackgroundImages)PluginGameData;

                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    try
                    {
                        if (!gameBackgroundImages.HasDataCover)
                        {
                            MustDisplay = false;
                            return;
                        }

                        SetCover();
                        this.DataContext = ControlDataContext;
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }));

                return true;
            });
        }


        public void SetCover()
        {
            string PathImage = string.Empty;

            if (BcTimer != null)
            {
                Counter = 0;
                BcTimer.Stop();
                BcTimer = null;
            }
            
            if (gameBackgroundImages.HasDataCover)
            {
                if (ControlDataContext.EnableAutoChanger)
                {
                    if (ControlDataContext.EnableRandomSelect)
                    {
                        Random rnd = new Random();
                        int ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsCover.Count));
                        PathImage = gameBackgroundImages.ItemsCover[ImgSelected].FullPath;
                    }
                    else
                    {
                        PathImage = gameBackgroundImages.ItemsCover[Counter].FullPath;
                    }

                    SetCoverImage(PathImage);

                    BcTimer = new System.Timers.Timer(PluginDatabase.PluginSettings.Settings.CoverImageAutoChangerTimer * 1000);
                    BcTimer.AutoReset = true;
                    BcTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                    BcTimer.Start();
                }
                else if (ControlDataContext.EnableRandomSelect)
                {
                    Random rnd = new Random();
                    int ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsCover.Count));
                    PathImage = gameBackgroundImages.ItemsCover[ImgSelected].FullPath;

                    SetCoverImage(PathImage);
                }
                else
                {
                    SetDefaultCoverImage();
                }
            }
            else
            {
                SetDefaultCoverImage();
            }
        }

        public void SetDefaultCoverImage()
        {
            if (PluginDatabase.GameContext.CoverImage.IsNullOrEmpty())
            {
                SetCoverImage();
            }
            else
            {
                string PathImage = PluginDatabase.PlayniteApi.Database.GetFullFilePath(PluginDatabase.GameContext.CoverImage);
                SetCoverImage(PathImage);
            }
        }

        public void SetCoverImage(string PathImage = null)
        {
            this.Dispatcher?.BeginInvoke(DispatcherPriority.Render, (Action)delegate
            {
                if (!File.Exists(PathImage))
                {
                    PathImage = null;
                }
                this.Source = PathImage;
            });
        }


        #region Source
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(PluginCoverImage),
            new PropertyMetadata(null, SourceChanged));

        public object Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        #endregion Source

        #region Stretch
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(PluginCoverImage),
            new PropertyMetadata(Stretch.UniformToFill));

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }
        #endregion Strech


        private object currentSource = null;

        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var control = (PluginCoverImage)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private async void LoadNewSource(object newSource, object oldSource)
        {
            string image = null;

            if (newSource?.Equals(currentSource) == true)
            {
                return;
            }

            currentSource = newSource;
            //if (newSource != null)
            //{
            //    image = await Task.Factory.StartNew(() =>
            //    {
            //        if (newSource is string str)
            //        {
            //            return ImageSourceManager.GetImage(str, false);
            //        }
            //        else if (newSource is BitmapLoadProperties props)
            //        {
            //            return ImageSourceManager.GetImage(props.Source, false, props);
            //        }
            //        else
            //        {
            //            return null;
            //        }
            //    });
            //}
            if (newSource is string)
            {
                image = (string)currentSource;
            }

            Image1.Source = image;
        }


        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                if (ControlDataContext.EnableRandomSelect)
                {
                    Random rnd = new Random();
                    int ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsCover.Count));
                    while (ImgSelected == Counter)
                    {
                        ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsCover.Count));
                    }
                    Counter = ImgSelected;

                    string PathImage = gameBackgroundImages.ItemsCover[ImgSelected].FullPath;

                    SetCoverImage(PathImage);
                }
                else
                {
                    Counter++;

                    if (Counter == gameBackgroundImages.ItemsCover.Count)
                    {
                        Counter = 0;
                    }

                    string PathImage = gameBackgroundImages.ItemsCover[Counter].FullPath;

                    SetCoverImage(PathImage);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }


        private void ImageHolder_Loaded(object sender, RoutedEventArgs e)
        {
            // Copy FadeImage properties
            GetFadeImageProperties();
        }
    }


    public class PluginCoverImageDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool UseAnimated { get; set; }
        public bool EnableRandomSelect { get; set; }
        public bool EnableAutoChanger { get; set; }
    }
}
