using APNG;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace wpf_animatedimage.Controls
{
    /// <summary>
    /// Logique d'interaction pour AnimatedImage.xaml
    /// </summary>
    public partial class AnimatedImage : Image
    {
        public bool HasError = false;

        private string FileName;

        // APng
        private Dictionary<fcTL, MemoryStream> m_Apng;

        // WebP
        private WebpAnim webPAnim;

        private Stream stream;
        private BitmapSource bitmapSource;
        private BitmapImage bitmapImage;

        private DispatcherTimer Timer;
        private int DelayDefault = 50;
        private int Delay = 0;
        private int ActualFrame = 0;

        public bool IsLoaded = false;


        #region Properties
        public bool UseBitmapImage
        {
            get { return (bool)GetValue(UseBitmapImageProperty); }
            set { SetValue(UseBitmapImageProperty, value); }
        }

        public static readonly DependencyProperty UseBitmapImageProperty = DependencyProperty.Register(
            nameof(UseBitmapImage),
            typeof(bool),
            typeof(AnimatedImage),
            new PropertyMetadata(true));


        public bool UseAnimated
        {
            get { return (bool)GetValue(UseAnimatedProperty); }
            set { SetValue(UseAnimatedProperty, value); }
        }

        public static readonly DependencyProperty UseAnimatedProperty = DependencyProperty.Register(
            nameof(UseAnimated),
            typeof(bool),
            typeof(AnimatedImage),
            new PropertyMetadata(false));


        public int DecodePixelWidth
        {
            get { return (int)GetValue(DecodePixelWidthProperty); }
            set { SetValue(DecodePixelWidthProperty, value); }
        }

        public static readonly DependencyProperty DecodePixelWidthProperty = DependencyProperty.Register(
            nameof(DecodePixelWidth),
            typeof(int),
            typeof(AnimatedImage),
            new PropertyMetadata(0));


        public new object Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public new static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(AnimatedImage),
            new PropertyMetadata(null, SourceChanged));
        #endregion


        public AnimatedImage()
        {
            InitializeComponent();
        }


        private static void SourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var control = (AnimatedImage)obj;
            control.LoadNewSource(args.NewValue, args.OldValue);
        }

        private async void LoadNewSource(object NewSource, object OldSource)
        {
            if (NewSource?.Equals(OldSource) == true)
            {
                return;
            }

            Image_Unloaded(null, null);

            if (NewSource != null && NewSource is string)
            {
                FileName = (string)NewSource;

                if (!File.Exists(FileName))
                {
                    base.Source = null;
                    return;
                }

                try
                {
                    int DecodePixelWidth = this.DecodePixelWidth;
                    BitmapImage bitmapImage = await Task.Factory.StartNew(() =>
                    {
                        if (NewSource is string str)
                        {
                            using (var fStream = OpenReadFileStreamSafe((string)NewSource))
                            {
                                fStream.Seek(0, SeekOrigin.Begin);
                                var image = new BitmapImage();
                                image.BeginInit();
                                image.StreamSource = fStream;

                                if (DecodePixelWidth != 0)
                                {
                                    image.DecodePixelWidth = DecodePixelWidth;
                                }
                                else
                                {
                                    image.DecodePixelWidth = (int)this.ActualWidth;
                                }

                                image.CacheOption = BitmapCacheOption.OnLoad;
                                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                image.EndInit();
                                image.Freeze();

                                return image;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    });
                    base.Source = bitmapImage;
                }
                catch (Exception ex)
                {
                    base.Source = null;
                }

                if (UseAnimated)
                {
                    if (System.IO.Path.GetExtension(FileName).ToLower().IndexOf("png") > -1)
                    {
                        CPng_Reader pngr;
                        try
                        {
                            var TaskPng = Task.Run(() =>
                            {
                                pngr = new CPng_Reader();
                                using (var fStream = OpenReadFileStreamSafe(FileName))
                                {
                                    m_Apng = pngr.Open(fStream).SpltAPng();
                                }

                                // Animated
                                if (m_Apng != null && m_Apng.Count != 0)
                                {
                                    Delay = m_Apng.FirstOrDefault().Key.Delay_Den;

                                    this.Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        Timer = new DispatcherTimer(DispatcherPriority.Render);
                                        if (Delay > 0)
                                        {
                                            Timer.Interval = TimeSpan.FromMilliseconds(Delay);
                                        }
                                        else
                                        {
                                            Timer.Interval = TimeSpan.FromMilliseconds(DelayDefault);
                                        }

                                        Timer.Tick += TimerTickAPng;
                                        Timer.Start();
                                    });
                                }
                                else
                                {
                                    m_Apng = null;
                                }

                                pngr = null;
                                IsLoaded = true;
                            });
                        }
                        catch (Exception ex)
                        {
                            pngr = null;
                            m_Apng = null;
                        }
                    }
                    else if (System.IO.Path.GetExtension(FileName).ToLower().IndexOf("webp") > -1)
                    {
                        var TaskWebP = Task.Run(() =>
                        {
                            webPAnim = new WebpAnim();
                            webPAnim.Load(FileName);

                            // Animated
                            if (webPAnim.FramesDuration() != 0)
                            {
                                Delay = webPAnim.FramesDuration();

                                this.Dispatcher.BeginInvoke((Action)delegate
                                {
                                    Timer = new DispatcherTimer(DispatcherPriority.Render);
                                    Timer.Interval = TimeSpan.FromMilliseconds(webPAnim.FramesDuration());
                                    Timer.Tick += TimerTickWebp;
                                    Timer.Start();
                                });
                            }
                            else
                            {
                                Stream stream = webPAnim.GetStream();
                                if (stream != null)
                                {
                                    BitmapImage bitmapImage = new BitmapImage();
                                    bitmapImage = new BitmapImage();
                                    bitmapImage.BeginInit();
                                    bitmapImage.StreamSource = stream;
                                    bitmapImage.DecodePixelWidth = (int)this.ActualWidth;
                                    bitmapImage.EndInit();

                                    this.Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        base.Source = bitmapImage;
                                    });
                                }
                                else
                                {
                                    this.Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        base.Source = null;
                                    });
                                }
                            }

                            IsLoaded = true;
                        });
                    }
                    else
                    {
                        IsLoaded = true;
                    }
                }
            }
            else
            {
                base.Source = null;
            }
        }


        public AnimetedImageInfos GetInfos()
        {
            System.Threading.SpinWait.SpinUntil(() => IsLoaded, -1);

            AnimetedImageInfos animetedImageInfos = new AnimetedImageInfos();

            if (FileName != null && FileName != string.Empty)
            {
                FileInfo info = new FileInfo(FileName);

                animetedImageInfos.Name = info.Name;
                animetedImageInfos.Size = info.Length;

                if (webPAnim != null)
                {
                    animetedImageInfos.Frames = webPAnim.FramesCount();
                    animetedImageInfos.Delay = Delay;
                }
                if (m_Apng != null)
                {
                    animetedImageInfos.Frames = m_Apng.Count;
                    animetedImageInfos.Delay = Delay;
                }
            }

            return animetedImageInfos;
        }


        #region Timer
        private void TimerTickWebp(object sender, EventArgs e)
        {
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("-TimerTickWebp-------------------------");
                System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
#endif
                if (UseBitmapImage)
                {
                    stream = webPAnim.GetFrameStream(ActualFrame);

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//30ms
#endif

                    bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    if (DecodePixelWidth != 0)
                    {
                        bitmapImage.DecodePixelWidth = DecodePixelWidth;
                    }
                    else
                    {
                        bitmapImage.DecodePixelWidth = (int)this.ActualWidth;
                    }
                    bitmapImage.EndInit();

                    base.Source = bitmapImage;
                }
                else
                {
                    bitmapSource = webPAnim.GetFrameBitmapSource(ActualFrame);

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//3ms
#endif

                    base.Source = bitmapSource;
                }
#if DEBUG
                System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//1ms
                System.Diagnostics.Debug.WriteLine("-TimerTickWebp-END---------------------");
#endif

                ActualFrame++;
                if (ActualFrame >= webPAnim.FramesCount())
                {
                    ActualFrame = 0;
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                Timer.Stop();
            }
        }

        private async void TimerTickAPng(object sender, EventArgs e)
        {
            try
            {
                if (m_Apng != null)
                {
                    try
                    {
                        int DecodePixelWidth = this.DecodePixelWidth;

                        fcTL fctl = this.m_Apng.ElementAt(ActualFrame).Key;
                        var drawingVisual = new DrawingVisual();
                        using (DrawingContext dc = drawingVisual.RenderOpen())
                        {
                            stream = m_Apng.ElementAt(ActualFrame).Value;
                            stream.Position = 0;

                            BitmapImage img = new BitmapImage();
                            img.BeginInit();
                            img.StreamSource = stream;
                            if (DecodePixelWidth != 0)
                            {
                                img.DecodePixelWidth = DecodePixelWidth;
                            }
                            else
                            {
                                img.DecodePixelWidth = (int)this.ActualWidth;
                            }
                            img.EndInit();
                            img.Freeze();
                            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, img.Width, img.Height));
                            dc.DrawImage(img, new Rect(fctl.X_Offset, fctl.Y_Offset, img.Width, img.Height));
                        }
                        RenderTargetBitmap rtb = new RenderTargetBitmap((int)drawingVisual.ContentBounds.Width, (int)drawingVisual.ContentBounds.Height, 96, 96, PixelFormats.Pbgra32);
                        rtb.Render(drawingVisual);
                        base.Source = rtb;
                    }
                    catch (Exception ex)
                    {
                        base.Source = null;
                    }

                    ActualFrame++;
                    if (ActualFrame >= this.m_Apng.Count)
                    {
                        ActualFrame = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                Timer.Stop();
            }
        }
        #endregion


        private void Image_Unloaded(object sender, RoutedEventArgs e)
        {
            IsLoaded = false;

            HasError = false;
            Delay = 0;
            ActualFrame = 0;

            m_Apng = null;
            if (webPAnim != null)
            {
                webPAnim.Dispose();
                webPAnim = null;
            }

            if (Timer != null)
            {
                Timer.Stop();
                Timer = null;
            }

            bitmapImage = null;

            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }

            GC.Collect();
        }


        #region Activate/Deactivated animation
        private void Image_Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Activated += Application_Activated;
            Application.Current.Deactivated += Application_Deactivated;
        }

        private void Application_Deactivated(object sender, EventArgs e)
        {
            if (Timer != null)
            {
                Timer.Stop();
            }
        }

        private void Application_Activated(object sender, EventArgs e)
        {
            if (Timer != null && !HasError)
            {
                Timer.Start();
            }
        }
        #endregion





        public Stream OpenReadFileStreamSafe(string path, int retryAttempts = 5)
        {
            IOException ioException = null;
            for (int i = 0; i < retryAttempts; i++)
            {
                try
                {
                    return new FileStream(path, FileMode.Open, FileAccess.Read);
                }
                catch (IOException exc)
                {
                    ioException = exc;
                    Task.Delay(500).Wait();
                }
            }

            throw new IOException($"Failed to read {path}", ioException);
        }
    }


    public class AnimetedImageInfos
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public int Frames { get; set; }
        public int Delay { get; set; }
    }
}
