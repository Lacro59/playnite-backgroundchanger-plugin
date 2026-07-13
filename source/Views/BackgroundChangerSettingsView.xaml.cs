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

        public BackgroundChangerSettingsView()
        {
            InitializeComponent();

            rbBackgroundOnSelect.IsChecked = PluginDatabase.PluginSettings.EnableBackgroundImageRandomOnSelect;
            rbBackgroundOnStart.IsChecked = PluginDatabase.PluginSettings.EnableBackgroundImageRandomOnStart;
            rbCoverOnSelect.IsChecked = PluginDatabase.PluginSettings.EnableCoverImageRandomOnSelect;
            rbCoverOnStart.IsChecked = PluginDatabase.PluginSettings.EnableCoverImageRandomOnStart;

            Rb_Click(null, null);
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
    }
}