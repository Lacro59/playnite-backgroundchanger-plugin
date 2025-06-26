using BackgroundChanger.Services;
using Playnite.SDK;
using System;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanger.Views
{
    public partial class BackgroundChangerSettingsView : UserControl
    {
        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        public BackgroundChangerSettingsView()
        {
            InitializeComponent();
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