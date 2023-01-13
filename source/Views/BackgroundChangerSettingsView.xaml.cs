using BackgroundChanger.Services;
using CommonPlayniteShared.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanger.Views
{
    public partial class BackgroundChangerSettingsView : UserControl
    {
        private static IResourceProvider resources = new ResourceProvider();
        private IPlayniteAPI PlayniteApi;

        private BackgroundChangerDatabase PluginDatabase = BackgroundChanger.PluginDatabase;

        public BackgroundChangerSettingsView(IPlayniteAPI PlayniteApi)
        {
            this.PlayniteApi = PlayniteApi;

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
                labelBcIntervalLabel_text.Content = "(" + slider.Value + " " + resources.GetString("LOCBcSeconds") + ")";
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
                labelIntervalLabel_text.Content = "(" + slider.Value + " " + resources.GetString("LOCBcSeconds") + ")";
            }
        }


        private void ButtonFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            string SelectedFile = PlayniteApi.Dialogs.SelectFile("File|ffmpeg.exe");

            if (!SelectedFile.IsNullOrEmpty())
            {
                PART_FfmpegFile.Text = SelectedFile;
                ((BackgroundChangerSettingsViewModel)this.DataContext).Settings.ffmpegFile = SelectedFile;
            }
        }


        private void ButtonWebpinfo_Click(object sender, RoutedEventArgs e)
        {
            string SelectedFile = PlayniteApi.Dialogs.SelectFile("File|webpinfo.exe");

            if (!SelectedFile.IsNullOrEmpty())
            {
                string destFileName = System.IO.Path.Combine(PluginDatabase.Paths.PluginPath, "webpinfo.exe");

                FileSystem.CopyFile(SelectedFile, destFileName, true);

                PART_WebpinfoFile.Text = destFileName;
                ((BackgroundChangerSettingsViewModel)this.DataContext).Settings.webpinfoFile = destFileName;
            }
        }

        private void hwBcVideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == null)
            {
                return;
            }

            Slider slider = sender as Slider;
            if (labelBcVideoIntervalLabel_text?.Content != null)
            {
                labelBcVideoIntervalLabel_text.Content = "(" + slider.Value + " " + resources.GetString("LOCBcSeconds") + ")";
            }
        }

        private void hwVideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender == null)
            {
                return;
            }

            Slider slider = sender as Slider;
            if (labelVideoIntervalLabel_text?.Content != null)
            {
                labelVideoIntervalLabel_text.Content = "(" + slider.Value + " " + resources.GetString("LOCBcSeconds") + ")";
            }
        }
    }
}
