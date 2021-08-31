using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BackgroundChanger.Views
{
    public partial class BackgroundChangerSettingsView : UserControl
    {
        private static IResourceProvider resources = new ResourceProvider();
        private IPlayniteAPI PlayniteApi;


        public BackgroundChangerSettingsView(IPlayniteAPI PlayniteApi)
        {
            this.PlayniteApi = PlayniteApi;

            InitializeComponent();

            HwBcSlider_ValueChanged(hwSlider, null);
            HwSlider_ValueChanged(hwSlider, null);
        }


        private void HwBcSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider;

            try
            {
                labelBcIntervalLabel_text.Content = "(" + slider.Value + " " + resources.GetString("LOCBcSeconds") + ")";
            }
            catch
            {
            }
        }

        private void HwSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider;

            try
            {
                labelIntervalLabel_text.Content = "(" + slider.Value + " " + resources.GetString("LOCBcSeconds") + ")";
            }
            catch
            {
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Process.Start((string)((FrameworkElement)sender).Tag);
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
    }
}