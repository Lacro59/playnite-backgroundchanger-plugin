using BackgroundChanger.Services;
using Playnite.SDK;
using System;
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

            rbBackgroundOnSelect.IsChecked = PluginDatabase.PluginSettings.Settings.EnableBackgroundImageRandomOnSelect;
            rbBackgroundOnStart.IsChecked = PluginDatabase.PluginSettings.Settings.EnableBackgroundImageRandomOnStart;
            rbCoverOnSelect.IsChecked = PluginDatabase.PluginSettings.Settings.EnableCoverImageRandomOnSelect;
            rbCoverOnStart.IsChecked = PluginDatabase.PluginSettings.Settings.EnableCoverImageRandomOnStart;

            rb_Click(null, null);
        }

        private void ButtonFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = API.Instance.Dialogs.SelectFile("File|ffmpeg.exe");
            if (!selectedFile.IsNullOrEmpty())
            {
                PART_FfmpegFile.Text = selectedFile;
                ((BackgroundChangerSettingsViewModel)DataContext).Settings.ffmpegFile = selectedFile;
            }
        }

        private void rb_Click(object sender, RoutedEventArgs e)
        {
            BackgroundOnSelect = (bool)rbBackgroundOnSelect.IsChecked;
            BackgroundOnStart = (bool)rbBackgroundOnStart.IsChecked;
            CoverOnSelect = (bool)rbCoverOnSelect.IsChecked;
            CoverOnStart = (bool)rbCoverOnStart.IsChecked;
        }

        //private void ButtonWebpinfo_Click(object sender, RoutedEventArgs e)
        //{
        //    string selectedFile = API.Instance.Dialogs.SelectFile("File|webpinfo.exe");
        //    if (!selectedFile.IsNullOrEmpty())
        //    {
        //        PART_WebpinfoFile.Text = selectedFile;
        //        ((BackgroundChangerSettingsViewModel)DataContext).Settings.webpinfoFile = selectedFile;
        //    }
        //}
    }
}