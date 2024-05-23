using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginCoverImage.xaml
    /// </summary>
    public partial class PluginCoverImage : PluginUserControlExtend
    {
        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;
        internal override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginCoverImageDataContext ControlDataContext = new PluginCoverImageDataContext();
        internal override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginCoverImageDataContext)controlDataContext;
        }

        private System.Timers.Timer BcTimer { get; set; }
        private int Counter { get; set; } = 0;
        private GameBackgroundImages gameBackgroundImages { get; set; }

        private bool WindowsIsActivated { get; set; } = true;
        private bool IsFirst { get; set; } = true;


        public override void SetDefaultDataContext()
        {
            if (BcTimer != null)
            {
                Counter = 0;
                BcTimer.Stop();
                BcTimer.Dispose();
                BcTimer = null;
            }

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
            this.DataContext = ControlDataContext;

            PluginDatabase.PluginSettings.PropertyChanged += PluginSettings_PropertyChanged;
            PluginDatabase.Database.ItemUpdated += Database_ItemUpdated;
            PluginDatabase.Database.ItemCollectionChanged += Database_ItemCollectionChanged;
            API.Instance.Database.Games.ItemUpdated += Games_ItemUpdated;

            // Apply settings
            PluginSettings_PropertyChanged(null, null);

            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                EventManager.RegisterClassHandler(typeof(Window), Window.UnloadedEvent, new RoutedEventHandler(WindowBase_UnloadedEvent));
            }
        }


        private void WindowBase_UnloadedEvent(object sender, System.EventArgs e)
        {
            string WinIdProperty = string.Empty;
            string WinName = string.Empty;

            try
            {
                WinIdProperty = ((Window)sender).GetValue(AutomationProperties.AutomationIdProperty).ToString();
                WinName = ((Window)sender).Name;

                if (WinIdProperty == "WindowSettings")
                {
                    GetCoverProperties();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on WindowBase_LoadedEvent for {WinName} - {WinIdProperty}", true, "BackgroundChanger");
            }
        }


        private void GetCoverProperties()
        {
            DependencyObject thisParent = ((FrameworkElement)((FrameworkElement)((FrameworkElement)this.Parent).Parent).Parent).Parent;
            FrameworkElement PART_ImageCover = UI.SearchElementByName("PART_ImageCover", thisParent, false, false);

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
                            PropertyInfo propBackChangerImage = backChangerImageProperties.Where(x => x.Name == propImageBackground.Name).FirstOrDefault();
                            try
                            {
                                if (propBackChangerImage != null)
                                {
                                    object value = propImageBackground.GetValue(PART_ImageCover, null);
                                    propBackChangerImage.SetValue(this, value, null);
                                }
                                else
                                {
                                    Logger.Warn($"No property for {propImageBackground.Name}");
                                }

                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, true, "BackgroundChanger");
                            }
                        }
                    }
                }
            }
        }


        public override void SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            gameBackgroundImages = (GameBackgroundImages)PluginGameData;

            try
            {
                Video1.LoadedBehavior = MediaState.Stop;

                if (!gameBackgroundImages.HasDataCover)
                {
                    MustDisplay = false;
                    return;
                }

                IsFirst = true;
                SetCover();
                IsFirst = false;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
            }
        }


        public void SetCover()
        {
            string PathImage = string.Empty;

            if (gameBackgroundImages.HasDataCover && !PluginDatabase.PluginSettings.Settings.useVideoDelayCoverImage)
            {
                ItemImage ItemFavorite = gameBackgroundImages.ItemsCover.FirstOrDefault(x => x.IsFavorite);

                if (ControlDataContext.EnableAutoChanger)
                {
                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            PathImage = ItemFavorite.FullPath;
                            Counter = gameBackgroundImages.ItemsCover.FindIndex(x => x.IsFavorite);
                        }
                        else
                        {
                            Random rnd = new Random();
                            Counter = rnd.Next(0, gameBackgroundImages.ItemsCover.Count);
                            PathImage = gameBackgroundImages.ItemsCover[Counter].FullPath;
                        }
                    }
                    else
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            PathImage = ItemFavorite.FullPath;
                            Counter = gameBackgroundImages.ItemsCover.FindIndex(x => x.IsFavorite);
                        }
                        else
                        {
                            PathImage = gameBackgroundImages.ItemsCover[Counter].FullPath;
                        }
                    }

                    SetCoverImage(PathImage);

                    BcTimer = new System.Timers.Timer(PluginDatabase.PluginSettings.Settings.CoverImageAutoChangerTimer * 1000);
                    BcTimer.AutoReset = true;
                    BcTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                    BcTimer.Start();
                }
                else if (ControlDataContext.EnableRandomSelect)
                {
                    if (IsFirst && ItemFavorite != null)
                    {
                        PathImage = ItemFavorite.FullPath;
                    }
                    else
                    {
                        Random rnd = new Random();
                        int ImgSelected = rnd.Next(0, (gameBackgroundImages.ItemsCover.Count));
                        PathImage = gameBackgroundImages.ItemsCover[ImgSelected].FullPath;
                    }

                    SetCoverImage(PathImage);
                }
                else
                {
                    if (ItemFavorite != null)
                    {
                        PathImage = ItemFavorite.FullPath;
                        SetCoverImage(PathImage);
                    }
                    else
                    {
                        SetDefaultCoverImage();
                    }
                }
            }
            else
            {
                SetDefaultCoverImage();
            }
        }

        public void SetDefaultCoverImage()
        {
            if (GameContext.CoverImage.IsNullOrEmpty())
            {
                SetCoverImage();
            }
            else
            {
                string PathImage = ImageSourceManager.GetImagePath(GameContext.CoverImage);
                if (PathImage.IsNullOrEmpty())
                {
                    PathImage = API.Instance.Database.GetFullFilePath(GameContext.CoverImage);
                }

                SetCoverImage(PathImage);
            }

            if (PluginDatabase.PluginSettings.Settings.useVideoDelayCoverImage)
            {
                _ = Task.Run(() =>
                {
                    Thread.Sleep(1000 * PluginDatabase.PluginSettings.Settings.videoDelayCoverImage);
                    _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        string PathImage = gameBackgroundImages?.ItemsCover?.Where(x => x.IsVideo)?.OrderBy(x => x.IsFavorite)?.FirstOrDefault()?.FullPath;
                        SetCoverImage(PathImage);
                    }));
                });
            }
        }

        public void SetCoverImage(string PathImage = null)
        {
            if (!File.Exists(PathImage))
            {
                ControlDataContext.ImageSource = null;
                ControlDataContext.VideoSource = null;
                return;
            }


            if (Path.GetExtension(PathImage).ToLower().Contains("mp4"))
            {
                ControlDataContext.ImageSource = null;
                ControlDataContext.VideoSource = PathImage;

                Video1.LoadedBehavior = MediaState.Play;
            }
            else
            {
                ControlDataContext.ImageSource = PathImage;
                ControlDataContext.VideoSource = null;
            }

            _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
            {
                Image1.Source = ControlDataContext.ImageSource;
                Video1.Source = ControlDataContext.VideoSource.IsNullOrEmpty() ? null : new Uri(ControlDataContext.VideoSource);
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
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
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
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
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
            get => (StretchDirection)GetValue(StretchDirectionProperty);
            set => SetValue(StretchDirectionProperty, value);
        }
        #endregion StretchDirection


        private object currentSource = null;

        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            PluginCoverImage control = (PluginCoverImage)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private async void LoadNewSource(object newSource, object oldSource)
        {
            string image = null;

            if (newSource?.Equals(currentSource) == true)
            {
                if (Video1.Source != null)
                {
                    Video1.LoadedBehavior = MediaState.Play;
                }

                return;
            }

            currentSource = newSource;

            if (newSource != null && newSource is string)
            {
                image = (string)currentSource;
            }

            if (Path.GetExtension(image).ToLower().Contains("mp4"))
            {
                Image1.Source = null;
                Video1.Source = new Uri(image);

                Video1.LoadedBehavior = MediaState.Play;
            }
            else
            {
                Image1.Source = image;
                Video1.Source = null;
            }
        }


        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            if (!WindowsIsActivated)
            {
                return;
            }

            try
            {
                _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() => 
                {
                    string PathImage = string.Empty;

                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (gameBackgroundImages.ItemsCover.Count != 0)
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

                        if (gameBackgroundImages.ItemsCover.Count != 0)
                        {
                            if (Counter == gameBackgroundImages.ItemsCover.Count)
                            {
                                Counter = 0;
                            }

                            PathImage = gameBackgroundImages.ItemsCover[Counter].FullPath;
                        }

                        SetCoverImage(PathImage);
                    }
                }));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
            }
        }


        private void ImageHolder_Loaded(object sender, RoutedEventArgs e)
        {
            // Copy FadeImage properties
            GetCoverProperties();

            // Activate/Deactivated animation
            Application.Current.Activated += Application_Activated;
            Application.Current.Deactivated += Application_Deactivated;
            Application.Current.MainWindow.StateChanged += MainWindow_StateChanged;
        }


        #region Activate/Deactivated animation
        private void Application_Deactivated(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                this.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    WindowsIsActivated = false;
                    Video1.LoadedBehavior = MediaState.Pause;

                    if (BcTimer != null)
                    {
                        BcTimer.Stop();
                    }
                }));
            });
        }

        private void Application_Activated(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                this.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    WindowsIsActivated = true;
                    Video1.LoadedBehavior = MediaState.Play;

                    if (BcTimer != null)
                    {
                        BcTimer.Start();
                    }
                }));
            });
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            switch (((Window)sender).WindowState)
            {
                case WindowState.Normal:
                case WindowState.Maximized:
                    Application_Activated(sender, e);
                    break;
                case WindowState.Minimized:
                    Application_Deactivated(sender, e);
                    break;
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
