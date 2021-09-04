//#define TestD3DImage
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Resources;
using APNG;
using System.Windows.Threading;
using System.Windows.Interop;
using SharpDX.Direct3D9;
using System.Runtime.InteropServices;
using SharpDX;
using System.Linq;
using APNG.Tool;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Threading;
using System.IO.Compression;

namespace WPF_APNG
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        Dictionary<fcTL, MemoryStream> m_Apng = new Dictionary<fcTL, MemoryStream>();
        Dictionary<fcTL, byte[]> m_Raws = new Dictionary<fcTL, byte[]>();
#if TestD3DImage
        CD3DImage m_D3DImage = new CD3DImage();
#endif
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            //return;

            StreamResourceInfo sri = Application.GetResourceStream(new Uri("pack://application:,,,/apng_spinfox.png", UriKind.Absolute));
            StreamResourceInfo elephant = Application.GetResourceStream(new Uri("pack://application:,,,/elephant.png", UriKind.Absolute));
            CPng_Reader pngr = new CPng_Reader();
            this.m_Apng = pngr.Open(sri.Stream).SpltAPng();

            //IHDR ihdr = pngr.IHDR;
            //var drawingVisual = new DrawingVisual();
            //using (DrawingContext dc = drawingVisual.RenderOpen())
            //{
            //    double x = 0;
            //    for (int i = 0; i < this.m_Apng.Count; i++)
            //    {
            //        fcTL fctl = this.m_Apng.ElementAt(i).Key;
            //        BitmapImage img = new BitmapImage();
            //        img.BeginInit();
            //        img.StreamSource = this.m_Apng.ElementAt(i).Value;
            //        img.EndInit();
            //        img.Freeze();
            //        //if(fctl.X_Offset > 0)
            //        //{
            //        //    dc.DrawRectangle(Brushes.Black, null, new Rect(x, 0, fctl.X_Offset, ihdr.Height));
            //        //}
            //        dc.DrawRectangle(Brushes.Transparent, null, new Rect(x, 0, ihdr.Width, ihdr.Height));
            //        dc.DrawImage(img, new Rect(x+ fctl.X_Offset, fctl.Y_Offset, img.Width, img.Height));
            //        x = x + ihdr.Width;
            //    }
            //}

            //_checkStoryboard = new Storyboard();

            //var keyFrames = new ThicknessAnimationUsingKeyFrames();
            ////keyFrames.AutoReverse = true;
            //Storyboard.SetTarget(keyFrames, sender as Image);
            //Storyboard.SetTargetProperty(keyFrames, new PropertyPath("Margin"));
            //TimeSpan start = TimeSpan.Zero;
            ////keyFrames.Duration = TimeSpan.FromSeconds(25);
            //for (var i = 0; i < this.m_Apng.Count; i++)
            //{
            //    var keyFrame = new DiscreteThicknessKeyFrame
            //    {
            //        //KeyTime = TimeSpan.FromSeconds((i + 1d) / 28d),
            //        KeyTime = TimeSpan.FromSeconds(i*0.04),
            //        Value = new Thickness(-(i + 1) * 480, 0, 0, 0)
            //    };
            //    keyFrames.KeyFrames.Add(keyFrame);
            //}
            //_checkStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            //_checkStoryboard.Children.Add(keyFrames);

            var storyboard = new Storyboard();
            var keyFrames = new ObjectAnimationUsingKeyFrames();
            Storyboard.SetTarget(keyFrames, this.image_png);
            Storyboard.SetTargetProperty(keyFrames, new PropertyPath("Source"));
            TimeSpan start = TimeSpan.Zero;
            IHDR ihdr = pngr.IHDR;
            fcTL fctl_prev = null;
            for (int i = 0; i < this.m_Apng.Count; i++)
            {
                fcTL fctl = this.m_Apng.ElementAt(i).Key;
                var drawingVisual = new DrawingVisual();
                using (DrawingContext dc = drawingVisual.RenderOpen())
                {
                    
                    BitmapImage img = new BitmapImage();
                    img.BeginInit();
                    img.StreamSource = this.m_Apng.ElementAt(i).Value;
                    img.EndInit();
                    img.Freeze();
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ihdr.Width, ihdr.Height));
                    dc.DrawImage(img, new Rect(fctl.X_Offset, fctl.Y_Offset, img.Width, img.Height));
                }
                RenderTargetBitmap rtb = new RenderTargetBitmap((int)drawingVisual.ContentBounds.Width, (int)drawingVisual.ContentBounds.Height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);
                if (fctl_prev != null)
                {
                    var dddd = TimeSpan.FromMilliseconds((double)(fctl_prev.Delay_Num) / fctl_prev.Delay_Den);
                    start = start + TimeSpan.FromSeconds((double)(fctl_prev.Delay_Num) / fctl_prev.Delay_Den);
                }
                else
                {
                    fctl_prev = fctl;
                }
                var keyFrame = new DiscreteObjectKeyFrame
                {
                    //KeyTime = TimeSpan.FromSeconds(i * 0.04),
                    KeyTime = start,
                    Value = rtb
                };
                
                keyFrames.KeyFrames.Add(keyFrame);

                //// Encoding the RenderBitmapTarget as a PNG file.
                //PngBitmapEncoder png = new PngBitmapEncoder();
                //png.Frames.Add(BitmapFrame.Create(rtb));
                //using (Stream stm = File.Create($"{ this.m_Apng.ElementAt(i).Key.SequenceNumber}.png"))
                //{
                //    png.Save(stm);
                //}
                //File.WriteAllBytes($"{this.m_Apng.ElementAt(i).Key.SequenceNumber}.png", this.m_Apng.ElementAt(i).Value.ToArray());
            }
            storyboard.RepeatBehavior = RepeatBehavior.Forever;
            storyboard.Children.Add(keyFrames);
            storyboard.Begin();

            //IHDR ihdr = pngr.IHDR;
            //var drawingVisual = new DrawingVisual();
            //using (DrawingContext dc = drawingVisual.RenderOpen())
            //{
            //    double x = 0;
            //    for (int i = 0; i < this.m_Apng.Count; i++)
            //    {
            //        fcTL fctl = this.m_Apng.ElementAt(i).Key;
            //        BitmapImage img = new BitmapImage();
            //        img.BeginInit();
            //        img.StreamSource = this.m_Apng.ElementAt(i).Value;
            //        img.EndInit();
            //        img.Freeze();
            //        //if(fctl.X_Offset > 0)
            //        //{
            //        //    dc.DrawRectangle(Brushes.Black, null, new Rect(x, 0, fctl.X_Offset, ihdr.Height));
            //        //}
            //        dc.DrawRectangle(Brushes.Transparent, null, new Rect(x, 0, ihdr.Width, ihdr.Height));
            //        dc.DrawImage(img, new Rect(x+ fctl.X_Offset, fctl.Y_Offset, img.Width, img.Height));
            //        x = x + ihdr.Width;
            //    }
            //}

            //RenderTargetBitmap rtb = new RenderTargetBitmap((int)drawingVisual.ContentBounds.Width, (int)drawingVisual.ContentBounds.Height, 96, 96, PixelFormats.Pbgra32);
            //rtb.Render(drawingVisual);

            //// Encoding the RenderBitmapTarget as a PNG file.
            //PngBitmapEncoder png = new PngBitmapEncoder();
            //png.Frames.Add(BitmapFrame.Create(rtb));
            //using (Stream stm = File.Create("new.png"))
            //{
            //    png.Save(stm);
            //}

            //this.image_png.Source = rtb;

#if TestD3DImage
                        IHDR ihdr = pngr.Chunks.FirstOrDefault(x => x.ChunkType == ChunkTypes.IHDR) as IHDR;
                        this.m_D3DImage.Open(ihdr.Width, ihdr.Height);
                        this.img.Source = this.m_D3DImage;
#endif


            //DispatcherTimer timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromMilliseconds(5);
            //timer.Tick += Timer_Tick;
            //timer.Start();





        }





        int index = 0;
#if TestD3DImage
        Dictionary<int, Tuple<WriteableBitmap, int>> m_Bmps = new Dictionary<int, Tuple<WriteableBitmap, int>>();
#else
        Dictionary<int, BitmapImage> m_Bmps = new Dictionary<int, BitmapImage>();
#endif
        private void Timer_Tick(object sender, EventArgs e)
        {
            return;
#if NET5
#endif
#if TestD3DImage

            if (this.m_Bmps.ContainsKey(index) == false)
            {
                MemoryStream stream = this.m_Apng.ElementAt(index).Value;
                stream.Position = 0;
                IntPtr intptr = Marshal.AllocHGlobal((int)stream.Length);
                Marshal.Copy(stream.ToArray(), 0, intptr, (int)stream.Length);
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.EndInit();
                //WriteableBitmap writeableBitmap = new WriteableBitmap(bmp);
                //this.img.Source = writeableBitmap;
                this.m_Bmps.Add(index, Tuple.Create<WriteableBitmap, int>(new WriteableBitmap(bmp), 148*148*4));
            }
            this.m_D3DImage.Refresh(this.m_Bmps[index].Item1.BackBuffer, this.m_Bmps[index].Item2);
            //this.img.Source = this.m_Bmps[index];
            index = index + 1;
            if (index >= this.m_Apng.Count)
            {
                index = 0;
            }
#else
            Stream stream = this.m_Apng.ElementAt(index).Value;
            if(this.m_Bmps.ContainsKey(index) == false)
            {
                stream.Position = 0;
                BitmapImage bmp = new BitmapImage();
                
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.EndInit();
                this.m_Bmps.Add(index, bmp);
            }
            
            index = index + 1;
            if (index >= this.m_Apng.Count)
            {
                index = 0;
            }
#endif
        }

        async private void Image_Loaded_1(object sender, RoutedEventArgs e)
        {
            //TranslateTransform _heartTransform = (sender as Image).RenderTransform as TranslateTransform;
            //_checkStoryboard = new Storyboard();

            //var keyFrames = new ThicknessAnimationUsingKeyFrames();
            ////keyFrames.AutoReverse = true;
            //Storyboard.SetTarget(keyFrames, sender as Image);
            //Storyboard.SetTargetProperty(keyFrames, new PropertyPath("Margin"));
            //TimeSpan start = TimeSpan.Zero;
            ////keyFrames.Duration = TimeSpan.FromSeconds(25);
            //for (var i = 0; i < this.m_Apng.Count; i++)
            //{
            //    var keyFrame = new DiscreteThicknessKeyFrame
            //    {
            //        //KeyTime = TimeSpan.FromSeconds((i + 1d) / 28d),
            //        KeyTime = TimeSpan.FromSeconds(i*0.04),
            //        Value = new Thickness(-(i + 1) * 480, 0, 0, 0)
            //    };
            //    keyFrames.KeyFrames.Add(keyFrame);
            //}
            //_checkStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            //_checkStoryboard.Children.Add(keyFrames);

            ////_checkStoryboard.FillBehavior = FillBehavior.HoldEnd;

            ////await Task.Delay(1000);
            //_checkStoryboard.Begin();
        }
    }

    public class CD3DImage:D3DImage
    {
        [DllImport("user32.dll", SetLastError = false)]
        static extern IntPtr GetDesktopWindow();
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
        public void Open(int width, int height)
        {
            Direct3DEx _direct3D = new Direct3DEx();

            PresentParameters presentparams = new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                PresentationInterval = PresentInterval.Default,
                PresentFlags = PresentFlags.Video,
                // The device back buffer is not used.
                BackBufferFormat = Format.Unknown,
                BackBufferWidth = width,
                BackBufferHeight = height,

                // Use dummy window handle.
                DeviceWindowHandle = GetDesktopWindow()
            };


            _device = new DeviceEx(_direct3D, 0, DeviceType.Hardware, IntPtr.Zero,
                                   CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
                                   presentparams);
            IntPtr handle = IntPtr.Zero;
            //SharpDX.Direct3D9.Texture texture = new Texture(_device, width, height, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default, ref handle);
            //SharpDX.Direct3D9.Surface surface = texture.GetSurfaceLevel(0);
            surface = SharpDX.Direct3D9.Surface.CreateOffscreenPlain(_device, width, height, Format.X8R8G8B8, Pool.Default);

            _swapChain = new SwapChain(_device, presentparams);


            this.Lock();
            this.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _swapChain.GetBackBuffer(0).NativePointer);
            this.Unlock();
        }

        int m_Width;
        int m_Height;
        SwapChain _swapChain;
        DeviceEx _device;
        Surface surface;
        public void Refresh(IntPtr ptr, int len)
        {
            DataRectangle rect = surface.LockRectangle(LockFlags.Discard);
            CopyMemory(rect.DataPointer, ptr, (uint)len);

            
            surface.UnlockRectangle();

            using (Surface bb = _swapChain.GetBackBuffer(0))
            {
                try
                {
                    _swapChain.Device.StretchRectangle(surface, bb, TextureFilter.None);

                }
                catch (Exception ee)
                {
                    System.Diagnostics.Trace.WriteLine(ee.Message);
                }
                _swapChain.Device.Present();
            }
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                Lock();
                this.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _swapChain.GetBackBuffer(0).NativePointer);
                AddDirtyRect(new Int32Rect(0, 0, PixelWidth, PixelHeight));

                Unlock();
            }));
        }
    }
}
