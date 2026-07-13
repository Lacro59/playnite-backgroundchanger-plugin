using BackgroundChanger.Models;
using BackgroundChangerPlugin.Controls;
using BackgroundChanger.Services;
using CommonPlayniteShared;
using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Interfaces;
using CommonPluginsShared.UI;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public partial class PluginBackgroundImage : PluginMediaLifecycleControlBase
    {
        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;
        protected override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginBackgroundImageDataContext ControlDataContext = new PluginBackgroundImageDataContext();
        protected override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginBackgroundImageDataContext)controlDataContext;
        }

        private System.Timers.Timer BcTimer { get; set; }
        private System.Timers.Timer BcTimerVideo { get; set; }
        private int Counter { get; set; } = 0;
        private GameBackgroundImages GameBackgroundImages { get; set; }

        private static readonly Random random = new Random();

        private bool IsFirst { get; set; } = true;

        protected override void AttachStaticEvents()
        {
            base.AttachStaticEvents();

            // Attach once per plugin to avoid subscribing multiple times across theme control instances.
            if (PluginDatabase == null || PluginDatabase.PluginSettings == null)
            {
                return;
            }

            AttachPluginEvents(PluginDatabase.PluginName, () =>
            {
                PluginDatabase.PluginSettings.PropertyChanged += CreatePluginSettingsHandler();
                PluginDatabase.DatabaseItemUpdated += CreateDatabaseItemUpdatedHandler<GameBackgroundImages>();
                PluginDatabase.DatabaseItemCollectionChanged += CreateDatabaseCollectionChangedHandler<GameBackgroundImages>();
            });
        }


        public override void SetDefaultDataContext()
        {
            DisposeBcTimers();

            ControlDataContext = new PluginBackgroundImageDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.EnableBackgroundImage,
                EnableRandomSelect = PluginDatabase.PluginSettings.EnableBackgroundImageRandomSelect,
                EnableRandomOnSelect = PluginDatabase.PluginSettings.EnableBackgroundImageRandomOnSelect,
                EnableRandomOnStart = PluginDatabase.PluginSettings.EnableBackgroundImageRandomOnStart,
                EnableAutoChanger = PluginDatabase.PluginSettings.EnableBackgroundImageAutoChanger
            };
        }


        public PluginBackgroundImage()
        {
            InitializeComponent();

            Delay = 0;
            DataContext = ControlDataContext;

            Image1FadeIn = (Storyboard)TryFindResource("Image1FadeIn");
            Image2FadeIn = (Storyboard)TryFindResource("Image2FadeIn");
            Image1FadeOut = (Storyboard)TryFindResource("Image1FadeOut");
            Image2FadeOut = (Storyboard)TryFindResource("Image2FadeOut");
            BorderDarkenFadeOut = (Storyboard)TryFindResource("BorderDarkenFadeOut");
            Image1FadeOut.Completed += Image1FadeOut_Completed;
            Image2FadeOut.Completed += Image2FadeOut_Completed;
            BorderDarkenFadeOut.Completed += BorderDarkenOut_Completed;

            Loaded += OnLoaded;
            InitializeMediaLifecycleHooks();

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
                    GetFadeImageProperties();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on WindowBase_LoadedEvent for {winName} - {winIdProperty}", true, PluginDatabase.PluginName);
            }
        }

        private void GetFadeImageProperties()
        {
            if (!PluginDatabase.PluginSettings.BackgroundImageSameSettings)
            {
                return;
            }

            FrameworkElement PART_ImageBackground_4 = null;
            try
            {
                PART_ImageBackground_4 = UIHelper.SearchElementByName("ControlRoot", false, false, 4);
            }
            catch
            {
            }

            FrameworkElement PART_ImageBackground_3 = null;
            try
            {
                PART_ImageBackground_3 = UIHelper.SearchElementByName("ControlRoot", false, false, 3);
            }
            catch
            {
            }

            FrameworkElement PART_ImageBackground_2 = null;
            try
            {
                PART_ImageBackground_2 = UIHelper.SearchElementByName("ControlRoot", false, false, 2);
            }
            catch
            {
            }

            FrameworkElement PART_ImageBackground = PART_ImageBackground_4 ?? PART_ImageBackground_3 ?? PART_ImageBackground_2 ?? null;

            if (PART_ImageBackground != null)
            {
                PropertyInfo[] ImageBackgroundProperties = PART_ImageBackground.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo[] backChangerImageProperties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

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


        public override void SetData(Game newContext, PluginGameEntry PluginGameData)
        {
            GameBackgroundImages = (GameBackgroundImages)PluginGameData;

            Video1.Volume = PluginDatabase.PluginSettings.Volume / 10;
            Video2.Volume = PluginDatabase.PluginSettings.Volume / 10;

            try
            {
                Video1.LoadedBehavior = MediaState.Stop;
                Video2.LoadedBehavior = MediaState.Stop;

                if (!GameBackgroundImages.HasDataBackground)
                {
                    DisposeBcTimers();
                    PauseVideos();
                    MustDisplay = false;
                    DataContext = ControlDataContext;
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
            string pathImage = string.Empty;

            if (GameBackgroundImages.HasDataBackground)
            {
                ItemImage ItemFavorite = GameBackgroundImages.ItemsBackground.FirstOrDefault(x => x.IsFavorite);

                if (ControlDataContext.EnableAutoChanger)
                {
                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            pathImage = ItemFavorite.FullPath;
                            Counter = GameBackgroundImages.ItemsBackground.FindIndex(x => x.IsFavorite);
                        }
                        else
                        {
                            Counter = random.Next(0, GameBackgroundImages.ItemsBackground.Count);
                            pathImage = GameBackgroundImages.ItemsBackground[Counter].FullPath;
                        }
                    }
                    else
                    {
                        if (IsFirst && ItemFavorite != null)
                        {
                            pathImage = ItemFavorite.FullPath;
                            Counter = GameBackgroundImages.ItemsBackground.FindIndex(x => x.IsFavorite);
                        }
                        else
                        {
                            pathImage = GameBackgroundImages.ItemsBackground[Counter].FullPath;
                        }
                    }

                    SetBackgroundImage(pathImage);

                    BcTimer = new System.Timers.Timer(PluginDatabase.PluginSettings.BackgroundImageAutoChangerTimer * 1000)
                    {
                        AutoReset = true
                    };
                    BcTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                    if (IsMediaLifecycleActive())
                    {
                        BcTimer.Start();
                    }
                }
                else if (ControlDataContext.EnableRandomSelect)
                {
                    if (ControlDataContext.EnableRandomOnStart)
                    {
                        pathImage = GameBackgroundImages.BackgroundImageOnStart.FullPath;
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
                            int imgSelected = rnd.Next(0, GameBackgroundImages.ItemsBackground.Count);
                            pathImage = GameBackgroundImages.ItemsBackground[imgSelected].FullPath;
                        }
                    }

                    SetBackgroundImage(pathImage);
                }
                else
                {
                    if (ItemFavorite != null)
                    {
                        pathImage = ItemFavorite.FullPath;
                        SetBackgroundImage(pathImage);
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

            if (PluginDatabase.PluginSettings.useVideoDelayBackgroundImage)
            {
                BcTimerVideo = new System.Timers.Timer(PluginDatabase.PluginSettings.videoDelayBackgroundImage * 1000)
                {
                    AutoReset = true
                };
                BcTimerVideo.Elapsed += new ElapsedEventHandler(OnTimedVideoEvent);
                if (IsMediaLifecycleActive())
                {
                    BcTimerVideo.Start();
                }
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
                string pathImage = ImageSourceManager.GetImagePath(GameContext.BackgroundImage)
                    ?? API.Instance.Database.GetFullFilePath(GameContext.BackgroundImage);
                SetBackgroundImage(pathImage);
            }
        }

        public void SetBackgroundImage(string pathImage = null)
        {
            _ = API.Instance.MainView.UIDispatcher?.BeginInvoke(DispatcherPriority.Render, (Action)delegate
            {
                if (!File.Exists(pathImage))
                {
                    pathImage = null;
                }
                Source = pathImage;
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
            Image1.Source = null;
            Image1.UpdateLayout();
            Video1.Source = null;
            Video1.UpdateLayout();
        }

        private void Image2FadeOut_Completed(object sender, EventArgs e)
        {
            Image2.Source = null;
            Image2.UpdateLayout();
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

            control.ApplyAdaptiveBlurEffect();
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
                string image = null;

                if (newSource?.Equals(currentSource) == true)
                {
                    if (Video1.Source != null)
                    {
                        Video1.LoadedBehavior = VideoStateForLifecycle();
                    }
                    if (Video2.Source != null)
                    {
                        Video2.LoadedBehavior = VideoStateForLifecycle();
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

                ApplyAdaptiveBlurEffect();

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
                        bool isVideo = Path.GetExtension(image).IsEqual(".mp4");
                        if (PluginDatabase.PluginSettings.BackgroundIsVideo != isVideo)
                        {
                            PluginDatabase.PluginSettings.BackgroundIsVideo = isVideo;
                        }

                        if (currentImage == CurrentImage.None)
                        {
                            Image1FadeOut.Stop();

                            if (Path.GetExtension(image).IsEqual(".mp4"))
                            {
                                //Image1.Source = null;
                                //Image2.Source = null;
                                Video1.Source = new Uri(image);
                                //Video2.Source = null;

                                Video1.LoadedBehavior = VideoStateForLifecycle();
                            }
                            else
                            {
                                //Video1.Source = null;
                                //Video2.Source = null;
                                Image1.Source = image;
                                //Image2.Source = null;
                            }

                            Image1FadeIn.Begin();
                            BorderDarken.Opacity = 1;
                            BorderDarkenFadeOut.Stop();
                            currentImage = CurrentImage.Image1;
                        }
                        else if (currentImage == CurrentImage.Image1)
                        {
                            Image2FadeOut.Stop();

                            if (Path.GetExtension(image).IsEqual(".mp4"))
                            {
                                //Image1.Source = null;
                                //Image2.Source = null;
                                //Video1.Source = null;
                                Video2.Source = new Uri(image);

                                Video2.LoadedBehavior = VideoStateForLifecycle();
                            }
                            else
                            {
                                //Video1.Source = null;
                                //Video2.Source = null;
                                //Image1.Source = null;
                                Image2.Source = image;
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

                            if (Path.GetExtension(image).IsEqual(".mp4"))
                            {
                                //Image1.Source = null;
                                //Image2.Source = null;
                                Video1.Source = new Uri(image);
                                //Video2.Source = null;

                                Video1.LoadedBehavior = VideoStateForLifecycle();
                            }
                            else
                            {
                                //Video1.Source = null;
                                //Video2.Source = null;
                                Image1.Source = image;
                                //Image2.Source = null;
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
                        if (image != null && Path.GetExtension(image).IsEqual(".mp4"))
                        {
                            Image1.Source = null;
                            Image2.Source = null;
                            Video1.Source = new Uri(image);
                            Video2.Source = null;

                            Video1.LoadedBehavior = VideoStateForLifecycle();
                        }
                        else
                        {
                            Video1.Source = null;
                            Video2.Source = null;
                            Image1.Source = image;
                            Image2.Source = null;
                        }
                    }
                    else if (currentImage == CurrentImage.Image2)
                    {
                        if (image != null && Path.GetExtension(image).IsEqual(".mp4"))
                        {
                            Image1.Source = null;
                            Image2.Source = null;
                            Video1.Source = null;
                            Video2.Source = new Uri(image);

                            Video2.LoadedBehavior = VideoStateForLifecycle();
                        }
                        else
                        {
                            Video1.Source = null;
                            Video2.Source = null;
                            Image1.Source = null;
                            Image2.Source = image;
                        }
                    }
                    else
                    {
                        if (image != null && Path.GetExtension(image).IsEqual(".mp4"))
                        {
                            Image1.Source = null;
                            Image2.Source = null;
                            Video1.Source = new Uri(image);
                            Video2.Source = null;

                            Video1.LoadedBehavior = VideoStateForLifecycle();
                        }
                        else
                        {
                            Video1.Source = null;
                            Video2.Source = null;
                            Image1.Source = image;
                            Image2.Source = null;
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
            try
            {
                InvokeOnUiIfLifecycleActive(() =>
                {
                    string pathImage = string.Empty;

                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (GameBackgroundImages.ItemsBackground.Count != 0)
                        {
                            Random rnd = new Random();
                            int imgSelected = rnd.Next(0, GameBackgroundImages.ItemsBackground.Count);
                            while (imgSelected == Counter && GameBackgroundImages.ItemsBackground.Count != 1)
                            {
                                imgSelected = rnd.Next(0, GameBackgroundImages.ItemsBackground.Count);
                            }
                            Counter = imgSelected;

                            pathImage = GameBackgroundImages.ItemsBackground[imgSelected].FullPath;
                        }

                        SetBackgroundImage(pathImage);
                    }
                    else
                    {
                        Counter++;

                        if (GameBackgroundImages.ItemsBackground.Count != 0)
                        {
                            if (Counter == GameBackgroundImages.ItemsBackground.Count)
                            {
                                Counter = 0;
                            }

                            pathImage = GameBackgroundImages.ItemsBackground[Counter].FullPath;
                        }

                        SetBackgroundImage(pathImage);
                    }
                });
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void OnTimedVideoEvent(object source, ElapsedEventArgs e)
        {
            try
            {
                InvokeOnUiIfLifecycleActive(() =>
                {
                    string pathVideo = GameBackgroundImages?.ItemsBackground?.Where(x => x.IsVideo && x.Exist)?.OrderBy(x => x.IsFavorite)?.FirstOrDefault()?.FullPath;
                    if (!pathVideo.IsNullOrEmpty())
                    {
                        SetBackgroundImage(pathVideo);
                    }
                });
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

            AttachApplicationFocusEvents();
        }


        #region Media lifecycle

        /// <summary>
        /// Whether auto-change timers and video playback are allowed for this control instance.
        /// </summary>
        private bool IsMediaLifecycleActive()
        {
            return IsLifecycleDisplayActive();
        }

        private MediaState VideoStateForLifecycle()
        {
            return IsMediaLifecycleActive() ? MediaState.Play : MediaState.Pause;
        }

        /// <summary>
        /// Applies blur with an adaptive quality profile.
        /// Auto-changer prefers a balanced quality/performance profile and falls back to performance.
        /// </summary>
        private void ApplyAdaptiveBlurEffect()
        {
            if (!IsBlurEnabled)
            {
                if (ImageHolder.Effect != null)
                {
                    ImageHolder.Effect = null;
                }
                return;
            }

            bool isAutoChangerEnabled = ControlDataContext != null && ControlDataContext.EnableAutoChanger;
            bool hasVideoSource = (Video1?.Source != null) || (Video2?.Source != null);
            int blurRadius = Math.Max(0, BlurAmount);
            RenderingBias renderingBias = ResolveAdaptiveBlurBias(isAutoChangerEnabled, hasVideoSource, blurRadius);

            try
            {
                ImageHolder.Effect = new BlurEffect
                {
                    KernelType = KernelType.Gaussian,
                    Radius = blurRadius,
                    RenderingBias = renderingBias
                };

                LogControlTrace(
                    "Adaptive blur applied",
                    string.Format(
                        "autoChanger={0}, hasVideo={1}, radius={2}, bias={3}",
                        isAutoChangerEnabled,
                        hasVideoSource,
                        blurRadius,
                        renderingBias));
            }
            catch (Exception ex)
            {
                // Runtime fallback: if quality profile fails, force performance profile.
                Common.LogError(ex, true, "Adaptive blur fallback to performance mode", true, PluginDatabase.PluginName);
                ImageHolder.Effect = new BlurEffect
                {
                    KernelType = KernelType.Gaussian,
                    Radius = blurRadius,
                    RenderingBias = RenderingBias.Performance
                };
            }
        }

        private RenderingBias ResolveAdaptiveBlurBias(bool isAutoChangerEnabled, bool hasVideoSource, int blurRadius)
        {
            if (!isAutoChangerEnabled)
            {
                return HighQualityBlur ? RenderingBias.Quality : RenderingBias.Performance;
            }

            const int intermediateRadiusThreshold = 12;
            bool canUseIntermediateQuality = !hasVideoSource
                && blurRadius <= intermediateRadiusThreshold
                && !AnimationEnabled;

            return canUseIntermediateQuality
                ? RenderingBias.Quality
                : RenderingBias.Performance;
        }

        private void DisposeBcTimers()
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
        }

        private void StopBcTimers()
        {
            BcTimer?.Stop();
            BcTimerVideo?.Stop();
        }

        private void StartBcTimersIfConfigured()
        {
            if (!IsMediaLifecycleActive())
            {
                return;
            }

            if (ControlDataContext.EnableAutoChanger && BcTimer != null)
            {
                BcTimer.Start();
            }

            if (PluginDatabase.PluginSettings.useVideoDelayBackgroundImage && BcTimerVideo != null)
            {
                BcTimerVideo.Start();
            }
        }

        private void PauseVideos()
        {
            Video1.LoadedBehavior = MediaState.Pause;
            Video2.LoadedBehavior = MediaState.Pause;
        }

        private void ResumeVideosIfLifecycleActive()
        {
            if (!IsMediaLifecycleActive())
            {
                return;
            }

            if (Video1.Source != null)
            {
                Video1.LoadedBehavior = MediaState.Play;
            }

            if (Video2.Source != null)
            {
                Video2.LoadedBehavior = MediaState.Play;
            }
        }

        private void PauseMediaActivity()
        {
            StopBcTimers();
            PauseVideos();
        }

        private void ResumeMediaActivityIfAllowed()
        {
            if (!IsMediaLifecycleActive())
            {
                return;
            }

            ResumeVideosIfLifecycleActive();
            StartBcTimersIfConfigured();
        }

        protected override void OnMediaLifecycleStateChanged()
        {
            if (IsMediaLifecycleActive())
            {
                LogControlTrace("Media activity", "resume timers/video");
                ResumeMediaActivityIfAllowed();
            }
            else
            {
                LogControlTrace("Media activity", "pause timers/video");
                PauseMediaActivity();
            }
        }

        #endregion

        protected override void OnMediaLifecycleUnloadedCore()
        {
            DisposeBcTimers();
        }
    }


    public class PluginBackgroundImageDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool EnableRandomSelect { get; set; }
        public bool EnableRandomOnSelect { get; set; }
        public bool EnableRandomOnStart { get; set; }
        public bool EnableAutoChanger { get; set; }
    }
}