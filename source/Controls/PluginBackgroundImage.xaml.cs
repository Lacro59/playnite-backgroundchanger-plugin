﻿using BackgroundChanger.Models;
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
using System.ComponentModel;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginBackgroundImage.xaml
    /// </summary>
    public partial class PluginBackgroundImage : PluginUserControlExtend
    {
        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;
        internal override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginBackgroundImageDataContext ControlDataContext = new PluginBackgroundImageDataContext();
        internal override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginBackgroundImageDataContext)controlDataContext;
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

            ControlDataContext = new PluginBackgroundImageDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.Settings.EnableBackgroundImage,
                UseAnimated = PluginDatabase.PluginSettings.Settings.EnableImageAnimatedBackground,
                EnableRandomSelect = PluginDatabase.PluginSettings.Settings.EnableBackgroundImageRandomSelect,
                EnableAutoChanger = PluginDatabase.PluginSettings.Settings.EnableBackgroundImageAutoChanger
            };
        }


        public PluginBackgroundImage()
        {
            InitializeComponent();
            this.DataContext = ControlDataContext;

            Image1FadeIn = (Storyboard)TryFindResource("Image1FadeIn");
            Image2FadeIn = (Storyboard)TryFindResource("Image2FadeIn");
            Image1FadeOut = (Storyboard)TryFindResource("Image1FadeOut");
            Image2FadeOut = (Storyboard)TryFindResource("Image2FadeOut");
            BorderDarkenFadeOut = (Storyboard)TryFindResource("BorderDarkenFadeOut");
            Image1FadeOut.Completed += Image1FadeOut_Completed;
            Image2FadeOut.Completed += Image2FadeOut_Completed;
            BorderDarkenFadeOut.Completed += BorderDarkenOut_Completed;


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
                    GetFadeImageProperties();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on WindowBase_LoadedEvent for {WinName} - {WinIdProperty}", true, PluginDatabase.PluginName);
            }
        }

        private void GetFadeImageProperties()
        {
            if (!PluginDatabase.PluginSettings.Settings.BackgroundImageSameSettings)
            {
                return;
            }

            FrameworkElement PART_ImageBackground_4 = UI.SearchElementByName("ControlRoot", false, false, 4);
            FrameworkElement PART_ImageBackground_3 = UI.SearchElementByName("ControlRoot", false, false, 3);
            FrameworkElement PART_ImageBackground_2 = UI.SearchElementByName("ControlRoot", false, false, 2);

            FrameworkElement PART_ImageBackground = PART_ImageBackground_4 ?? PART_ImageBackground_3 ?? PART_ImageBackground_2 ?? null;

            if (PART_ImageBackground != null)
            {
                PropertyInfo[] ImageBackgroundProperties = PART_ImageBackground.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo[] backChangerImageProperties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                List<string> UsedProperties = new List<string>
                {
                    "AnimationEnabled", "ImageOpacityMask", "ImageDarkeningBrush", "Stretch", "StretchDirection",
                    "IsBlurEnabled", "BlurAmount", "HighQualityBlur", "OpacityMask"
                };

                foreach (PropertyInfo propImageBackground in ImageBackgroundProperties)
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
                                    object value = propImageBackground.GetValue(PART_ImageBackground, null);
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
            gameBackgroundImages = (GameBackgroundImages)PluginGameData;

            Video1.Volume = PluginDatabase.PluginSettings.Settings.Volume / 10;
            Video2.Volume = PluginDatabase.PluginSettings.Settings.Volume / 10;

            try
            {
                Video1.LoadedBehavior = MediaState.Stop;
                Video2.LoadedBehavior = MediaState.Stop;

                if (!gameBackgroundImages.HasDataBackground)
                {
                    MustDisplay = false;
                    this.DataContext = ControlDataContext;
                    return;
                }

                IsFirst = true;
                SetBackground();
                IsFirst = false;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }


        public void SetBackground()
        {
            string PathImage = string.Empty;

            if (gameBackgroundImages.HasDataBackground && !PluginDatabase.PluginSettings.Settings.useVideoDelayBackgroundImage)
            {
                ItemImage ItemFavorite = gameBackgroundImages.ItemsBackground.Where(x => x.IsFavorite).FirstOrDefault();

                if (ControlDataContext.EnableAutoChanger)
                {
                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            PathImage = ItemFavorite.FullPath;
                            Counter = gameBackgroundImages.ItemsBackground.FindIndex(x => x.IsFavorite);
                        }
                        else
                        {
                            Random rnd = new Random();
                            Counter = rnd.Next(0, gameBackgroundImages.ItemsBackground.Count);
                            PathImage = gameBackgroundImages.ItemsBackground[Counter].FullPath;
                        }
                    }
                    else
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            PathImage = ItemFavorite.FullPath;
                            Counter = gameBackgroundImages.ItemsBackground.FindIndex(x => x.IsFavorite);
                        }
                        else
                        {
                            PathImage = gameBackgroundImages.ItemsBackground[Counter].FullPath;
                        }
                    }

                    SetBackgroundImage(PathImage);

                    BcTimer = new System.Timers.Timer(PluginDatabase.PluginSettings.Settings.BackgroundImageAutoChangerTimer * 1000);
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
                        int ImgSelected = rnd.Next(0, gameBackgroundImages.ItemsBackground.Count);
                        PathImage = gameBackgroundImages.ItemsBackground[ImgSelected].FullPath;
                    }

                    SetBackgroundImage(PathImage);
                }
                else
                {
                    if (ItemFavorite != null)
                    {
                        PathImage = ItemFavorite.FullPath;
                        SetBackgroundImage(PathImage);
                    }
                    else
                    {
                        SetDefaultBackgroundImage();
                    }
                }
            }
            else
            {
                SetDefaultBackgroundImage();
            }
        }

        public void SetDefaultBackgroundImage()
        {
            if (GameContext.BackgroundImage.IsNullOrEmpty())
            {
                SetBackgroundImage();
            }
            else
            {
                string PathImage = ImageSourceManager.GetImagePath(GameContext.BackgroundImage);
                if (PathImage.IsNullOrEmpty())
                {
                    PathImage = API.Instance.Database.GetFullFilePath(GameContext.BackgroundImage);
                }

                SetBackgroundImage(PathImage);
            }

            if (PluginDatabase.PluginSettings.Settings.useVideoDelayBackgroundImage)
            {
                _ = Task.Run(() =>
                {
                    Thread.Sleep(1000 * PluginDatabase.PluginSettings.Settings.videoDelayBackgroundImage);
                    _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        string PathImage = gameBackgroundImages?.ItemsBackground?.Where(x => x.IsVideo)?.OrderBy(x => x.IsFavorite)?.FirstOrDefault()?.FullPath;
                        SetBackgroundImage(PathImage);
                    }));
                });
            }
        }

        public void SetBackgroundImage(string PathImage = null)
        {
            _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Render, (Action)delegate
            {
                if (!File.Exists(PathImage))
                {
                    PathImage = null;
                }
                this.Source = PathImage;
            });
        }


        private enum CurrentImage
        {
            Image1,
            Image2,
            None
        }

        private CurrentImage currentImage = CurrentImage.None;
        private object currentSource = null;

        internal Storyboard Image1FadeIn;
        internal Storyboard Image2FadeIn;
        internal Storyboard Image1FadeOut;
        internal Storyboard Image2FadeOut;
        internal Storyboard BorderDarkenFadeOut;

        #region AnimationEnabled
        public static readonly DependencyProperty AnimationEnabledProperty = DependencyProperty.Register(
            nameof(AnimationEnabled),
            typeof(bool),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(true));

        public bool AnimationEnabled
        {
            get => (bool)GetValue(AnimationEnabledProperty);
            set => SetValue(AnimationEnabledProperty, value);
        }
        #endregion

        #region Source
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(null, SourceChanged));

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }
        #endregion

        #region ImageOpacityMask
        public static readonly DependencyProperty ImageOpacityMaskProperty = DependencyProperty.Register(
            nameof(ImageOpacityMask),
            typeof(Brush),
            typeof(PluginBackgroundImage),
            new PropertyMetadata());

        public Brush ImageOpacityMask
        {
            get => (Brush)GetValue(ImageOpacityMaskProperty);
            set => SetValue(ImageOpacityMaskProperty, value);
        }
        #endregion

        #region ImageDarkeningBrush
        public static readonly DependencyProperty ImageDarkeningBrushProperty = DependencyProperty.Register(
            nameof(ImageDarkeningBrush),
            typeof(Brush),
            typeof(PluginBackgroundImage),
            new PropertyMetadata());

        public Brush ImageDarkeningBrush
        {
            get => (Brush)GetValue(ImageDarkeningBrushProperty);
            set => SetValue(ImageDarkeningBrushProperty, value);
        }
        #endregion

        #region Stretch
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch),
            typeof(Stretch),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(Stretch.UniformToFill));

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }
        #endregion

        #region StretchDirection
        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(default));

        public StretchDirection StretchDirection
        {
            get => (StretchDirection)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }
        #endregion

        #region IsBlurEnabled
        public static readonly DependencyProperty IsBlurEnabledProperty = DependencyProperty.Register(
            nameof(IsBlurEnabled),
            typeof(bool),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(false, BlurSettingChanged));

        public bool IsBlurEnabled
        {
            get => (bool)GetValue(IsBlurEnabledProperty);
            set => SetValue(IsBlurEnabledProperty, value);
        }
        #endregion

        #region BlurAmount
        public static readonly DependencyProperty BlurAmountProperty = DependencyProperty.Register(
            nameof(BlurAmount),
            typeof(int),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(10, BlurSettingChanged));

        public int BlurAmount
        {
            get => (int)GetValue(BlurAmountProperty);
            set => SetValue(BlurAmountProperty, value);
        }
        #endregion

        #region HighQualityBlur
        public static readonly DependencyProperty HighQualityBlurProperty = DependencyProperty.Register(
            nameof(HighQualityBlurProperty),
            typeof(bool),
            typeof(PluginBackgroundImage),
            new PropertyMetadata(false, BlurSettingChanged));

        public bool HighQualityBlur
        {
            get => (bool)GetValue(HighQualityBlurProperty);
            set => SetValue(HighQualityBlurProperty, value);
        }
        #endregion

        private void Image1FadeOut_Completed(object sender, EventArgs e)
        {
            AnimatedImage1.Source = null;
            AnimatedImage1.UpdateLayout();
            Video1.Source = null;
            Video1.UpdateLayout();
            GC.Collect();
        }

        private void Image2FadeOut_Completed(object sender, EventArgs e)
        {
            AnimatedImage2.Source = null;
            AnimatedImage2.UpdateLayout();
            Video2.Source = null;
            Video2.UpdateLayout();
        }

        private void BorderDarkenOut_Completed(object sender, EventArgs e)
        {
            BorderDarken.Opacity = 0;
        }

        private static void BlurSettingChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            PluginBackgroundImage control = (PluginBackgroundImage)obj;
            if (control.Source == null)
            {
                return;
            }

            int blurAmount = control.BlurAmount;
            bool blurEnabled = control.IsBlurEnabled;
            bool highQuality = control.HighQualityBlur;
            control.ImageHolder.Effect = blurEnabled
                ? new BlurEffect()
                {
                    KernelType = KernelType.Gaussian,
                    Radius = blurAmount,
                    RenderingBias = highQuality ? RenderingBias.Quality : RenderingBias.Performance
                }
                : null;
        }

        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            PluginBackgroundImage control = (PluginBackgroundImage)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private async void LoadNewSource(object newSource, object oldSource)
        {
            try
            {
                int blurAmount = BlurAmount;
                bool blurEnabled = IsBlurEnabled;
                bool highQuality = HighQualityBlur;

                string image = null;

                if (newSource?.Equals(currentSource) == true)
                {
                    if (Video1.Source != null)
                    {
                        Video1.LoadedBehavior = MediaState.Play;
                    }
                    if (Video2.Source != null)
                    {
                        Video2.LoadedBehavior = MediaState.Play;
                    }

                    return;
                }

                currentSource = newSource;

                if (newSource is string)
                {
                    if (!File.Exists(newSource.ToString()))
                    {
                        Logger.Warn($"File not founs {newSource}");
                    }
                    else
                    {
                        image = (string)currentSource;
                    }
                }

                if (blurEnabled)
                {
                    if (ImageHolder.Effect == null)
                    {
                        ImageHolder.Effect = new BlurEffect()
                        {
                            KernelType = KernelType.Gaussian,
                            Radius = blurAmount,
                            RenderingBias = highQuality ? RenderingBias.Quality : RenderingBias.Performance
                        };
                    }
                }
                else
                {
                    if (ImageHolder.Effect != null)
                    {
                        ImageHolder.Effect = null;
                    }
                }

                if (AnimationEnabled)
                {
                    if (image == null)
                    {
                        if (currentImage == CurrentImage.None)
                        {
                            return;
                        }

                        if (currentImage == CurrentImage.Image1)
                        {
                            Image1FadeOut.Begin();
                            BorderDarkenFadeOut.Begin();
                        }
                        else if (currentImage == CurrentImage.Image2)
                        {
                            Image2FadeOut.Begin();
                            BorderDarkenFadeOut.Begin();
                        }

                        currentImage = CurrentImage.None;
                    }
                    else
                    {
                        if (currentImage == CurrentImage.None)
                        {
                            Image1FadeOut.Stop();

                            if (Path.GetExtension(image).ToLower().Contains("mp4"))
                            {
                                //AnimatedImage1.Source = null;
                                //AnimatedImage2.Source = null;
                                Video1.Source = new Uri(image);
                                //Video2.Source = null;

                                Video1.LoadedBehavior = MediaState.Play;
                            }
                            else
                            {
                                //Video1.Source = null;
                                //Video2.Source = null;
                                AnimatedImage1.Source = image;
                                //AnimatedImage2.Source = null;
                            }

                            Image1FadeIn.Begin();
                            BorderDarken.Opacity = 1;
                            BorderDarkenFadeOut.Stop();
                            currentImage = CurrentImage.Image1;
                        }
                        else if (currentImage == CurrentImage.Image1)
                        {
                            Image2FadeOut.Stop();

                            if (Path.GetExtension(image).ToLower().Contains("mp4"))
                            {
                                //AnimatedImage1.Source = null;
                                //AnimatedImage2.Source = null;
                                //Video1.Source = null;
                                Video2.Source = new Uri(image);

                                Video2.LoadedBehavior = MediaState.Play;
                            }
                            else
                            {
                                //Video1.Source = null;
                                //Video2.Source = null;
                                //AnimatedImage1.Source = null;
                                AnimatedImage2.Source = image;
                            }

                            Image2FadeIn.Begin();
                            Image1FadeOut.Begin();
                            BorderDarken.Opacity = 1;
                            BorderDarkenFadeOut.Stop();
                            currentImage = CurrentImage.Image2;
                        }
                        else if (currentImage == CurrentImage.Image2)
                        {
                            Image1FadeOut.Stop();

                            if (Path.GetExtension(image).ToLower().Contains("mp4"))
                            {
                                //AnimatedImage1.Source = null;
                                //AnimatedImage2.Source = null;
                                Video1.Source = new Uri(image);
                                //Video2.Source = null;

                                Video1.LoadedBehavior = MediaState.Play;
                            }
                            else
                            {
                                //Video1.Source = null;
                                //Video2.Source = null;
                                AnimatedImage1.Source = image;
                                //AnimatedImage2.Source = null;
                            }

                            Image1FadeIn.Begin();
                            Image2FadeOut.Begin();
                            BorderDarken.Opacity = 1;
                            BorderDarkenFadeOut.Stop();
                            currentImage = CurrentImage.Image1;
                        }
                    }
                }
                else
                {
                    if (currentImage == CurrentImage.Image1)
                    {
                        if (image != null && Path.GetExtension(image).ToLower().Contains("mp4"))
                        {
                            AnimatedImage1.Source = null;
                            AnimatedImage2.Source = null;
                            Video1.Source = new Uri(image);
                            Video2.Source = null;

                            Video1.LoadedBehavior = MediaState.Play;
                        }
                        else
                        {
                            Video1.Source = null;
                            Video2.Source = null;
                            AnimatedImage1.Source = image;
                            AnimatedImage2.Source = null;
                        }
                    }
                    else if (currentImage == CurrentImage.Image2)
                    {
                        if (image != null && Path.GetExtension(image).ToLower().Contains("mp4"))
                        {
                            AnimatedImage1.Source = null;
                            AnimatedImage2.Source = null;
                            Video1.Source = null;
                            Video2.Source = new Uri(image);

                            Video2.LoadedBehavior = MediaState.Play;
                        }
                        else
                        {
                            Video1.Source = null;
                            Video2.Source = null;
                            AnimatedImage1.Source = null;
                            AnimatedImage2.Source = image;
                        }
                    }
                    else
                    {
                        if (image != null && Path.GetExtension(image).ToLower().Contains("mp4"))
                        {
                            AnimatedImage1.Source = null;
                            AnimatedImage2.Source = null;
                            Video1.Source = new Uri(image);
                            Video2.Source = null;

                            Video1.LoadedBehavior = MediaState.Play;
                        }
                        else
                        {
                            Video1.Source = null;
                            Video2.Source = null;
                            AnimatedImage1.Source = image;
                            AnimatedImage2.Source = null;
                        }

                        currentImage = CurrentImage.Image1;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, pluginDatabase.PluginName);
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
                _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    string PathImage = string.Empty;

                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (gameBackgroundImages.ItemsBackground.Count != 0)
                        {
                            Random rnd = new Random();
                            int ImgSelected = rnd.Next(0, gameBackgroundImages.ItemsBackground.Count);
                            while (ImgSelected == Counter && gameBackgroundImages.ItemsBackground.Count != 1)
                            {
                                ImgSelected = rnd.Next(0, gameBackgroundImages.ItemsBackground.Count);
                            }
                            Counter = ImgSelected;

                            PathImage = gameBackgroundImages.ItemsBackground[ImgSelected].FullPath;
                        }

                        SetBackgroundImage(PathImage);
                    }
                    else
                    {
                        Counter++;

                        if (gameBackgroundImages.ItemsBackground.Count != 0)
                        {
                            if (Counter == gameBackgroundImages.ItemsBackground.Count)
                            {
                                Counter = 0;
                            }

                            PathImage = gameBackgroundImages.ItemsBackground[Counter].FullPath;
                        }

                        SetBackgroundImage(PathImage);
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
            GetFadeImageProperties();

            // Activate/Deactivated animation
            Application.Current.Activated += Application_Activated;
            Application.Current.Deactivated += Application_Deactivated;
            Application.Current.MainWindow.StateChanged += MainWindow_StateChanged;
        }


        #region Activate/Deactivated animation
        private void Application_Deactivated(object sender, EventArgs e)
        {
            _ = Task.Run(() =>
            {
                Thread.Sleep(1000);
                _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    WindowsIsActivated = false;
                    Video1.LoadedBehavior = MediaState.Pause;
                    Video2.LoadedBehavior = MediaState.Pause;

                    if (BcTimer != null)
                    {
                        BcTimer.Stop();
                    }
                }));
            });
        }

        private void Application_Activated(object sender, EventArgs e)
        {
            _ = Task.Run(() =>
            {
                Thread.Sleep(1000);
                _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    WindowsIsActivated = true;
                    Video1.LoadedBehavior = MediaState.Play;
                    Video2.LoadedBehavior = MediaState.Play;

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
                default:
                    break;
            }
        }
        #endregion
    }


    public class PluginBackgroundImageDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool UseAnimated { get; set; }
        public bool EnableRandomSelect { get; set; }
        public bool EnableAutoChanger { get; set; }
    }
}
