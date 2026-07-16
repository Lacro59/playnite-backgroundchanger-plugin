using BackgroundChanger.Services;
using Playnite.SDK;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanger.Views
{
    public partial class BackgroundChangerSettingsView : UserControl
    {
        public static bool BackgroundOnSelect { get; set; }
        public static bool BackgroundOnStart { get; set; }
        public static bool CoverOnSelect { get; set; }
        public static bool CoverOnStart { get; set; }


        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        private BackgroundChangerSettings EditingSettings =>
            DataContext is BackgroundChangerSettingsViewModel viewModel ? viewModel.Settings : PluginDatabase.PluginSettings;

        public BackgroundChangerSettingsView()
        {
            InitializeComponent();

            rbBackgroundOnSelect.IsChecked = PluginDatabase.PluginSettings.EnableBackgroundImageRandomOnSelect;
            rbBackgroundOnStart.IsChecked = PluginDatabase.PluginSettings.EnableBackgroundImageRandomOnStart;
            rbCoverOnSelect.IsChecked = PluginDatabase.PluginSettings.EnableCoverImageRandomOnSelect;
            rbCoverOnStart.IsChecked = PluginDatabase.PluginSettings.EnableCoverImageRandomOnStart;

            bool legacyIconOnSelect = PluginDatabase.PluginSettings.EnableIconImageRandomOnSelect
                && !PluginDatabase.PluginSettings.EnableIconImageRandomOnStart
                && !PluginDatabase.PluginSettings.EnableIconImageAutoChanger;
            if (legacyIconOnSelect)
            {
                PluginDatabase.PluginSettings.EnableIconImageRandomOnStart = true;
            }

            Loaded += OnSettingsViewLoaded;
            Rb_Click(null, null);
        }

        private void OnSettingsViewLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnSettingsViewLoaded;
            CoerceIconRandomMode();
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

        private void Rb_Click(object sender, RoutedEventArgs e)
        {
            BackgroundOnSelect = (bool)rbBackgroundOnSelect.IsChecked;
            BackgroundOnStart = (bool)rbBackgroundOnStart.IsChecked;
            CoverOnSelect = (bool)rbCoverOnSelect.IsChecked;
            CoverOnStart = (bool)rbCoverOnStart.IsChecked;
        }

        private void RbIconOnStart_Click(object sender, RoutedEventArgs e)
        {
            if (rbIconOnStart.IsChecked == true)
            {
                EditingSettings.EnableIconImageAutoChanger = false;
            }

            CoerceIconRandomMode();
        }

        private void CbIconAutoChanger_Checked(object sender, RoutedEventArgs e)
        {
            EditingSettings.EnableIconImageRandomOnStart = false;
            CoerceIconRandomMode();
        }

        private void CbIconAutoChanger_Unchecked(object sender, RoutedEventArgs e)
        {
            if (cbIconRandom.IsChecked == true)
            {
                EditingSettings.EnableIconImageRandomOnStart = true;
            }

            CoerceIconRandomMode();
        }

        /// <summary>
        /// Enforces OnStart xor Timer for icon random mode in the settings UI.
        /// </summary>
        private void CoerceIconRandomMode()
        {
            BackgroundChangerSettings settings = EditingSettings;
            if (settings == null)
            {
                return;
            }

            if (settings.EnableIconImageAutoChanger)
            {
                settings.EnableIconImageRandomOnStart = false;
            }
            else if (cbIconRandom.IsChecked == true)
            {
                settings.EnableIconImageRandomOnStart = true;
            }
        }
    }
}