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
using System.Windows.Threading;
using System.Threading;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginCoverImage.xaml
    /// </summary>
    public partial class PluginCoverImage : PluginMediaLifecycleControlBase, IMediaThemeSyncTarget
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

        private static readonly Random random = new Random();

        private const double MinDecodePixelHeight = 100;
        private const string ImageDecodeParameterAuto = "0";

        private int _lastLoggedDecodeHeight = -1;
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

            ControlDataContext = new PluginCoverImageDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.EnableCoverImage,
                EnableRandomSelect = PluginDatabase.PluginSettings.EnableCoverImageRandomSelect,
                EnableRandomOnSelect = PluginDatabase.PluginSettings.EnableCoverImageRandomOnSelect,
                EnableRandomOnStart = PluginDatabase.PluginSettings.EnableCoverImageRandomOnStart,
                EnableAutoChanger = PluginDatabase.PluginSettings.EnableCoverImageAutoChanger,

                ImageSource = null,
                VideoSource = null
            };
        }


        public PluginCoverImage()
        {
            InitializeComponent();

            Delay = 0;
            DataContext = ControlDataContext;
            Loaded += OnLoaded;
            SizeChanged += OnCoverSizeChanged;
            InitializeMediaLifecycleHooks();
            MediaThemeSyncWindowHandler.EnsureRegistered(this);
        }


        void IMediaThemeSyncTarget.SyncThemePropertiesOnSettingsClose()
        {
            GetCoverProperties();
        }

        private void GetCoverProperties()
        {
            int propsCopied = 0;
            bool partFound = false;

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseThemeSync,
                LogControlTrace,
                LogControlIssue,
                slowThresholdMs: MediaControlDiagnostics.ThemeSyncSlowThresholdMs))
            {
                DependencyObject thisParent = ((FrameworkElement)((FrameworkElement)((FrameworkElement)this.Parent).Parent).Parent).Parent;
                FrameworkElement PART_ImageCover = UIHelper.SearchElementByName("PART_ImageCover", thisParent, false, false);

                partFound = PART_ImageCover != null;

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
                        "PART_ImageCover not found");
                }

                MediaControlDiagnostics.Trace(
                    LogControlTrace,
                    MediaControlDiagnostics.PhaseThemeSync,
                    string.Format("props={0}, partFound={1}, partDepth=0", propsCopied, partFound));
            }
        }


        public override void SetData(Game newContext, PluginGameEntry PluginGameData)
        {
            GameBackgroundImages = (GameBackgroundImages)PluginGameData;

            string setDataDetail = string.Format(
                "game={0}, hasData={1}",
                MediaControlDiagnostics.FormatGameRef(GameContext),
                GameBackgroundImages.HasDataCover);

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseSetData,
                LogControlTrace,
                LogControlIssue,
                setDataDetail))
            {
                try
                {
                    Video1.LoadedBehavior = MediaState.Stop;

                    if (!GameBackgroundImages.HasDataCover)
                    {
                        DisposeBcTimers();
                        PauseVideos();
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
        }


        public void SetCover()
        {
            string pathImage = string.Empty;

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseMediaSelect,
                LogControlTrace,
                LogControlIssue))
            {
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
                                LogMediaSelect("auto-changer-favorite-first", pathImage);
                            }
                            else
                            {
                                Counter = random.Next(0, GameBackgroundImages.ItemsCover.Count);
                                pathImage = GameBackgroundImages.ItemsCover[Counter].FullPath;
                                LogMediaSelect("auto-changer-random", pathImage);
                            }
                        }
                        else
                        {
                            if (IsFirst && ItemFavorite != null)
                            {
                                pathImage = ItemFavorite.FullPath;
                                Counter = GameBackgroundImages.ItemsCover.FindIndex(x => x.IsFavorite);
                                LogMediaSelect("auto-changer-favorite-first", pathImage);
                            }
                            else
                            {
                                pathImage = GameBackgroundImages.ItemsCover[Counter].FullPath;
                                LogMediaSelect("auto-changer-sequential", pathImage);
                            }
                        }

                        SetCoverImage(pathImage);

                        BcTimer = new System.Timers.Timer(PluginDatabase.PluginSettings.CoverImageAutoChangerTimer * 1000)
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
                            pathImage = GameBackgroundImages.CoverImageOnStart.FullPath;
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
                                int imgSelected = random.Next(0, GameBackgroundImages.ItemsCover.Count);
                                pathImage = GameBackgroundImages.ItemsCover[imgSelected].FullPath;
                                LogMediaSelect("random-on-select", pathImage, imgSelected);
                            }
                        }

                        SetCoverImage(pathImage);
                    }
                    else
                    {
                        if (ItemFavorite != null)
                        {
                            pathImage = ItemFavorite.FullPath;
                            LogMediaSelect("favorite", pathImage);
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

                if (PluginDatabase.PluginSettings.useVideoDelayCoverImage)
                {
                    BcTimerVideo = new System.Timers.Timer(PluginDatabase.PluginSettings.videoDelayCoverImage * 1000)
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

        public void SetDefaultCoverImage()
        {
            if (GameContext.CoverImage.IsNullOrEmpty())
            {
                LogMediaSelect("default-playnite", null);
                SetCoverImage();
            }
            else
            {
                string pathImage = ImageSourceManager.GetImagePath(GameContext.CoverImage)
                    ?? API.Instance.Database.GetFullFilePath(GameContext.CoverImage);
                LogMediaSelect("default-playnite", pathImage);
                SetCoverImage(pathImage);
            }
        }

        public void SetCoverImage(string pathImage = null)
        {
            UpdateImageDecodePixelHeight();

            bool exists = !string.IsNullOrEmpty(pathImage) && File.Exists(pathImage);
            bool isVideo = !string.IsNullOrEmpty(pathImage) && Path.GetExtension(pathImage).IsEqual(".mp4");
            _isCurrentMediaVideo = exists && isVideo;

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
                MediaControlDiagnostics.FormatSourceSetDetail(pathImage, exists, isVideo, "Loaded"));

            if (!exists)
            {
                _isCurrentMediaVideo = false;
                ControlDataContext.ImageSource = null;
                ControlDataContext.VideoSource = null;
                return;
            }

            if (Path.GetExtension(pathImage).IsEqual(".mp4"))
            {
                ControlDataContext.ImageSource = null;
                ControlDataContext.VideoSource = pathImage;

                Video1.LoadedBehavior = VideoStateForLifecycle();
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
            string oldPath = oldSource as string;
            string newPath = newSource as string;

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
                        "Image1"));

                if (Video1.Source != null)
                {
                    Video1.LoadedBehavior = VideoStateForLifecycle();
                }

                return;
            }

            string fadeBranch = "direct-swap";

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseLoadNewSource,
                LogControlTrace,
                LogControlIssue,
                MediaControlDiagnostics.FormatLoadNewSourceDetail(
                    oldPath,
                    newPath,
                    false,
                    fadeBranch,
                    "Image1")))
            {
                string image = null;

                currentSource = newSource;

                if (newSource != null && newSource is string)
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

                if (!string.IsNullOrEmpty(image) && Path.GetExtension(image).IsEqual(".mp4"))
                {
                    fadeBranch = "video";
                    Image1.Source = null;
                    Video1.Source = new Uri(image);

                    Video1.LoadedBehavior = VideoStateForLifecycle();
                }
                else
                {
                    fadeBranch = "image";
                    Image1.Source = image;
                    Video1.Source = null;
                }
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
                        if (GameBackgroundImages.ItemsCover.Count != 0)
                        {
                            int imgSelected = random.Next(0, (GameBackgroundImages.ItemsCover.Count));
                            while (imgSelected == Counter && GameBackgroundImages.ItemsCover.Count != 1)
                            {
                                imgSelected = random.Next(0, (GameBackgroundImages.ItemsCover.Count));
                            }
                            Counter = imgSelected;

                            pathImage = GameBackgroundImages.ItemsCover[imgSelected].FullPath;
                        }

                        MediaControlDiagnostics.Trace(
                            LogControlTrace,
                            MediaControlDiagnostics.PhaseTimerTick,
                            MediaControlDiagnostics.FormatTimerTickDetail("auto-changer-random", pathImage, true));

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

                        MediaControlDiagnostics.Trace(
                            LogControlTrace,
                            MediaControlDiagnostics.PhaseTimerTick,
                            MediaControlDiagnostics.FormatTimerTickDetail("auto-changer-sequential", pathImage, true));

                        SetCoverImage(pathImage);
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
                    string pathVideo = GameBackgroundImages?.ItemsCover?.Where(x => x.IsVideo && x.Exist)?.OrderBy(x => x.IsFavorite)?.FirstOrDefault()?.FullPath;

                    MediaControlDiagnostics.Trace(
                        LogControlTrace,
                        MediaControlDiagnostics.PhaseTimerTick,
                        MediaControlDiagnostics.FormatTimerTickDetail("video-delay", pathVideo, true));

                    if (!pathVideo.IsNullOrEmpty())
                    {
                        SetCoverImage(pathVideo);
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
            GetCoverProperties();

            AttachApplicationFocusEvents();
            UpdateImageDecodePixelHeight();
        }

        private void OnCoverSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateImageDecodePixelHeight();
        }

        /// <summary>
        /// Adapts <see cref="ImageAsync.DecodePixelHeight"/> to the control display size (grid vs details).
        /// Requires <c>Parameter="0"</c> on ImageAsync so <see cref="CommonPluginsShared.Converters.ImageConverter"/> applies resize buckets.
        /// </summary>
        private void UpdateImageDecodePixelHeight()
        {
            if (Image1 == null)
            {
                return;
            }

            double displayHeight = ActualHeight;
            if (double.IsNaN(displayHeight) || displayHeight <= 0)
            {
                displayHeight = RenderSize.Height;
            }

            if (displayHeight <= 0)
            {
                return;
            }

            int decodeHeight = (int)Math.Max(MinDecodePixelHeight, Math.Round(displayHeight));
            if (Math.Abs(Image1.DecodePixelHeight - decodeHeight) < 1)
            {
                return;
            }

            Image1.Parameter = ImageDecodeParameterAuto;
            Image1.DecodePixelHeight = decodeHeight;

            if (_lastLoggedDecodeHeight != decodeHeight)
            {
                _lastLoggedDecodeHeight = decodeHeight;
                MediaControlDiagnostics.Trace(
                    LogControlTrace,
                    MediaControlDiagnostics.PhasePerf,
                    string.Format("decodeHeight={0}, actualHeight={1:F0}", decodeHeight, displayHeight));
            }
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

            if (PluginDatabase.PluginSettings.useVideoDelayCoverImage && BcTimerVideo != null)
            {
                BcTimerVideo.Start();
            }
        }

        private void PauseVideos()
        {
            Video1.LoadedBehavior = MediaState.Pause;
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
            int itemTotal = total >= 0 ? total : (GameBackgroundImages?.ItemsCover?.Count ?? 0);

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


    public class PluginCoverImageDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool EnableRandomSelect { get; set; }
        public bool EnableRandomOnSelect { get; set; }
        public bool EnableRandomOnStart { get; set; }
        public bool EnableAutoChanger { get; set; }

        public string ImageSource { get; set; }
        public string VideoSource { get; set; }
    }
}