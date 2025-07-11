﻿using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        protected override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginCoverImageDataContext ControlDataContext = new PluginCoverImageDataContext();
        protected override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginCoverImageDataContext)controlDataContext;
        }

        private System.Timers.Timer BcTimer { get; set; }
        private System.Timers.Timer BcTimerVideo { get; set; }
        private int Counter { get; set; } = 0;
        private GameBackgroundImages GameBackgroundImages { get; set; }

        private static readonly Random _random = new Random();

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
            if (BcTimerVideo != null)
            {
                BcTimerVideo.Stop();
                BcTimerVideo.Dispose();
                BcTimerVideo = null;
            }

            ControlDataContext = new PluginCoverImageDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.Settings.EnableCoverImage,
                UseAnimated = PluginDatabase.PluginSettings.Settings.EnableImageAnimatedCover,
                EnableRandomSelect = PluginDatabase.PluginSettings.Settings.EnableCoverImageRandomSelect,
                EnableRandomOnSelect = PluginDatabase.PluginSettings.Settings.EnableCoverImageRandomOnSelect,
                EnableRandomOnStart = PluginDatabase.PluginSettings.Settings.EnableCoverImageRandomOnStart,
                EnableAutoChanger = PluginDatabase.PluginSettings.Settings.EnableCoverImageAutoChanger,

                ImageSource = null,
                VideoSource = null
            };
        }


        public PluginCoverImage()
        {
            InitializeComponent();

            Delay = 0;
            DataContext = ControlDataContext;

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
            string winIdProperty = string.Empty;
            string winName = string.Empty;

            try
            {
                winIdProperty = ((Window)sender).GetValue(AutomationProperties.AutomationIdProperty).ToString();
                winName = ((Window)sender).Name;

                if (winIdProperty == "WindowSettings")
                {
                    GetCoverProperties();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on WindowBase_LoadedEvent for {winName} - {winIdProperty}", true, PluginDatabase.PluginName);
            }
        }


        private void GetCoverProperties()
        {
            DependencyObject thisParent = ((FrameworkElement)((FrameworkElement)((FrameworkElement)this.Parent).Parent).Parent).Parent;
            FrameworkElement PART_ImageCover = UI.SearchElementByName("PART_ImageCover", thisParent, false, false);

            if (PART_ImageCover != null)
            {
                PropertyInfo[] ImageCoverProperties = PART_ImageCover.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo[] backChangerImageProperties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                List<string> usedProperties = new List<string>
                {
                    "Stretch", "StretchDirection"
                };

                foreach (PropertyInfo propImageBackground in ImageCoverProperties)
                {
                    if (propImageBackground.CanWrite)
                    {
                        if (usedProperties.Contains(propImageBackground.Name))
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
                                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                            }
                        }
                    }
                }
            }
        }


        public override void SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            GameBackgroundImages = (GameBackgroundImages)PluginGameData;

            try
            {
                Video1.LoadedBehavior = MediaState.Stop;

                if (!GameBackgroundImages.HasDataCover)
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
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }


        public void SetCover()
        {
            string pathImage = string.Empty;

            if (GameBackgroundImages.HasDataCover)
            {
                ItemImage ItemFavorite = GameBackgroundImages.ItemsCover.FirstOrDefault(x => x.IsFavorite);

                if (ControlDataContext.EnableAutoChanger)
                {
                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            pathImage = ItemFavorite.FullPath;
                            Counter = GameBackgroundImages.ItemsCover.FindIndex(x => x.IsFavorite);
                        }
                        else
                        {
                            Counter = _random.Next(0, GameBackgroundImages.ItemsCover.Count);
                            pathImage = GameBackgroundImages.ItemsCover[Counter].FullPath;
                        }
                    }
                    else
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            pathImage = ItemFavorite.FullPath;
                            Counter = GameBackgroundImages.ItemsCover.FindIndex(x => x.IsFavorite);
                        }
                        else
                        {
                            pathImage = GameBackgroundImages.ItemsCover[Counter].FullPath;
                        }
                    }

                    SetCoverImage(pathImage);

                    BcTimer = new System.Timers.Timer(PluginDatabase.PluginSettings.Settings.CoverImageAutoChangerTimer * 1000)
                    {
                        AutoReset = true
                    };
                    BcTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                    BcTimer.Start();
                }
                else if (ControlDataContext.EnableRandomSelect)
                {
                    if (ControlDataContext.EnableRandomOnStart)
                    {
                        pathImage = GameBackgroundImages.CoverImageOnStart.FullPath;
                    }
                    else
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            pathImage = ItemFavorite.FullPath;
                        }
                        else
                        {
                            Random rnd = new Random();
                            int imgSelected = rnd.Next(0, GameBackgroundImages.ItemsCover.Count);
                            pathImage = GameBackgroundImages.ItemsCover[imgSelected].FullPath;
                        }
                    }

                    SetCoverImage(pathImage);
                }
                else
                {
                    if (ItemFavorite != null)
                    {
                        pathImage = ItemFavorite.FullPath;
                        SetCoverImage(pathImage);
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

            if (PluginDatabase.PluginSettings.Settings.useVideoDelayCoverImage)
            {
                BcTimerVideo = new System.Timers.Timer(PluginDatabase.PluginSettings.Settings.videoDelayCoverImage * 1000)
                {
                    AutoReset = true
                };
                BcTimerVideo.Elapsed += new ElapsedEventHandler(OnTimedVideoEvent);
                BcTimerVideo.Start();
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
                string pathImage = ImageSourceManager.GetImagePath(GameContext.CoverImage)
                    ?? API.Instance.Database.GetFullFilePath(GameContext.CoverImage);
                SetCoverImage(pathImage);
            }
        }

        public void SetCoverImage(string pathImage = null)
        {
            if (!File.Exists(pathImage))
            {
                ControlDataContext.ImageSource = null;
                ControlDataContext.VideoSource = null;
                return;
            }

            PluginDatabase.PluginSettings.Settings.CoverIsVideo = Path.GetExtension(pathImage).IsEqual(".mp4");

            if (Path.GetExtension(pathImage).IsEqual(".mp4"))
            {
                ControlDataContext.ImageSource = null;
                ControlDataContext.VideoSource = pathImage;

                Video1.LoadedBehavior = MediaState.Play;
            }
            else
            {
                ControlDataContext.ImageSource = pathImage;
                ControlDataContext.VideoSource = null;
            }

            _ = API.Instance.MainView.UIDispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
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

            if (Path.GetExtension(image).IsEqual(".mp4"))
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


        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (!WindowsIsActivated)
            {
                return;
            }

            try
            {
                _ = API.Instance.MainView.UIDispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() => 
                {
                    string pathImage = string.Empty;

                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (GameBackgroundImages.ItemsCover.Count != 0)
                        {
                            Random rnd = new Random();
                            int imgSelected = rnd.Next(0, (GameBackgroundImages.ItemsCover.Count));
                            while (imgSelected == Counter && GameBackgroundImages.ItemsCover.Count != 1)
                            {
                                imgSelected = rnd.Next(0, (GameBackgroundImages.ItemsCover.Count));
                            }
                            Counter = imgSelected;

                            pathImage = GameBackgroundImages.ItemsCover[imgSelected].FullPath;
                        }

                        SetCoverImage(pathImage);
                    }
                    else
                    {
                        Counter++;

                        if (GameBackgroundImages.ItemsCover.Count != 0)
                        {
                            if (Counter == GameBackgroundImages.ItemsCover.Count)
                            {
                                Counter = 0;
                            }

                            pathImage = GameBackgroundImages.ItemsCover[Counter].FullPath;
                        }

                        SetCoverImage(pathImage);
                    }
                }));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void OnTimedVideoEvent(object source, ElapsedEventArgs e)
        {
            if (!WindowsIsActivated)
            {
                return;
            }

            try
            {
                _ = API.Instance.MainView.UIDispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    string pathVideo = GameBackgroundImages?.ItemsCover?.Where(x => x.IsVideo && x.Exist)?.OrderBy(x => x.IsFavorite)?.FirstOrDefault()?.FullPath;
                    if (!pathVideo.IsNullOrEmpty())
                    {
                        SetCoverImage(pathVideo);
                    }
                }));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
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
        public bool EnableRandomOnSelect { get; set; }
        public bool EnableRandomOnStart { get; set; }
        public bool EnableAutoChanger { get; set; }

        public string ImageSource { get; set; }
        public string VideoSource { get; set; }
    }
}