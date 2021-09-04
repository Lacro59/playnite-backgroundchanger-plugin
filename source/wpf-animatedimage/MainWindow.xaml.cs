using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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

namespace wpf_animatedimage
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string FileName = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.ShowDialog();

            FileName = ofd.FileName;

            if (FileName != null && FileName != string.Empty)
            {
                string fname = ofd.FileName;
                PART_AnimatedImage.Source = fname;

                PART_Name.Content = string.Empty;
                PART_Size.Content = string.Empty;
                PART_Frames.Content = string.Empty;
                PART_Delay.Content = string.Empty;

                Task.Run(() =>
                {
                    var info = PART_AnimatedImage.GetInfos();

                    this.Dispatcher.BeginInvoke((Action)delegate
                    {
                        PART_Name.Content = info.Name;
                        PART_Size.Content = info.Size;
                        PART_Frames.Content = info.Frames;
                        PART_Delay.Content = info.Delay;
                    });
                });
            }
        }


        private void ButtonExtract_Click(object sender, RoutedEventArgs e)
        {
            //ffmpeg -r 25 -f image2 -s 1920x620 -i "D:\Test\myImage%1d.png" -vcodec libx264 -crf 25 -pix_fmt yuv420p test.mp4
            if (FileName != null && FileName != string.Empty)
            {
                if (System.IO.Path.GetExtension(FileName).ToLower().IndexOf("webp") > -1)
                {
                    WebpAnim webPAnim = new WebpAnim();
                    webPAnim = new WebpAnim();
                    webPAnim.Load(FileName);

                    int ActualFrame = 0;
                    while (ActualFrame < webPAnim.FramesCount())
                    {
                        System.Drawing.Image img = System.Drawing.Image.FromStream(webPAnim.GetFrameStream(ActualFrame));
                        img.Save($"D:\\Test\\myImage{ActualFrame}.png", ImageFormat.Png);

                        ActualFrame++;
                    }
                }
            }
        }
    }
}
