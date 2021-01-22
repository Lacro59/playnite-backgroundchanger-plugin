using Playnite.SDK;
using System;
using System.Collections.Generic;
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


        public BackgroundChangerSettingsView()
        {
            InitializeComponent();

            HwSlider_ValueChanged(hwSlider, null);
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
    }
}