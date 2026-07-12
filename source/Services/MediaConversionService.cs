using BackgroundChanger.Models;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using QSoft.Apng;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using wpf_animatedimage;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Centralizes detection and FFmpeg conversion of animated media to MP4 (H.264 / yuv420p).
    /// Alpha transparency is not preserved; most animated library art does not rely on it.
    /// </summary>
    public class MediaConversionService
    {
        private const int MaxStderrLogLength = 2000;
        private const string LogPrefix = "[MediaConversionService]";
        private const string WebpinfoFileName = "webpinfo.exe";
        private const byte Vp8xAnimationFlag = 0x10;

        private enum MediaConversionInputFormat
        {
            AnimatedWebp,
            Webm,
            Apng,
            Gif,
            Other
        }

        /// <summary>
        /// Gets the plugin database instance.
        /// </summary>
        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        /// <summary>
        /// Determines whether FFmpeg is configured and the executable file exists.
        /// </summary>
        /// <returns><c>true</c> when <see cref="BackgroundChangerSettings.ffmpegFile"/> points to an existing file.</returns>
        public bool IsFfmpegConfigured()
        {
            string ffmpegPath = PluginDatabase.PluginSettings.ffmpegFile;
            return !ffmpegPath.IsNullOrWhiteSpace() && File.Exists(ffmpegPath);
        }

        /// <summary>
        /// Determines whether the given path refers to animated media that should be converted to MP4.
        /// Static images and existing MP4 files are excluded.
        /// </summary>
        /// <param name="path">Local file path or remote URL.</param>
        /// <returns><c>true</c> when the media is animated and not already MP4.</returns>
        public bool ShouldConvert(string path)
        {
            if (path.IsNullOrWhiteSpace())
            {
                return false;
            }

            if (Path.GetExtension(path).IsEqual(".mp4"))
            {
                return false;
            }

            if (IsRemotePath(path))
            {
                return ShouldConvertRemote(path);
            }

            if (!File.Exists(path))
            {
                return false;
            }

            return ShouldConvertLocal(path);
        }

        /// <summary>
        /// Converts animated media to MP4 using FFmpeg (libx264, CRF 23, yuv420p, faststart).
        /// </summary>
        /// <param name="inputPath">Local file path or remote URL readable by FFmpeg.</param>
        /// <param name="outputPath">Target MP4 path. When <c>null</c>, derived from <paramref name="inputPath"/> (local only).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The output MP4 path on success; otherwise <c>null</c>.</returns>
        public async Task<string> ConvertToMp4Async(
            string inputPath,
            string outputPath = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsFfmpegConfigured())
            {
                LogDebugMessage("ConvertToMp4Async aborted: FFmpeg is not configured or the executable was not found.");
                return null;
            }

            if (inputPath.IsNullOrWhiteSpace())
            {
                LogDebugMessage("ConvertToMp4Async aborted: input path is empty.");
                return null;
            }

            string resolvedOutput = outputPath ?? Path.ChangeExtension(inputPath, ".mp4");
            if (resolvedOutput.IsNullOrWhiteSpace())
            {
                LogDebugMessage(string.Format("ConvertToMp4Async aborted: could not resolve output path for input {0}.", inputPath));
                return null;
            }

            if (IsRemotePath(inputPath) && outputPath.IsNullOrWhiteSpace())
            {
                LogDebugMessage(string.Format("ConvertToMp4Async aborted: remote input requires an explicit output path ({0}).", inputPath));
                return null;
            }

            string outputDirectory = Path.GetDirectoryName(resolvedOutput);
            if (!outputDirectory.IsNullOrEmpty())
            {
                FileSystem.CreateDirectory(outputDirectory);
            }

            string ffmpegPath = PluginDatabase.PluginSettings.ffmpegFile;

            return await Task.Run(
                () => ConvertToMp4(inputPath, resolvedOutput, ffmpegPath, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        private bool ShouldConvertRemote(string path)
        {
            return path.Contains(".webm", StringComparison.InvariantCultureIgnoreCase);
        }

        private bool ShouldConvertLocal(string path)
        {
            string extension = Path.GetExtension(path);

            if (extension.IsEqual(".webm"))
            {
                return true;
            }

            if (extension.IsEqual(".webp"))
            {
                return IsAnimatedWebp(path);
            }

            if (extension.IsEqual(".png"))
            {
                return IsAnimatedPng(path);
            }

            if (extension.IsEqual(".gif"))
            {
                return IsAnimatedGif(path);
            }

            return false;
        }

        private static bool IsAnimatedWebp(string filePath)
        {
            try
            {
                bool animated = IsAnimatedWebpByContainer(filePath);
                LogDebugMessage(string.Format("WebP animation check: {0} -> {1}", filePath, animated));
                return animated;
            }
            catch (Exception ex)
            {
                LogDetectionFailure("WebP", filePath, ex);
                return false;
            }
        }

        /// <summary>
        /// Detects animated WebP via RIFF container flags (VP8X bit 4) or ANMF chunks.
        /// </summary>
        private static bool IsAnimatedWebpByContainer(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            if (data.Length < 12)
            {
                return false;
            }

            if (data[0] != (byte)'R' || data[1] != (byte)'I' || data[2] != (byte)'F' || data[3] != (byte)'F')
            {
                return false;
            }

            if (data[8] != (byte)'W' || data[9] != (byte)'E' || data[10] != (byte)'B' || data[11] != (byte)'P')
            {
                return false;
            }

            for (int i = 12; i <= data.Length - 9; i++)
            {
                if (data[i] == (byte)'V' && data[i + 1] == (byte)'P' && data[i + 2] == (byte)'8' && data[i + 3] == (byte)'X')
                {
                    byte flags = data[i + 8];
                    if ((flags & Vp8xAnimationFlag) != 0)
                    {
                        return true;
                    }
                }
            }

            return IndexOfFourCc(data, "ANMF") >= 0;
        }

        private static int IndexOfFourCc(byte[] data, string fourCc)
        {
            if (fourCc == null || fourCc.Length != 4)
            {
                return -1;
            }

            byte b0 = (byte)fourCc[0];
            byte b1 = (byte)fourCc[1];
            byte b2 = (byte)fourCc[2];
            byte b3 = (byte)fourCc[3];

            for (int i = 0; i <= data.Length - 4; i++)
            {
                if (data[i] == b0 && data[i + 1] == b1 && data[i + 2] == b2 && data[i + 3] == b3)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsAnimatedPng(string filePath)
        {
            try
            {
                Png_Reader pngReader = new Png_Reader();
                using (Stream stream = FileSystem.OpenReadFileStreamSafe(filePath))
                {
                    Dictionary<fcTL, MemoryStream> apngFrames = pngReader.Open(stream).SpltAPng();
                    return apngFrames.Count > 0;
                }
            }
            catch (Exception ex)
            {
                LogDetectionFailure("APNG", filePath, ex);
                return false;
            }
        }

        private static bool IsAnimatedGif(string filePath)
        {
            try
            {
                using (Stream stream = File.OpenRead(filePath))
                using (Image gif = Image.FromStream(stream, false, false))
                {
                    FrameDimension dimension = new FrameDimension(gif.FrameDimensionsList[0]);
                    return gif.GetFrameCount(dimension) > 1;
                }
            }
            catch (Exception ex)
            {
                LogDetectionFailure("GIF", filePath, ex);
                return false;
            }
        }

        private static bool IsRemotePath(string path)
        {
            return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private string ConvertToMp4(
            string inputPath,
            string outputPath,
            string ffmpegPath,
            CancellationToken cancellationToken)
        {
            MediaConversionInputFormat inputFormat = ResolveInputFormat(inputPath);

            if (inputFormat == MediaConversionInputFormat.AnimatedWebp)
            {
                return ConvertAnimatedWebpToMp4(inputPath, outputPath, ffmpegPath, cancellationToken);
            }

            return ConvertViaDirectFfmpeg(inputPath, outputPath, ffmpegPath, inputFormat, cancellationToken);
        }

        private MediaConversionInputFormat ResolveInputFormat(string inputPath)
        {
            if (IsLocalAnimatedWebp(inputPath))
            {
                return MediaConversionInputFormat.AnimatedWebp;
            }

            string extension = Path.GetExtension(inputPath);

            if (extension.IsEqual(".webm") || (IsRemotePath(inputPath) && inputPath.Contains(".webm", StringComparison.InvariantCultureIgnoreCase)))
            {
                return MediaConversionInputFormat.Webm;
            }

            if (extension.IsEqual(".png") && File.Exists(inputPath) && IsAnimatedPng(inputPath))
            {
                return MediaConversionInputFormat.Apng;
            }

            if (extension.IsEqual(".gif"))
            {
                return MediaConversionInputFormat.Gif;
            }

            return MediaConversionInputFormat.Other;
        }

        private MediaConversionSettings GetConversionSettings()
        {
            MediaConversionSettings settings = PluginDatabase.PluginSettings.MediaConversion;
            return settings ?? new MediaConversionSettings();
        }

        private MediaConversionFormatSettings GetFormatSettings(MediaConversionInputFormat inputFormat)
        {
            MediaConversionSettings settings = GetConversionSettings();

            switch (inputFormat)
            {
                case MediaConversionInputFormat.AnimatedWebp:
                    return settings.AnimatedWebp;
                case MediaConversionInputFormat.Webm:
                    return settings.Webm;
                case MediaConversionInputFormat.Apng:
                    return settings.Apng;
                case MediaConversionInputFormat.Gif:
                    return settings.Gif;
                default:
                    return new MediaConversionFormatSettings();
            }
        }

        private static bool IsLocalAnimatedWebp(string inputPath)
        {
            return !IsRemotePath(inputPath)
                && Path.GetExtension(inputPath).IsEqual(".webp")
                && IsAnimatedWebpByContainer(inputPath);
        }

        /// <summary>
        /// FFmpeg cannot decode animated WebP (ANIM/ANMF chunks). Frames are extracted via webpinfo + WebpAnim, then encoded to MP4.
        /// </summary>
        private string ConvertAnimatedWebpToMp4(
            string inputPath,
            string outputPath,
            string ffmpegPath,
            CancellationToken cancellationToken)
        {
            string webpinfoPath = ResolveWebpinfoPath();
            if (webpinfoPath.IsNullOrEmpty() || !File.Exists(webpinfoPath))
            {
                LogDebugMessage(string.Format(
                    "Animated WebP conversion aborted: webpinfo.exe not found (configured or next to plugin). Input: {0}",
                    inputPath));
                return null;
            }

            string tempFramesDirectory = Path.Combine(
                PluginDatabase.Paths.PluginCachePath,
                "webp-frames-" + Guid.NewGuid().ToString("N"));

            try
            {
                FileSystem.CreateDirectory(tempFramesDirectory);

                int frameCount;
                int width;
                int height;
                int framerate;

                using (WebpAnim webpAnim = new WebpAnim())
                {
                    webpAnim.SetWebpinfoExecutable(webpinfoPath);
                    webpAnim.Load(inputPath);
                    frameCount = webpAnim.FramesCount();

                    if (frameCount == 0)
                    {
                        LogDebugMessage(string.Format(
                            "Animated WebP conversion aborted: no frames parsed for {0} (webpinfo: {1}).",
                            inputPath,
                            webpinfoPath));
                        return null;
                    }

                    framerate = GetWebpOutputFramerate(webpAnim, GetConversionSettings());
                    width = 0;
                    height = 0;

                    for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string framePath = Path.Combine(tempFramesDirectory, string.Format("frame_{0:D4}.png", frameIndex));
                        using (Stream frameStream = webpAnim.GetFrameStream(frameIndex))
                        using (Image frameImage = Image.FromStream(frameStream))
                        {
                            if (frameIndex == 0)
                            {
                                width = frameImage.Width;
                                height = frameImage.Height;
                            }

                            frameImage.Save(framePath, ImageFormat.Png);
                        }
                    }
                }

                int crf = GetConversionSettings().ResolveCrf(GetFormatSettings(MediaConversionInputFormat.AnimatedWebp));
                string framePattern = Path.Combine(tempFramesDirectory, "frame_%04d.png");
                string arguments = string.Format(
                    "-y -framerate {0} -f image2 -video_size {1}x{2} -i \"{3}\" -c:v libx264 -crf {4} -pix_fmt yuv420p -movflags +faststart \"{5}\"",
                    framerate,
                    width,
                    height,
                    framePattern,
                    crf,
                    outputPath);

                LogDebugMessage(string.Format(
                    "Animated WebP frame export complete ({0} frames, {1} fps). Encoding to MP4: {2}",
                    frameCount,
                    framerate,
                    outputPath));

                if (!RunFfmpegProcess(ffmpegPath, arguments, inputPath, outputPath, out string stderr))
                {
                    LogDebugMessage(string.Format(
                        "FFmpeg failed on animated WebP frames (exit). Input: {0} Output: {1} Stderr: {2}",
                        inputPath,
                        outputPath,
                        TruncateForLog(stderr)));
                    return null;
                }

                LogDebugMessage(string.Format("Converted animated WebP to MP4: {0} -> {1}", inputPath, outputPath));
                return outputPath;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                return null;
            }
            finally
            {
                try
                {
                    FileSystem.DeleteDirectory(tempFramesDirectory, true);
                }
                catch (Exception ex)
                {
                    LogDebugMessage(string.Format("Failed to delete temporary WebP frames directory {0}: {1}", tempFramesDirectory, ex.Message));
                }
            }
        }

        private string ConvertViaDirectFfmpeg(
            string inputPath,
            string outputPath,
            string ffmpegPath,
            MediaConversionInputFormat inputFormat,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                MediaConversionSettings conversionSettings = GetConversionSettings();
                MediaConversionFormatSettings formatSettings = GetFormatSettings(inputFormat);
                int crf = conversionSettings.ResolveCrf(formatSettings);
                string arguments;

                if (inputFormat == MediaConversionInputFormat.Webm
                    && formatSettings is MediaConversionWebmSettings webmSettings
                    && !webmSettings.ForceReencode)
                {
                    arguments = string.Format(
                        "-y -i \"{0}\" -c:v copy -movflags +faststart \"{1}\"",
                        inputPath,
                        outputPath);
                }
                else if (!formatSettings.UseAutoFramerate
                    && (inputFormat == MediaConversionInputFormat.Apng || inputFormat == MediaConversionInputFormat.Gif))
                {
                    int framerate = conversionSettings.ResolveFramerate(formatSettings, MediaConversionDefaults.DefaultFramerate);
                    arguments = string.Format(
                        "-y -r {0} -i \"{1}\" -c:v libx264 -crf {2} -pix_fmt yuv420p -movflags +faststart \"{3}\"",
                        framerate,
                        inputPath,
                        crf,
                        outputPath);
                }
                else
                {
                    arguments = string.Format(
                        "-y -i \"{0}\" -c:v libx264 -crf {1} -pix_fmt yuv420p -movflags +faststart \"{2}\"",
                        inputPath,
                        crf,
                        outputPath);
                }

                if (!RunFfmpegProcess(ffmpegPath, arguments, inputPath, outputPath, out string stderr))
                {
                    LogDebugMessage(string.Format(
                        "FFmpeg failed (exit). Input: {0} Output: {1} Command: {2} {3} Stderr: {4}",
                        inputPath,
                        outputPath,
                        ffmpegPath,
                        arguments,
                        TruncateForLog(stderr)));
                    return null;
                }

                LogDebugMessage(string.Format("Converted to MP4: {0} -> {1}", inputPath, outputPath));
                return outputPath;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                return null;
            }
        }

        private static bool RunFfmpegProcess(
            string ffmpegPath,
            string arguments,
            string inputPathForLog,
            string outputPath,
            out string stderr)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.Start();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 && File.Exists(outputPath);
        }

        private string ResolveWebpinfoPath()
        {
            string configuredPath = PluginDatabase.PluginSettings.webpinfoFile;
            if (!configuredPath.IsNullOrWhiteSpace() && File.Exists(configuredPath))
            {
                return configuredPath;
            }

            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!pluginDirectory.IsNullOrEmpty())
            {
                string pluginLocalPath = Path.Combine(pluginDirectory, WebpinfoFileName);
                if (File.Exists(pluginLocalPath))
                {
                    return pluginLocalPath;
                }
            }

            return null;
        }

        private static int GetWebpOutputFramerate(WebpAnim webpAnim, MediaConversionSettings conversionSettings)
        {
            MediaConversionFormatSettings webpSettings = conversionSettings.AnimatedWebp ?? new MediaConversionFormatSettings();
            int autoFramerate = MediaConversionDefaults.DefaultFramerate;
            int frameDurationMs = webpAnim.FramesDuration();

            if (frameDurationMs > 0)
            {
                autoFramerate = Math.Max(1, (int)Math.Round(1000.0 / frameDurationMs));
            }

            return conversionSettings.ResolveFramerate(webpSettings, autoFramerate);
        }

        private static void LogDebugMessage(string message)
        {
            Common.LogDebug(false, LogPrefix + " " + message);
        }

        private static void LogDetectionFailure(string formatLabel, string filePath, Exception ex)
        {
            LogDebugMessage(string.Format(
                "Failed to detect animated {0} for {1}: {2}",
                formatLabel,
                filePath,
                ex.Message));
        }

        private static string TruncateForLog(string text)
        {
            if (text.IsNullOrEmpty())
            {
                return string.Empty;
            }

            if (text.Length <= MaxStderrLogLength)
            {
                return text;
            }

            return text.Substring(0, MaxStderrLogLength) + "...";
        }
    }
}
