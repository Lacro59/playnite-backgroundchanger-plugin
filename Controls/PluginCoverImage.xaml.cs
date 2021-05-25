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
            BcTimer = null;

            ControlDataContext = new PluginCoverImageDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.Settings.EnableCoverImage,
                UseAnimated = PluginDatabase.PluginSettings.Settings.EnableImageAnimatedCover,
                EnableRandomSelect = PluginDatabase.PluginSettings.Settings.EnableCoverImageRandomSelect,
                EnableAutoChanger = PluginDatabase.PluginSettings.Settings.EnableCoverImageAutoChanger,

                ImageSource = null,
                VideoSource = null
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

        // TODO Get after settings modification
        private void GetCoverProperties()
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
                                    if (propBackChangerImage != null)
                                    {
                                        var value = propImageBackground.GetValue(PART_ImageCover, null);
                                        propBackChangerImage.SetValue(this, value, null);
                                    }
                                    else
                                    {
                                        logger.Warn($"No property for {propImageBackground.Name}");
                                    }

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
                            this.DataContext = ControlDataContext;
                            return;
                        }

                        SetCover();
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
                string PathImage = ImageSourceManager.GetImagePath(PluginDatabase.GameContext.CoverImage);
                if (PathImage.IsNullOrEmpty())
                {
                    PathImage = PluginDatabase.PlayniteApi.Database.GetFullFilePath(PluginDatabase.GameContext.CoverImage);
                }

                SetCoverImage(PathImage);
            }
        }

        public void SetCoverImage(string PathImage = null)
        {
            //this.Dispatcher?.BeginInvoke(DispatcherPriority.Render, (Action)delegate
            //{
            //    if (!File.Exists(PathImage))
            //    {
            //        PathImage = null;
            //    }
            //
            //    this.Source = PathImage;
            //});

            if (!File.Exists(PathImage))
            {
                ControlDataContext.ImageSource = null;
                ControlDataContext.VideoSource = null;
                return;
            }
            
            
            if (System.IO.Path.GetExtension(PathImage).ToLower().Contains("mp4"))
            {
                ControlDataContext.ImageSource = null;
                ControlDataContext.VideoSource = PathImage;
            }
            else
            {
                ControlDataContext.ImageSource = PathImage;
                ControlDataContext.VideoSource = null;
            }

            this.Dispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
            {
                this.DataContext = ControlDataContext;
            }));
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

        #region StretchDirection
        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(PluginCoverImage),
            new PropertyMetadata(StretchDirection.Both));

        public StretchDirection StretchDirection
        {
            get { return (StretchDirection)GetValue(StretchDirectionProperty); }
            set { SetValue(StretchDirectionProperty, value); }
        }
        #endregion StretchDirection


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

            if (newSource != null && newSource is string)
            {
                image = (string)currentSource;
            }

            if (System.IO.Path.GetExtension(image).ToLower().Contains("mp4"))
            {
                Image1.Source = null;
                Video1.Source = new Uri(image);
            }
            else
            {
                Image1.Source = image;
                Video1.Source = null;
            }
        }


        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                string PathImage = string.Empty;

                if (ControlDataContext.EnableRandomSelect)
                {
                    if (gameBackgroundImages.ItemsBackground.Count != 0)
                    {
                        Random rnd = new Random();
                        int ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsCover.Count));
                        while (ImgSelected == Counter && gameBackgroundImages.ItemsCover.Count != 1)
                        {
                            ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsCover.Count));
                        }
                        Counter = ImgSelected;

                        PathImage = gameBackgroundImages.ItemsCover[ImgSelected].FullPath;
                    }

                    SetCoverImage(PathImage);
                }
                else
                {
                    Counter++;

                    if (gameBackgroundImages.ItemsBackground.Count != 0)
                    {
                        if (Counter == gameBackgroundImages.ItemsCover.Count)
                        {
                            Counter = 0;
                        }

                        PathImage = gameBackgroundImages.ItemsCover[Counter].FullPath;
                    }

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
            GetCoverProperties();

            // Activate/Deactivated animation
            Application.Current.Activated += Application_Activated;
            Application.Current.Deactivated += Application_Deactivated;
        }


        #region Activate/Deactivated animation
        private void Application_Deactivated(object sender, EventArgs e)
        {
            Video1.LoadedBehavior = MediaState.Pause;

            if (BcTimer != null)
            {
                BcTimer.Stop();
            }
        }

        private void Application_Activated(object sender, EventArgs e)
        {
            Video1.LoadedBehavior = MediaState.Play;

            if (BcTimer != null)
            {
                BcTimer.Start();
            }
        }
        #endregion
    }


    public class PluginCoverImageDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool UseAnimated { get; set; }
        public bool EnableRandomSelect { get; set; }
        public bool EnableAutoChanger { get; set; }

        public string ImageSource { get; set; }
        public string VideoSource { get; set; }
    }
}
