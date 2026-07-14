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
    public partial class PluginBackgroundImage : PluginMediaLifecycleControlBase, IMediaThemeSyncTarget
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

        private object _lastBlurMediaSource;
        private int _lastBlurRadius = -1;
        private RenderingBias? _lastBlurBias;
        private bool _isCurrentMediaVideo;

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
            MediaThemeSyncWindowHandler.EnsureRegistered(this);
        }


        void IMediaThemeSyncTarget.SyncThemePropertiesOnSettingsClose()
        {
            GetFadeImageProperties();
        }

        private void GetFadeImageProperties()
        {
            if (!PluginDatabase.PluginSettings.BackgroundImageSameSettings)
            {
                MediaControlDiagnostics.Trace(
                    LogControlTrace,
                    MediaControlDiagnostics.PhaseThemeSync,
                    "skipped, sameSettings=false");
                return;
            }

            int propsCopied = 0;
            bool partFound = false;
            int partDepth = 0;

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseThemeSync,
                LogControlTrace,
                LogControlIssue,
                slowThresholdMs: MediaControlDiagnostics.ThemeSyncSlowThresholdMs))
            {
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

                if (PART_ImageBackground_4 != null)
                {
                    partDepth = 4;
                }
                else if (PART_ImageBackground_3 != null)
                {
                    partDepth = 3;
                }
                else if (PART_ImageBackground_2 != null)
                {
                    partDepth = 2;
                }

                partFound = PART_ImageBackground != null;

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
                                        propsCopied++;
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
                else
                {
                    MediaControlDiagnostics.Issue(
                        LogControlIssue,
                        MediaControlDiagnostics.PhaseThemeSync,
                        "PART_ImageBackground not found");
                }

                MediaControlDiagnostics.Trace(
                    LogControlTrace,
                    MediaControlDiagnostics.PhaseThemeSync,
                    string.Format("props={0}, partFound={1}, partDepth={2}", propsCopied, partFound, partDepth));
            }
        }


        public override void SetData(Game newContext, PluginGameEntry PluginGameData)
        {
            GameBackgroundImages = (GameBackgroundImages)PluginGameData;

            Video1.Volume = PluginDatabase.PluginSettings.Volume / 10;
            Video2.Volume = PluginDatabase.PluginSettings.Volume / 10;

            string setDataDetail = string.Format(
                "game={0}, hasData={1}",
                MediaControlDiagnostics.FormatGameRef(GameContext),
                GameBackgroundImages.HasDataBackground);

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseSetData,
                LogControlTrace,
                LogControlIssue,
                setDataDetail))
            {
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
        }


        public void SetBackground()
        {
            string pathImage = string.Empty;

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseMediaSelect,
                LogControlTrace,
                LogControlIssue))
            {
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
                                LogMediaSelect("auto-changer-favorite-first", pathImage);
                            }
                            else
                            {
                                Counter = random.Next(0, GameBackgroundImages.ItemsBackground.Count);
                                pathImage = GameBackgroundImages.ItemsBackground[Counter].FullPath;
                                LogMediaSelect("auto-changer-random", pathImage);
                            }
                        }
                        else
                        {
                            if (IsFirst && ItemFavorite != null)
                            {
                                pathImage = ItemFavorite.FullPath;
                                Counter = GameBackgroundImages.ItemsBackground.FindIndex(x => x.IsFavorite);
                                LogMediaSelect("auto-changer-favorite-first", pathImage);
                            }
                            else
                            {
                                pathImage = GameBackgroundImages.ItemsBackground[Counter].FullPath;
                                LogMediaSelect("auto-changer-sequential", pathImage);
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
                            LogMediaSelect("random-on-start", pathImage);
                        }
                        else
                        {
                            if (IsFirst && ItemFavorite != null)
                            {
                                pathImage = ItemFavorite.FullPath;
                                LogMediaSelect("favorite", pathImage);
                            }
                            else
                            {
                                int imgSelected = random.Next(0, GameBackgroundImages.ItemsBackground.Count);
                                pathImage = GameBackgroundImages.ItemsBackground[imgSelected].FullPath;
                                LogMediaSelect("random-on-select", pathImage, imgSelected);
                            }
                        }

                        SetBackgroundImage(pathImage);
                    }
                    else
                    {
                        if (ItemFavorite != null)
                        {
                            pathImage = ItemFavorite.FullPath;
                            LogMediaSelect("favorite", pathImage);
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
        }

        public void SetDefaultBackgroundImage()
        {
            if (GameContext.BackgroundImage.IsNullOrEmpty())
            {
                LogMediaSelect("default-playnite", null);
                SetBackgroundImage();
            }
            else
            {
                string pathImage = ImageSourceManager.GetImagePath(GameContext.BackgroundImage)
                    ?? API.Instance.Database.GetFullFilePath(GameContext.BackgroundImage);
                LogMediaSelect("default-playnite", pathImage);
                SetBackgroundImage(pathImage);
            }
        }

        public void SetBackgroundImage(string pathImage = null)
        {
            bool exists = !string.IsNullOrEmpty(pathImage) && File.Exists(pathImage);
            bool isVideo = !string.IsNullOrEmpty(pathImage) && Path.GetExtension(pathImage).IsEqual(".mp4");

            if (!string.IsNullOrEmpty(pathImage) && !exists)
            {
                MediaControlDiagnostics.Issue(
                    LogControlIssue,
                    MediaControlDiagnostics.PhaseSourceSet,
                    string.Format("file missing: {0}", MediaControlDiagnostics.FormatFileName(pathImage)));
            }

            MediaControlDiagnostics.Trace(
                LogControlTrace,
                MediaControlDiagnostics.PhaseSourceSet,
                MediaControlDiagnostics.FormatSourceSetDetail(pathImage, exists, isVideo, "Render"));

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
            string oldPath = oldSource as string;
            string newPath = newSource as string;

            try
            {
                if (newSource?.Equals(currentSource) == true)
                {
                    MediaControlDiagnostics.Trace(
                        LogControlTrace,
                        MediaControlDiagnostics.PhaseLoadNewSource,
                        MediaControlDiagnostics.FormatLoadNewSourceDetail(
                            oldPath,
                            newPath,
                            true,
                            "duplicate",
                            currentImage.ToString()));

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

                string fadeBranch = AnimationEnabled ? "crossfade" : "direct-swap";

                using (MediaControlDiagnostics.BeginScope(
                    MediaControlDiagnostics.PhaseLoadNewSource,
                    LogControlTrace,
                    LogControlIssue,
                    MediaControlDiagnostics.FormatLoadNewSourceDetail(
                        oldPath,
                        newPath,
                        false,
                        fadeBranch,
                        currentImage.ToString())))
                {
                    string image = null;

                    currentSource = newSource;

                    if (newSource is string)
                    {
                        if (!File.Exists(newSource.ToString()))
                        {
                            MediaControlDiagnostics.Issue(
                                LogControlIssue,
                                MediaControlDiagnostics.PhaseLoadNewSource,
                                string.Format("file missing: {0}", MediaControlDiagnostics.FormatFileName(newPath)));
                        }
                        else
                        {
                            image = (string)currentSource;
                        }
                    }

                    _isCurrentMediaVideo = !string.IsNullOrEmpty(image) && Path.GetExtension(image).IsEqual(".mp4");
                    ApplyAdaptiveBlurEffect();

                    if (AnimationEnabled)
                    {
                        if (image == null)
                        {
                            fadeBranch = "fade-out";

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
                                fadeBranch = "crossfade-to-Image1";
                                Image1FadeOut.Stop();

                                if (Path.GetExtension(image).IsEqual(".mp4"))
                                {
                                    Video1.Source = new Uri(image);
                                    Video1.LoadedBehavior = VideoStateForLifecycle();
                                }
                                else
                                {
                                    Image1.Source = image;
                                }

                                Image1FadeIn.Begin();
                                BorderDarken.Opacity = 1;
                                BorderDarkenFadeOut.Stop();
                                currentImage = CurrentImage.Image1;
                            }
                            else if (currentImage == CurrentImage.Image1)
                            {
                                fadeBranch = "crossfade-to-Image2";
                                Image2FadeOut.Stop();

                                if (Path.GetExtension(image).IsEqual(".mp4"))
                                {
                                    Video2.Source = new Uri(image);
                                    Video2.LoadedBehavior = VideoStateForLifecycle();
                                }
                                else
                                {
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
                                fadeBranch = "crossfade-to-Image1";
                                Image1FadeOut.Stop();

                                if (Path.GetExtension(image).IsEqual(".mp4"))
                                {
                                    Video1.Source = new Uri(image);
                                    Video1.LoadedBehavior = VideoStateForLifecycle();
                                }
                                else
                                {
                                    Image1.Source = image;
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
                        fadeBranch = "direct-swap";

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
                LogTimerTickLifecycleIfInactive("auto-changer");

                InvokeOnUiIfLifecycleActive(() =>
                {
                    string pathImage = string.Empty;

                    if (ControlDataContext.EnableRandomSelect)
                    {
                        if (GameBackgroundImages.ItemsBackground.Count != 0)
                        {
                            int imgSelected = random.Next(0, GameBackgroundImages.ItemsBackground.Count);
                            while (imgSelected == Counter && GameBackgroundImages.ItemsBackground.Count != 1)
                            {
                                imgSelected = random.Next(0, GameBackgroundImages.ItemsBackground.Count);
                            }
                            Counter = imgSelected;

                            pathImage = GameBackgroundImages.ItemsBackground[imgSelected].FullPath;
                        }

                        MediaControlDiagnostics.Trace(
                            LogControlTrace,
                            MediaControlDiagnostics.PhaseTimerTick,
                            MediaControlDiagnostics.FormatTimerTickDetail("auto-changer-random", pathImage, true));

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

                        MediaControlDiagnostics.Trace(
                            LogControlTrace,
                            MediaControlDiagnostics.PhaseTimerTick,
                            MediaControlDiagnostics.FormatTimerTickDetail("auto-changer-sequential", pathImage, true));

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
                LogTimerTickLifecycleIfInactive("video-delay");

                InvokeOnUiIfLifecycleActive(() =>
                {
                    string pathVideo = GameBackgroundImages?.ItemsBackground?.Where(x => x.IsVideo && x.Exist)?.OrderBy(x => x.IsFavorite)?.FirstOrDefault()?.FullPath;

                    MediaControlDiagnostics.Trace(
                        LogControlTrace,
                        MediaControlDiagnostics.PhaseTimerTick,
                        MediaControlDiagnostics.FormatTimerTickDetail("video-delay", pathVideo, true));

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
                _lastBlurMediaSource = null;
                _lastBlurRadius = -1;
                _lastBlurBias = null;

                if (ImageHolder.Effect != null)
                {
                    ImageHolder.Effect = null;
                }
                return;
            }

            bool isAutoChangerEnabled = ControlDataContext != null && ControlDataContext.EnableAutoChanger;
            bool hasVideoSource = _isCurrentMediaVideo || (Video1?.Source != null) || (Video2?.Source != null);
            int blurRadius = Math.Max(0, BlurAmount);
            RenderingBias renderingBias = ResolveAdaptiveBlurBias(isAutoChangerEnabled, hasVideoSource, blurRadius);
            string sourceKey = currentSource as string ?? Source as string;

            if (sourceKey != null
                && sourceKey.Equals(_lastBlurMediaSource)
                && blurRadius == _lastBlurRadius
                && _lastBlurBias == renderingBias
                && ImageHolder.Effect is BlurEffect existingBlur
                && existingBlur.Radius == blurRadius
                && existingBlur.RenderingBias == renderingBias)
            {
                MediaControlDiagnostics.Trace(
                    LogControlTrace,
                    MediaControlDiagnostics.PhaseBlur,
                    "skipped unchanged source");
                return;
            }

            try
            {
                ImageHolder.Effect = new BlurEffect
                {
                    KernelType = KernelType.Gaussian,
                    Radius = blurRadius,
                    RenderingBias = renderingBias
                };

                _lastBlurMediaSource = sourceKey;
                _lastBlurRadius = blurRadius;
                _lastBlurBias = renderingBias;

                MediaControlDiagnostics.Trace(
                    LogControlTrace,
                    MediaControlDiagnostics.PhaseBlur,
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

                _lastBlurMediaSource = sourceKey;
                _lastBlurRadius = blurRadius;
                _lastBlurBias = RenderingBias.Performance;
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

        #region Media diagnostics

        private void LogMediaSelect(string branch, string pathImage, int index = -1, int total = -1)
        {
            int itemIndex = index >= 0 ? index : Counter;
            int itemTotal = total >= 0 ? total : (GameBackgroundImages?.ItemsBackground?.Count ?? 0);

            MediaControlDiagnostics.Trace(
                LogControlTrace,
                MediaControlDiagnostics.PhaseMediaSelect,
                MediaControlDiagnostics.FormatMediaSelectDetail(GameContext, branch, itemIndex, itemTotal, pathImage));
        }

        private void LogTimerTickLifecycleIfInactive(string timerType)
        {
            Dispatcher dispatcher = API.Instance?.MainView?.UIDispatcher ?? Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (!IsLifecycleDisplayActive())
                {
                    MediaControlDiagnostics.Issue(
                        LogControlIssue,
                        MediaControlDiagnostics.PhaseTimerTick,
                        string.Format("timer={0}, lifecycle=inactive", timerType));
                }
            }));
        }

        #endregion

        protected override void OnMediaLifecycleUnloadedCore()
        {
            MediaThemeSyncWindowHandler.Unregister(this);
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