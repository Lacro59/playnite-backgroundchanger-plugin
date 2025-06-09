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

            HwBcSlider_ValueChanged(hwSlider, null);
            HwSlider_ValueChanged(hwSlider, null);
        }


        private void HwBcSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == null)
            {
                return;
            }

            Slider slider = sender as Slider;
            if (labelBcIntervalLabel_text?.Content != null)
            {
                labelBcIntervalLabel_text.Content = "(" + slider.Value + " " + ResourceProvider.GetString("LOCBcSeconds") + ")";
            }
        }

        private void HwSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == null)
            {
                return;
            }

            Slider slider = sender as Slider;
            if (labelIntervalLabel_text?.Content != null)
            {
                labelIntervalLabel_text.Content = "(" + slider.Value + " " + ResourceProvider.GetString("LOCBcSeconds") + ")";
            }
        }


        private void ButtonFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = API.Instance.Dialogs.SelectFile("File|ffmpeg.exe");
            if (!selectedFile.IsNullOrEmpty())
            {
                PART_FfmpegFile.Text = selectedFile;
                ((BackgroundChangerSettingsViewModel)this.DataContext).Settings.ffmpegFile = selectedFile;
            }
        }


        private void ButtonWebpinfo_Click(object sender, RoutedEventArgs e)
        {
            string selectedFile = API.Instance.Dialogs.SelectFile("File|webpinfo.exe");
            if (!selectedFile.IsNullOrEmpty())
            {
                PART_WebpinfoFile.Text = selectedFile;
                ((BackgroundChangerSettingsViewModel)this.DataContext).Settings.webpinfoFile = selectedFile;
            }
        }

        private void HwBcVideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == null)
            {
                return;
            }

            Slider slider = sender as Slider;
            if (labelBcVideoIntervalLabel_text?.Content != null)
            {
                labelBcVideoIntervalLabel_text.Content = "(" + slider.Value + " " + ResourceProvider.GetString("LOCBcSeconds") + ")";
            }
        }

        private void HwVideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == null)
            {
                return;
            }

            Slider slider = sender as Slider;
            if (labelVideoIntervalLabel_text?.Content != null)
            {
                labelVideoIntervalLabel_text.Content = "(" + slider.Value + " " + ResourceProvider.GetString("LOCBcSeconds") + ")";
            }
        }
    }
}
