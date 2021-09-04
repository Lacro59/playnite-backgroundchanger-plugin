using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WebPWrapper;

namespace wpf_animatedimage
{
    public class WebpAnim : IDisposable
    {
        private string Webpinfo = "webpinfo.exe";
        private string WebpinfoPath = string.Empty;

        private WebP Webp = new WebP();

        private string FilePath = string.Empty;
        private byte[] RawWebpAnim = null;

        public int CanvasWidth { get; private set; }
        public int CanvasHeight { get; private set; }

        private List<WebpInfo> webpInfos = new List<WebpInfo>();

        private Image BackImage = null;
        private MemoryStream memoryStream = new MemoryStream();

        private Graphics canvas;
        private Bitmap bitmap;


        public WebpAnim()
        {
            string AppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            WebpinfoPath = Path.Combine(AppPath, Webpinfo);
        }

        public int FrameActual { get; private set; } = 0;


        public void Load(string FilePath)
        {
            this.FilePath = FilePath;
            this.RawWebpAnim = ReadFileAsBytesSafe(FilePath);
            this.webpInfos = GetWebpInfo();
        }


        private string[] GetFileInfo()
        {
            if (File.Exists(WebpinfoPath))
            {
                string arguments = @"/c " + WebpinfoPath + " -summary " + FilePath;

                string FileInfo = RunExternalExe("cmd.exe", arguments);

                string[] FileInfoSplitted = FileInfo.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                return FileInfoSplitted;
            }

            return null;
        }


        private List<WebpInfo> GetWebpInfo()
        {
            string[] FileInfo = GetFileInfo();

            List<WebpInfo> GetWebpInfo = new List<WebpInfo>();

            if (FileInfo == null)
            {
                return GetWebpInfo;
            }

            try
            {
                string RegMatch = string.Empty;
                Match match = null;

                bool IsVp8 = false;
                bool IsAnmf = false;
                bool IsAlpf = false;

                WebpInfo webpInfo = null;

                foreach (string Line in FileInfo)
                {
                    int Offset = 0;
                    int Length = 0;

                    int Width = 0;
                    int Height = 0;
                    int Alpha = 0;
                    int Animation = 0;

                    int Offset_X = 0;
                    int Offset_Y = 0;
                    int Duration = 0;
                    int Dispose = 0;
                    int Blend = 0;


                    if (Line.Contains("Canvas size"))
                    {
                        match = Regex.Match(Line.Trim(), @"\bCanvas size\b[ ]*(\d*)[ ]*x[ ]*(\d*)");
                        int.TryParse(match.Groups[1].Value, out Width);
                        int.TryParse(match.Groups[2].Value, out Height);

                        CanvasWidth = Width;
                        CanvasWidth = Height;
                    }


                    if (Line.Contains("Chunk ANMF at offset"))
                    {
                        if (webpInfo != null)
                        {
                            byte[] RawWebp = new byte[webpInfo.GetLength()];
                            Array.Copy(RawWebpAnim, webpInfo.GetOffsetStart(), RawWebp, 0, webpInfo.GetLength());
                            webpInfo.RawWebp = RawWebp;

                            GetWebpInfo.Add(webpInfo);
                        }

                        webpInfo = new WebpInfo();
                        webpInfo.Vp8 = new Vp8();
                        webpInfo.Vp8.ByteInfo = new ByteInfo();
                        webpInfo.Anmf = new Anmf();
                        webpInfo.Anmf.ByteInfo = new ByteInfo();
                        webpInfo.Alph = new Alph();
                        webpInfo.Alph.ByteInfo = new ByteInfo();


                        match = Regex.Match(Line.Trim(), @"\boffset[ ]*\b(\d*),[ ]*\blength[ ]*\b(\d*)");
                        int.TryParse(match.Groups[1].Value, out Offset);
                        int.TryParse(match.Groups[2].Value, out Length);

                        webpInfo.Anmf.ByteInfo.Offset = Offset;
                        webpInfo.Anmf.ByteInfo.Length = Length;

                        IsVp8 = false;
                        IsAnmf = true;
                        IsAlpf = false;
                    }

                    if (IsAnmf)
                    {
                        if (Line.Contains("Offset_X:"))
                        {
                            int.TryParse(Line.Replace("Offset_X:", string.Empty).Trim(), out Offset_X);
                            webpInfo.Anmf.Offset_X = Offset_X;
                        }
                        if (Line.Contains("Offset_Y:"))
                        {
                            int.TryParse(Line.Replace("Offset_Y:", string.Empty).Trim(), out Offset_Y);
                            webpInfo.Anmf.Offset_Y = Offset_Y;
                        }
                        if (Line.Contains("Width:"))
                        {
                            int.TryParse(Line.Replace("Width:", string.Empty).Trim(), out Width);
                            webpInfo.Anmf.Width = Width;
                        }
                        if (Line.Contains("Height:"))
                        {
                            int.TryParse(Line.Replace("Height:", string.Empty).Trim(), out Height);
                            webpInfo.Anmf.Height = Height;
                        }
                        if (Line.Contains("Duration:"))
                        {
                            int.TryParse(Line.Replace("Duration:", string.Empty).Trim(), out Duration);
                            webpInfo.Anmf.Duration = Duration;
                        }
                        if (Line.Contains("Dispose:"))
                        {
                            int.TryParse(Line.Replace("Dispose:", string.Empty).Trim(), out Dispose);
                            webpInfo.Anmf.Dispose = Dispose;
                        }
                        if (Line.Contains("Blend:"))
                        {
                            int.TryParse(Line.Replace("Blend:", string.Empty).Trim(), out Blend);
                            webpInfo.Anmf.Blend = Blend;
                        }
                    }


                    if (Line.Contains("Chunk VP8  at offset") || Line.Contains("Chunk VP8L at offset"))
                    {
                        match = Regex.Match(Line.Trim(), @"\boffset[ ]*\b(\d*),[ ]*\blength[ ]*\b(\d*)");
                        int.TryParse(match.Groups[1].Value, out Offset);
                        int.TryParse(match.Groups[2].Value, out Length);

                        webpInfo.Vp8.ByteInfo.Offset = Offset;
                        webpInfo.Vp8.ByteInfo.Length = Length;

                        IsVp8 = true;
                        IsAnmf = false;
                        IsAlpf = false;
                    }

                    if (IsVp8)
                    {
                        if (Line.Contains("Width:"))
                        {
                            int.TryParse(Line.Replace("Width:", string.Empty).Trim(), out Width);
                            webpInfo.Vp8.Width = Width;
                        }
                        if (Line.Contains("Height:"))
                        {
                            int.TryParse(Line.Replace("Height:", string.Empty).Trim(), out Height);
                            webpInfo.Vp8.Height = Height;
                        }
                        if (Line.Contains("Alpha:"))
                        {
                            int.TryParse(Line.Replace("Alpha:", string.Empty).Trim(), out Alpha);
                            webpInfo.Vp8.Alpha = Alpha;
                        }
                        if (Line.Contains("Animation:"))
                        {
                            int.TryParse(Line.Replace("Animation:", string.Empty).Trim(), out Animation);
                            webpInfo.Vp8.Animation = Animation;
                        }
                        if (Line.Contains("Format:"))
                        {
                            if (Line.ToLower().Contains("lossyless"))
                            {
                                webpInfo.Vp8.Format = WebpFormat.lossless;
                            }
                            if (Line.ToLower().Contains("lossy"))
                            {
                                webpInfo.Vp8.Format = WebpFormat.lossy;
                            }
                        }
                    }


                    if (Line.Contains("Chunk ALPH at offset"))
                    {
                        match = Regex.Match(Line.Trim(), @"\boffset[ ]*\b(\d*),[ ]*\blength[ ]*\b(\d*)");
                        int.TryParse(match.Groups[1].Value, out Offset);
                        int.TryParse(match.Groups[2].Value, out Length);

                        webpInfo.Alph.ByteInfo.Offset = Offset;
                        webpInfo.Alph.ByteInfo.Length = Length;

                        IsVp8 = false;
                        IsAnmf = false;
                        IsAlpf = true;
                    }
                }
            }
            catch (Exception ex)
            {

            }

            return GetWebpInfo;
        }




        public Stream GetStream()
        {
            memoryStream = new MemoryStream();
            memoryStream.Position = 0;

            if (FramesCount() == 0)
            {
                if (File.Exists(FilePath))
                {
                    Webp.Decode(RawWebpAnim).Save(memoryStream, ImageFormat.Png);
                    return memoryStream;
                }
            }
            else
            {
                Webp.Decode(webpInfos[0].RawWebp).Save(memoryStream, ImageFormat.Png);
                return memoryStream;
            }

            return null;
        }

        public Stream GetFrameStream(int frame)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("-GetFrameStream-------------------------");
            System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
#endif

            if (FramesCount() == 0)
            {
                if (File.Exists(FilePath))
                {
                    memoryStream = new MemoryStream();
                    memoryStream.Position = 0;
                    Webp.Decode(RawWebpAnim).Save(memoryStream, ImageFormat.Png);
                    return memoryStream;
                }
            }

            if (frame >= FramesCount())
            {
                return null;
            }
            if (frame < 0)
            {
                return null;
            }

            memoryStream = new MemoryStream();
            memoryStream.Position = 0;

            bitmap = Webp.Decode(webpInfos[frame].RawWebp);

#if DEBUG
            System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//6ms
#endif

            Stream WebpImage;
            if (BackImage != null)
            {
                bitmap = GenerateImageBitmap(bitmap, webpInfos[frame].Anmf.Offset_X, webpInfos[frame].Anmf.Offset_Y);
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//8ms
#endif

            bitmap.Save(memoryStream, ImageFormat.Png);
            WebpImage = memoryStream;

            BackImage = bitmap;

#if DEBUG
            System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//20ms
            System.Diagnostics.Debug.WriteLine("-GetFrameStream-END---------------------");
#endif

            return WebpImage;
        }

        public BitmapSource GetFrameBitmapSource(int frame)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("-GetFrameBitmapSource-------------------------");
            System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);
#endif

            if (FramesCount() == 0)
            {
                if (File.Exists(FilePath))
                {
                    memoryStream = new MemoryStream();
                    memoryStream.Position = 0;
                    bitmap = Webp.Decode(RawWebpAnim);
                    return LoadBitmap(bitmap);
                }
            }

            if (frame >= FramesCount())
            {
                return null;
            }
            if (frame < 0)
            {
                return null;
            }

            memoryStream = new MemoryStream();
            memoryStream.Position = 0;

            bitmap = Webp.Decode(webpInfos[frame].RawWebp);

#if DEBUG
            System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//6ms
#endif

            BitmapSource WebpImage;
            if (BackImage != null)
            {
                bitmap = GenerateImageBitmap(bitmap, webpInfos[frame].Anmf.Offset_X, webpInfos[frame].Anmf.Offset_Y);
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//8ms
#endif

            WebpImage = LoadBitmap(bitmap);

            BackImage = bitmap;

#if DEBUG
            System.Diagnostics.Debug.WriteLine(DateTime.Now.Ticks / (decimal)TimeSpan.TicksPerMillisecond);//4ms
            System.Diagnostics.Debug.WriteLine("-GetFrameBitmapSource-END---------------------");
#endif

            return WebpImage;
        }


        public int FramesCount()
        {
            return webpInfos.Count();
        }

        public int FramesDuration()
        {
            if (FramesCount() > 0)
            {
                return webpInfos[0].Anmf.Duration;
            }
            return 0;
        }


        public void Dispose()
        {
            if (memoryStream != null)
            {
                memoryStream.Dispose();
            }
            memoryStream = null;

            RawWebpAnim = null;

            if (Webp != null)
            {
                Webp.Dispose();
            }
            Webp = null;

            webpInfos = null;

            if (BackImage != null)
            {
                BackImage.Dispose();
            }
            BackImage = null;

            if (canvas != null)
            {
                canvas.Dispose();
            }
            canvas = null;

            if (bitmap != null)
            {
                bitmap.Dispose();
            }
            bitmap = null;
        }



        private Bitmap GenerateImageBitmap(Image frontImage, int x = 0, int y = 0)
        {
            int targetHeight = BackImage.Height;
            int targetWidth = BackImage.Width;

            //be sure to use a pixelformat that supports transparency
            bitmap = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);

            canvas = Graphics.FromImage(bitmap);

            //this ensures that the backgroundcolor is transparent
            canvas.Clear(Color.Transparent);

            //this selects the entire backimage and and paints
            //it on the new image in the same size, so its not distorted.
            canvas.DrawImage(BackImage,
                        new Rectangle(0, 0, BackImage.Width, BackImage.Height),
                        new Rectangle(0, 0, BackImage.Width, BackImage.Height),
                        GraphicsUnit.Pixel);

            //this paints the frontimage with a offset at the given coordinates
            canvas.DrawImage(frontImage, x, y, frontImage.Width, frontImage.Height);

            canvas.Save();

            return bitmap;
        }

        private Stream GenerateImageStream(Image frontImage, int x = 0, int y = 0)
        {
            bitmap = GenerateImageBitmap(frontImage, x, y);

            memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Png);

            return memoryStream;
        }



        private static byte[] ReadFileAsBytesSafe(string path, int retryAttempts = 5)
        {
            IOException ioException = null;
            for (int i = 0; i < retryAttempts; i++)
            {
                try
                {
                    return File.ReadAllBytes(path);
                }
                catch (IOException exc)
                {
                    ioException = exc;
                    Task.Delay(500).Wait();
                }
            }

            throw new IOException($"Failed to read {path}", ioException);
        }

        private string RunExternalExe(string filename, string arguments = null)
        {
            var process = new Process();

            process.StartInfo.FileName = filename;
            if (!string.IsNullOrEmpty(arguments))
            {
                process.StartInfo.Arguments = arguments;
            }

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;

            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            var stdOutput = new StringBuilder();
            process.OutputDataReceived += (sender, args) => stdOutput.AppendLine(args.Data); // Use AppendLine rather than Append since args.Data is one line of output, not including the newline character.

            string stdError = null;
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                stdError = process.StandardError.ReadToEnd();
                process.WaitForExit();
            }
            catch (Exception e)
            {
                throw new Exception("OS error while executing " + Format(filename, arguments) + ": " + e.Message, e);
            }

            if (process.ExitCode == 0)
            {
                return stdOutput.ToString();
            }
            else
            {
                var message = new StringBuilder();

                if (!string.IsNullOrEmpty(stdError))
                {
                    message.AppendLine(stdError);
                }

                if (stdOutput.Length != 0)
                {
                    message.AppendLine("Std output:");
                    message.AppendLine(stdOutput.ToString());
                }

                throw new Exception(Format(filename, arguments) + " finished with exit code = " + process.ExitCode + ": " + message);
            }
        }

        private string Format(string filename, string arguments)
        {
            return "'" + filename +
                ((string.IsNullOrEmpty(arguments)) ? string.Empty : " " + arguments) +
                "'";
        }





        [DllImport("gdi32")]
        static extern int DeleteObject(IntPtr o);

        public static BitmapSource LoadBitmap(System.Drawing.Bitmap source)
        {
            IntPtr ip = source.GetHbitmap();
            BitmapSource bs = null;
            try
            {
                   bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(ip,
                   IntPtr.Zero, Int32Rect.Empty,
                   System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(ip);
            }

            return bs;
        }
    }


    public enum WebpFormat
    {
        lossy,
        lossless
    }

    public class WebpInfo
    {
        public Vp8 Vp8 { get; set; }
        public Anmf Anmf { get; set; }
        public Alph Alph { get; set; }

        public byte[] RawWebp { get; set; }

        public long GetOffsetStart()
        {
            if (Alph.ByteInfo.Offset != 0)
            {
                return Alph.ByteInfo.Offset;
            }

            return Vp8.ByteInfo.Offset;
        }
        public long GetLength()
        {
            if (Alph.ByteInfo.Length != 0)
            {
                return Alph.ByteInfo.Length + Vp8.ByteInfo.Length;
            }

            return Vp8.ByteInfo.Length;
        }
    }

    public class ByteInfo
    {
        public long Offset { get; set; }
        public long Length { get; set; }
    }

    public class Vp8
    {
        public ByteInfo ByteInfo { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Alpha { get; set; }
        public int Animation { get; set; }
        public WebpFormat Format { get; set; }
    }

    public class Anmf
    {
        public ByteInfo ByteInfo { get; set; }
        public int Offset_X { get; set; }
        public int Offset_Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Duration { get; set; }
        public int Dispose { get; set; }
        public int Blend { get; set; }
    }

    public class Alph
    {
        public ByteInfo ByteInfo { get; set; }
    }
}
