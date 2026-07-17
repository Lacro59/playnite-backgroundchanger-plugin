using BackgroundChanger.Services;
using Playnite.SDK;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Settings view for BackgroundChanger plugin options.
    /// </summary>
    public partial class BackgroundChangerSettingsView : UserControl
    {
        private enum MediaSelectionMode
        {
            Default,
            OnStart,
            OnSelect,
            Timer
        }

        private static BackgroundChangerSettingsView _activeInstance;

        private bool _suppressModeHandlers;

        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        private BackgroundChangerSettings EditingSettings =>
            DataContext is BackgroundChangerSettingsViewModel viewModel ? viewModel.Settings : PluginDatabase.PluginSettings;

        public BackgroundChangerSettingsView()
        {
            InitializeComponent();
            _activeInstance = this;
            Loaded += OnSettingsViewLoaded;
            Unloaded += OnSettingsViewUnloaded;
        }

        /// <summary>
        /// Writes exclusive selection-mode flags from the active settings view into <paramref name="settings"/>.
        /// </summary>
        /// <param name="settings">Settings instance being saved.</param>
        public static void ApplyActiveSelectionModes(BackgroundChangerSettings settings)
        {
            _activeInstance?.ApplySelectionModesToSettings(settings);
        }

        /// <summary>
        /// Writes exclusive selection-mode flags from the UI radios into <paramref name="settings"/>.
        /// </summary>
        /// <param name="settings">Settings instance being saved.</param>
        public void ApplySelectionModesToSettings(BackgroundChangerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            ApplyBackgroundMode(settings, ResolveBackgroundMode(), cbBackgroundTimerRandom.IsChecked == true);
            ApplyCoverMode(settings, ResolveCoverMode(), cbCoverTimerRandom.IsChecked == true);
            ApplyIconMode(settings, ResolveIconMode());
        }

        private void OnSettingsViewLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnSettingsViewLoaded;
            LoadSelectionModesFromSettings(EditingSettings);
        }

        private void OnSettingsViewUnloaded(object sender, RoutedEventArgs e)
        {
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }
        }

        private void ButtonFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = API.Instance.Dialogs.SelectFile("File|ffmpeg.exe");
            if (!selectedFile.IsNullOrEmpty())
            {
                PART_FfmpegFile.Text = selectedFile;
                var settings = ((BackgroundChangerSettingsViewModel)DataContext).Settings;
                settings.ffmpegFile = selectedFile;

                string siblingFfprobe = Path.Combine(Path.GetDirectoryName(selectedFile) ?? string.Empty, "ffprobe.exe");
                if (settings.ffprobeFile.IsNullOrWhiteSpace() && File.Exists(siblingFfprobe))
                {
                    PART_FfprobeFile.Text = siblingFfprobe;
                    settings.ffprobeFile = siblingFfprobe;
                }
            }
        }

        private void ButtonFfprobe_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = API.Instance.Dialogs.SelectFile("File|ffprobe.exe");
            if (!selectedFile.IsNullOrEmpty())
            {
                PART_FfprobeFile.Text = selectedFile;
                ((BackgroundChangerSettingsViewModel)DataContext).Settings.ffprobeFile = selectedFile;
            }
        }

        private void OnBackgroundSelectionModeChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressModeHandlers)
            {
                return;
            }

            BackgroundChangerSettings settings = EditingSettings;
            if (settings == null || !(sender is RadioButton radio) || radio.IsChecked != true)
            {
                return;
            }

            ApplyBackgroundMode(settings, ResolveBackgroundMode(), cbBackgroundTimerRandom.IsChecked == true);
        }

        private void OnCoverSelectionModeChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressModeHandlers)
            {
                return;
            }

            BackgroundChangerSettings settings = EditingSettings;
            if (settings == null || !(sender is RadioButton radio) || radio.IsChecked != true)
            {
                return;
            }

            ApplyCoverMode(settings, ResolveCoverMode(), cbCoverTimerRandom.IsChecked == true);
        }

        private void OnIconSelectionModeChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressModeHandlers)
            {
                return;
            }

            BackgroundChangerSettings settings = EditingSettings;
            if (settings == null || !(sender is RadioButton radio) || radio.IsChecked != true)
            {
                return;
            }

            ApplyIconMode(settings, ResolveIconMode());
        }

        private void OnBackgroundTimerRandomChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressModeHandlers || EditingSettings == null || rbBackgroundTimer.IsChecked != true)
            {
                return;
            }

            EditingSettings.EnableBackgroundImageRandomSelect = cbBackgroundTimerRandom.IsChecked == true;
        }

        private void OnCoverTimerRandomChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressModeHandlers || EditingSettings == null || rbCoverTimer.IsChecked != true)
            {
                return;
            }

            EditingSettings.EnableCoverImageRandomSelect = cbCoverTimerRandom.IsChecked == true;
        }

        private void LoadSelectionModesFromSettings(BackgroundChangerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            _suppressModeHandlers = true;
            try
            {
                MediaSelectionMode backgroundMode = ResolveModeFromFlags(
                    settings.EnableBackgroundImageAutoChanger,
                    settings.EnableBackgroundImageRandomSelect,
                    settings.EnableBackgroundImageRandomOnStart,
                    settings.EnableBackgroundImageRandomOnSelect,
                    allowOnSelect: true);
                SetBackgroundModeRadios(backgroundMode);
                cbBackgroundTimerRandom.IsChecked = settings.EnableBackgroundImageAutoChanger
                    && settings.EnableBackgroundImageRandomSelect;

                MediaSelectionMode coverMode = ResolveModeFromFlags(
                    settings.EnableCoverImageAutoChanger,
                    settings.EnableCoverImageRandomSelect,
                    settings.EnableCoverImageRandomOnStart,
                    settings.EnableCoverImageRandomOnSelect,
                    allowOnSelect: true);
                SetCoverModeRadios(coverMode);
                cbCoverTimerRandom.IsChecked = settings.EnableCoverImageAutoChanger
                    && settings.EnableCoverImageRandomSelect;

                // Legacy icon OnSelect → OnStart
                if (settings.EnableIconImageRandomOnSelect
                    && !settings.EnableIconImageRandomOnStart
                    && !settings.EnableIconImageAutoChanger)
                {
                    settings.EnableIconImageRandomOnStart = true;
                    settings.EnableIconImageRandomSelect = true;
                    settings.EnableIconImageRandomOnSelect = false;
                }

                // Legacy icon Timer → OnStart: UI no longer exposes Timer (multi-instance list hosts).
                if (settings.EnableIconImageAutoChanger)
                {
                    settings.EnableIconImageAutoChanger = false;
                    settings.EnableIconImageRandomOnStart = true;
                    settings.EnableIconImageRandomSelect = true;
                    settings.EnableIconImageRandomOnSelect = false;
                }

                MediaSelectionMode iconMode = ResolveModeFromFlags(
                    settings.EnableIconImageAutoChanger,
                    settings.EnableIconImageRandomSelect,
                    settings.EnableIconImageRandomOnStart,
                    onSelect: false,
                    allowOnSelect: false);
                SetIconModeRadios(iconMode);
            }
            finally
            {
                _suppressModeHandlers = false;
            }
        }

        private static MediaSelectionMode ResolveModeFromFlags(
            bool autoChanger,
            bool randomSelect,
            bool onStart,
            bool onSelect,
            bool allowOnSelect)
        {
            if (autoChanger)
            {
                return MediaSelectionMode.Timer;
            }

            if (randomSelect && allowOnSelect && onSelect && !onStart)
            {
                return MediaSelectionMode.OnSelect;
            }

            if (randomSelect && onStart)
            {
                return MediaSelectionMode.OnStart;
            }

            if (randomSelect)
            {
                return allowOnSelect && onSelect ? MediaSelectionMode.OnSelect : MediaSelectionMode.OnStart;
            }

            return MediaSelectionMode.Default;
        }

        private void SetBackgroundModeRadios(MediaSelectionMode mode)
        {
            rbBackgroundDefault.IsChecked = mode == MediaSelectionMode.Default;
            rbBackgroundOnStart.IsChecked = mode == MediaSelectionMode.OnStart;
            rbBackgroundOnSelect.IsChecked = mode == MediaSelectionMode.OnSelect;
            rbBackgroundTimer.IsChecked = mode == MediaSelectionMode.Timer;
        }

        private void SetCoverModeRadios(MediaSelectionMode mode)
        {
            rbCoverDefault.IsChecked = mode == MediaSelectionMode.Default;
            rbCoverOnStart.IsChecked = mode == MediaSelectionMode.OnStart;
            rbCoverOnSelect.IsChecked = mode == MediaSelectionMode.OnSelect;
            rbCoverTimer.IsChecked = mode == MediaSelectionMode.Timer;
        }

        private void SetIconModeRadios(MediaSelectionMode mode)
        {
            rbIconDefault.IsChecked = mode == MediaSelectionMode.Default;
            rbIconOnStart.IsChecked = mode == MediaSelectionMode.OnStart;
        }

        private MediaSelectionMode ResolveIconMode()
        {
            if (rbIconOnStart.IsChecked == true)
            {
                return MediaSelectionMode.OnStart;
            }

            return MediaSelectionMode.Default;
        }
        private MediaSelectionMode ResolveBackgroundMode()
        {
            if (rbBackgroundTimer.IsChecked == true)
            {
                return MediaSelectionMode.Timer;
            }

            if (rbBackgroundOnSelect.IsChecked == true)
            {
                return MediaSelectionMode.OnSelect;
            }

            if (rbBackgroundOnStart.IsChecked == true)
            {
                return MediaSelectionMode.OnStart;
            }

            return MediaSelectionMode.Default;
        }

        private static void ApplyBackgroundMode(BackgroundChangerSettings settings, MediaSelectionMode mode, bool timerRandom)
        {
            settings.EnableBackgroundImageAutoChanger = mode == MediaSelectionMode.Timer;
            settings.EnableBackgroundImageRandomOnStart = mode == MediaSelectionMode.OnStart;
            settings.EnableBackgroundImageRandomOnSelect = mode == MediaSelectionMode.OnSelect;

            if (mode == MediaSelectionMode.Timer)
            {
                settings.EnableBackgroundImageRandomSelect = timerRandom;
            }
            else if (mode == MediaSelectionMode.OnStart || mode == MediaSelectionMode.OnSelect)
            {
                settings.EnableBackgroundImageRandomSelect = true;
            }
            else
            {
                settings.EnableBackgroundImageRandomSelect = false;
            }
        }

        private MediaSelectionMode ResolveCoverMode()
        {
            if (rbCoverTimer.IsChecked == true)
            {
                return MediaSelectionMode.Timer;
            }

            if (rbCoverOnSelect.IsChecked == true)
            {
                return MediaSelectionMode.OnSelect;
            }

            if (rbCoverOnStart.IsChecked == true)
            {
                return MediaSelectionMode.OnStart;
            }

            return MediaSelectionMode.Default;
        }

        private static void ApplyCoverMode(BackgroundChangerSettings settings, MediaSelectionMode mode, bool timerRandom)
        {
            settings.EnableCoverImageAutoChanger = mode == MediaSelectionMode.Timer;
            settings.EnableCoverImageRandomOnStart = mode == MediaSelectionMode.OnStart;
            settings.EnableCoverImageRandomOnSelect = mode == MediaSelectionMode.OnSelect;

            if (mode == MediaSelectionMode.Timer)
            {
                settings.EnableCoverImageRandomSelect = timerRandom;
            }
            else if (mode == MediaSelectionMode.OnStart || mode == MediaSelectionMode.OnSelect)
            {
                settings.EnableCoverImageRandomSelect = true;
            }
            else
            {
                settings.EnableCoverImageRandomSelect = false;
            }
        }

        /// <summary>
        /// Maps icon UI mode to settings flags. Icons expose Default and OnStart only.
        /// </summary>
        /// <remarks>
        /// <see cref="BackgroundChangerSettings.EnableIconImageAutoChanger"/> is always cleared: Timer would require
        /// excluding <c>PluginIconImage</c> from Details list template (one instance per visible row).
        /// </remarks>
        private static void ApplyIconMode(BackgroundChangerSettings settings, MediaSelectionMode mode)
        {
            settings.EnableIconImageAutoChanger = false;
            settings.EnableIconImageRandomOnStart = mode == MediaSelectionMode.OnStart;
            settings.EnableIconImageRandomOnSelect = false;
            settings.EnableIconImageRandomSelect = mode == MediaSelectionMode.OnStart;
        }
    }
}
