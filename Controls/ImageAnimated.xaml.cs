using APNG;
using CommonPluginsShared;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
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
using System.Windows.Threading;

namespace BackgroundChanger2.Controls
{
    /// <summary>
    /// Logique d'interaction pour ImageAnimated.xaml
    /// </summary>
    public partial class ImageAnimated : Image
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private Dictionary<fcTL, MemoryStream> m_Apng = new Dictionary<fcTL, MemoryStream>();
        private Dictionary<int, BitmapImage> m_Bmps = new Dictionary<int, BitmapImage>();
        private DispatcherTimer timer;

        private int timeSpan = 40;
        private int delay = 0;
        private int index = 0;
        private object currentSource = null;


        public new static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(ImageAnimated),
            new PropertyMetadata(null, SourceChanged));

        public new object Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty ImgWidthProperty = DependencyProperty.Register(
            nameof(ImgWidth),
            typeof(int),
            typeof(ImageAnimated),
            new PropertyMetadata(200));

        private int _ImgWidth = 200;
        public int ImgWidth
        {
            get
            {
                try
                {
                    int value = (int)GetValue(ImgWidthProperty);
                    return (int)GetValue(ImgWidthProperty); ;
                }
                catch
                {
                    return 200;
                }
            }
            set
            {
                _ImgWidth = value;
                SetValue(ImgWidthProperty, value);
            }
        }

        public static readonly DependencyProperty UseOpacityMaskProperty = DependencyProperty.Register(
            nameof(UseOpacityMask),
            typeof(bool),
            typeof(ImageAnimated),
            new PropertyMetadata(false, UseOpacityMaskChanged));

        public bool UseOpacityMask
        {
            get
            {
                return (bool)GetValue(UseOpacityMaskProperty);
            }
            set
            {
                SetValue(UseOpacityMaskProperty, value);
            }
        }



        public ImageAnimated()
        {
            InitializeComponent();
        }


        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var control = (ImageAnimated)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private static void UseOpacityMaskChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var control = (ImageAnimated)obj;
            control.DataContext = new
            {
                UseOpacityMask = (bool)args.NewValue
            };
        }


        private async void LoadNewSource(object newSource, object oldSource)
        { 
            if (newSource?.Equals(currentSource) == true)
            {
                return;
            }

            BitmapImage image = null;
            currentSource = newSource;

            m_Apng = null;
            m_Bmps = null;
            m_Bmps = new Dictionary<int, BitmapImage>();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            if (newSource != null)
            {
                image = await Task.Factory.StartNew(() =>
                {
                    if (newSource is string str)
                    {
                        if (!File.Exists(str))
                        {
                            return null;
                        }

                        if (System.IO.Path.GetExtension(str).ToLower().IndexOf("png") > -1)
                        {
                            Task.Run(() =>
                            {
                                CPng_Reader pngr;
                                try
                                {
                                    pngr = new CPng_Reader();
                                    m_Apng = pngr.Open(File.OpenRead(str)).SpltAPng();

                                    delay = pngr.delay;

                                    if (pngr.Chunks.Count != 0)
                                    {
                                        this.Dispatcher.BeginInvoke((Action)delegate
                                        {
                                            index = 0;
                                            timer = new DispatcherTimer();

                                            if (delay > 0)
                                            {
                                                timer.Interval = TimeSpan.FromMilliseconds(timeSpan * delay);
                                            }
                                            else
                                            {
                                                timer.Interval = TimeSpan.FromMilliseconds(timeSpan);
                                            }
                                            timer.Tick += Timer_Tick;
                                            timer.Start();
                                        });
                                    }
                                    else
                                    {
                                        m_Apng = null;
                                    }

                                    pngr = null;
                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, "BackgroundChanger");

                                    pngr = null;
                                    m_Apng = null;
                                }
                            });
                        }

                        return BitmapExtensions.BitmapFromFile(str, new BitmapLoadProperties(_ImgWidth, 0));
                    }
                    else
                    {
                        return null;
                    }
                });
            }

            base.Source = image;
        }


        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (m_Apng != null)
                {
                    using (Stream stream = m_Apng.ElementAt(index).Value)
                    {
                        if (m_Bmps.ContainsKey(index) == false)
                        {
                            stream.Position = 0;
                            BitmapImage bmp = BitmapExtensions.BitmapFromStream(stream, new BitmapLoadProperties(_ImgWidth, 0));

                            if (bmp != null)
                            {
                                m_Bmps.Add(index, bmp);
                            }
                        }
                    }

                    if (m_Apng.Count == m_Bmps.Count)
                    {
                        m_Apng = null;
                    }
                }

                int MaxCount = 0;
                if (m_Apng == null)
                {
                    MaxCount = m_Bmps.Count;
                }
                else
                {
                    MaxCount = m_Apng.Count;
                }

                if (index < m_Bmps.Count)
                {
                    base.Source = m_Bmps[index];
                }

                index = index + 1;
                if (index >= MaxCount)
                {
                    index = 0;
                }
            }
            catch
            {

            }
        }


        private void Image_Unloaded(object sender, RoutedEventArgs e)
        {
            m_Apng = null;
            m_Bmps = null;

            currentSource = null;

            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }

        private void Image_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = Tools.FindParent<Window>(this);
            window.Activated += Window_Activated;
            window.Deactivated += Window_Deactivated;
        }


        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (timer != null)
            {
                timer.Stop();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (timer != null)
            {
                timer.Start();
            }
        }
    }
}
