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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Theme control that displays shuffled game icons from the plugin collection.
    /// </summary>
    /// <remarks>
    /// Icons support <b>Default</b> and <b>OnStart</b> only — no Timer and no OnSelect (unlike cover/background).
    /// BCThemes hosts one instance per game row in Details list view; a per-control timer would spawn dozens of
    /// concurrent timers and cascade pause/resume through media lifecycle, breaking overview controls.
    /// OnStart draws once per game session via <see cref="GameBackgroundImages.IconImageOnStart"/>, which is safe
    /// when many rows are visible at once.
    /// </remarks>
    public partial class PluginIconImage : PluginMediaLifecycleControlBase, IMediaThemeSyncTarget
    {
        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;
        protected override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginIconImageDataContext ControlDataContext = new PluginIconImageDataContext();
        protected override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginIconImageDataContext)controlDataContext;
        }

        private GameBackgroundImages GameBackgroundImages { get; set; }

        private const double MinDecodePixelHeight = 32;
        private const string ImageDecodeParameterAuto = "0";

        private int _lastLoggedDecodeHeight = -1;

        protected override void AttachStaticEvents()
        {
            base.AttachStaticEvents();

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
            ControlDataContext = new PluginIconImageDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.EnableIconImage,
                EnableRandomSelect = PluginDatabase.PluginSettings.EnableIconImageRandomSelect,
                EnableRandomOnStart = PluginDatabase.PluginSettings.EnableIconImageRandomOnStart,

                ImageSource = null,
                VideoSource = null
            };
        }


        public PluginIconImage()
        {
            InitializeComponent();

            Delay = 0;
            DataContext = ControlDataContext;
            SizeChanged += OnIconSizeChanged;
            InitializeMediaLifecycleHooks();
            MediaThemeSyncWindowHandler.EnsureRegistered(this);
        }


        void IMediaThemeSyncTarget.SyncThemePropertiesOnSettingsClose()
        {
            GetIconProperties();
        }

        private void GetIconProperties()
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
                FrameworkElement partImageIcon = UIHelper.SearchElementByName("PART_ImageIcon", thisParent, false, false);

                partFound = partImageIcon != null;

                if (partImageIcon != null)
                {
                    PropertyInfo[] iconProperties = partImageIcon.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    PropertyInfo[] pluginIconProperties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    List<string> usedProperties = new List<string>
                    {
                        "Stretch", "StretchDirection"
                    };

                    foreach (PropertyInfo propIcon in iconProperties)
                    {
                        if (propIcon.CanWrite && usedProperties.Contains(propIcon.Name))
                        {
                            PropertyInfo propPluginIcon = pluginIconProperties.FirstOrDefault(x => x.Name == propIcon.Name);
                            try
                            {
                                if (propPluginIcon != null)
                                {
                                    object value = propIcon.GetValue(partImageIcon, null);
                                    propPluginIcon.SetValue(this, value, null);
                                    propsCopied++;
                                }
                                else
                                {
                                    Logger.Warn($"No property for {propIcon.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                            }
                        }
                    }
                }
                else
                {
                    MediaControlDiagnostics.Issue(
                        LogControlIssue,
                        MediaControlDiagnostics.PhaseThemeSync,
                        "PART_ImageIcon not found");
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
                GameBackgroundImages.HasDataIcon);

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseSetData,
                LogControlTrace,
                LogControlIssue,
                setDataDetail))
            {
                try
                {
                    Video1.LoadedBehavior = MediaState.Stop;

                    if (!GameBackgroundImages.HasDataIcon)
                    {
                        PauseVideos();
                        MustDisplay = false;
                        return;
                    }

                    SetIcon();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }
        }


        /// <summary>
        /// Resolves which icon file to show from plugin data and current selection-mode flags.
        /// </summary>
        /// <remarks>
        /// Mode matrix: Default (favorite or Playnite icon), OnStart (one random pick per game, stable on
        /// re-selection). Timer was removed — see class remarks.
        /// </remarks>
        public void SetIcon()
        {
            string pathImage = string.Empty;

            using (MediaControlDiagnostics.BeginScope(
                MediaControlDiagnostics.PhaseMediaSelect,
                LogControlTrace,
                LogControlIssue))
            {
                if (GameBackgroundImages.HasDataIcon)
                {
                    ItemImage itemFavorite = GameBackgroundImages.ItemsIcon.FirstOrDefault(x => x.IsFavorite);
                    bool onStartMode = ControlDataContext.EnableRandomSelect
                        && ControlDataContext.EnableRandomOnStart;

                    int itemsCount = GameBackgroundImages.ItemsIcon.Count;
                    int favIndex = itemFavorite != null
                        ? GameBackgroundImages.ItemsIcon.FindIndex(x => x.IsFavorite)
                        : -1;

                    string modeName;
                    if (onStartMode)
                    {
                        modeName = "OnStart+Random";
                    }
                    else if (ControlDataContext.EnableRandomSelect)
                    {
                        modeName = "OnStart+Random";
                    }
                    else if (itemFavorite != null)
                    {
                        modeName = "Favorite";
                    }
                    else
                    {
                        modeName = "Default";
                    }

                    Common.LogDebug(
                        true,
                        string.Format(
                            "[PluginIconImage][Mode] game={0}, mode={1}, randomSelect={2}, randomOnStart={3}, items={4}, favIndex={5}",
                            MediaControlDiagnostics.FormatGameRef(GameContext),
                            modeName,
                            ControlDataContext.EnableRandomSelect,
                            ControlDataContext.EnableRandomOnStart,
                            itemsCount,
                            favIndex));

                    // OnStart: IconImageOnStart is computed once when game data loads; stable across re-selections.
                    if (onStartMode)
                    {
                        pathImage = GameBackgroundImages.IconImageOnStart.FullPath;
                        LogMediaSelect("random-on-start", pathImage);
                        int onStartIndex = GameBackgroundImages.ItemsIcon.FindIndex(x => x.FullPath == pathImage);
                        Common.LogDebug(
                            true,
                            string.Format(
                                "[PluginIconImage][Change] branch={0}, index={1}/{2}, file={3}",
                                "random-on-start",
                                onStartIndex,
                                itemsCount,
                                MediaControlDiagnostics.FormatFileName(pathImage)));
                        SetIconImage(pathImage);
                    }
                    else if (ControlDataContext.EnableRandomSelect)
                    {
                        // Legacy / coerce miss: treat as OnStart. Icons never use OnSelect or Timer.
                        pathImage = GameBackgroundImages.IconImageOnStart.FullPath;
                        LogMediaSelect("random-on-start", pathImage);
                        int onStartIndex = GameBackgroundImages.ItemsIcon.FindIndex(x => x.FullPath == pathImage);
                        Common.LogDebug(
                            true,
                            string.Format(
                                "[PluginIconImage][Change] branch={0}, index={1}/{2}, file={3}",
                                "random-on-start",
                                onStartIndex,
                                itemsCount,
                                MediaControlDiagnostics.FormatFileName(pathImage)));
                        SetIconImage(pathImage);
                    }
                    else
                    {
                        if (itemFavorite != null)
                        {
                            pathImage = itemFavorite.FullPath;
                            LogMediaSelect("favorite", pathImage, favIndex, itemsCount);
                            SetIconImage(pathImage);
                            Common.LogDebug(
                                true,
                                string.Format(
                                    "[PluginIconImage][Change] branch={0}, index={1}/{2}, file={3}",
                                    "favorite",
                                    favIndex,
                                    itemsCount,
                                    MediaControlDiagnostics.FormatFileName(pathImage)));
                        }
                        else
                        {
                            SetDefaultIconImage();
                        }
                    }
                }
                else
                {
                    SetDefaultIconImage();
                }
            }
        }

        public void SetDefaultIconImage()
        {
            if (GameContext.Icon.IsNullOrEmpty())
            {
                LogMediaSelect("default-playnite", null);
                SetIconImage();
                Common.LogDebug(true, string.Format("[PluginIconImage][Change] branch={0}, file={1}", "default-playnite", "(null)"));
            }
            else
            {
                string pathImage = ImageSourceManager.GetImagePath(GameContext.Icon)
                    ?? API.Instance.Database.GetFullFilePath(GameContext.Icon);
                LogMediaSelect("default-playnite", pathImage);
                SetIconImage(pathImage);
                Common.LogDebug(
                    true,
                    string.Format(
                        "[PluginIconImage][Change] branch={0}, index={1}/{2}, file={3}",
                        "default-playnite",
                        -1,
                        -1,
                        MediaControlDiagnostics.FormatFileName(pathImage)));
            }
        }

        public void SetIconImage(string pathImage = null)
        {
            UpdateImageDecodePixelHeight();

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
                MediaControlDiagnostics.FormatSourceSetDetail(pathImage, exists, isVideo, "Loaded"));

            if (!exists)
            {
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
            typeof(PluginIconImage),
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
            typeof(PluginIconImage),
            new PropertyMetadata(Stretch.UniformToFill));

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        #endregion Stretch

        #region StretchDirection

        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(
            nameof(StretchDirection),
            typeof(StretchDirection),
            typeof(PluginIconImage),
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
            PluginIconImage control = (PluginIconImage)obj;
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


        private void ImageHolder_Loaded(object sender, RoutedEventArgs e)
        {
            GetIconProperties();

            AttachApplicationFocusEvents();
            UpdateImageDecodePixelHeight();
        }

        private void OnIconSizeChanged(object sender, SizeChangedEventArgs e)
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

        // No BcTimer here — icon random is OnStart-only (see class remarks). Lifecycle hooks pause/resume video only.

        private bool IsMediaLifecycleActive()
        {
            return IsLifecycleDisplayActive();
        }

        private MediaState VideoStateForLifecycle()
        {
            return IsMediaLifecycleActive() ? MediaState.Play : MediaState.Pause;
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
            PauseVideos();
        }

        private void ResumeMediaActivityIfAllowed()
        {
            if (!IsMediaLifecycleActive())
            {
                return;
            }

            ResumeVideosIfLifecycleActive();
        }

        protected override void OnMediaLifecycleStateChanged()
        {
            if (IsMediaLifecycleActive())
            {
                LogControlTrace("Media activity", "resume video");
                ResumeMediaActivityIfAllowed();
            }
            else
            {
                LogControlTrace("Media activity", "pause video");
                PauseMediaActivity();
            }
        }

        #endregion

        #region Media diagnostics

        private void LogMediaSelect(string branch, string pathImage, int index = -1, int total = -1)
        {
            int itemIndex = index;
            int itemTotal = total >= 0 ? total : (GameBackgroundImages?.ItemsIcon?.Count ?? 0);

            MediaControlDiagnostics.Trace(
                LogControlTrace,
                MediaControlDiagnostics.PhaseMediaSelect,
                MediaControlDiagnostics.FormatMediaSelectDetail(GameContext, branch, itemIndex, itemTotal, pathImage));
        }

        #endregion

        protected override void OnMediaLifecycleUnloadedCore()
        {
            MediaThemeSyncWindowHandler.Unregister(this);
        }
    }


    public class PluginIconImageDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool EnableRandomSelect { get; set; }
        public bool EnableRandomOnStart { get; set; }

        public string ImageSource { get; set; }
        public string VideoSource { get; set; }
    }
}
