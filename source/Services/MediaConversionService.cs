using BackgroundChanger.Models;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
        private const string FfprobeFileName = "ffprobe.exe";
        private const byte Vp8xAnimationFlag = 0x10;
        private const int ApngActlScanMaxBytes = 65536;

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
        /// Determines whether ffprobe is available from settings or next to the configured FFmpeg executable.
        /// </summary>
        /// <returns><c>true</c> when a resolved ffprobe path points to an existing file.</returns>
        public bool IsFfprobeConfigured()
        {
            string ffprobePath = ResolveFfprobePath();
            return !ffprobePath.IsNullOrWhiteSpace() && File.Exists(ffprobePath);
        }

        /// <summary>
        /// Determines whether both FFmpeg and ffprobe are configured and their executables exist.
        /// </summary>
        /// <returns><c>true</c> when the full media conversion toolchain is available.</returns>
        public bool IsMediaToolkitConfigured()
        {
            return IsFfmpegConfigured() && IsFfprobeConfigured();
        }

        /// <summary>
        /// Determines whether configured FFmpeg and ffprobe builds meet the minimum version for animated WebP conversion.
        /// Unparseable development builds (for example <c>N-xxxxx</c>) are treated as supported.
        /// </summary>
        /// <returns><c>true</c> when the toolchain is configured and versions are supported or unknown.</returns>
        public bool IsMediaToolkitVersionSupported()
        {
            if (!IsMediaToolkitConfigured())
            {
                return false;
            }

            MediaToolkitVersion minimumVersion = MediaToolkitVersion.MinimumForAnimatedWebp;

            MediaToolkitVersion ffmpegVersion;
            if (TryGetFfmpegVersion(out ffmpegVersion) && !ffmpegVersion.IsAtLeast(minimumVersion))
            {
                return false;
            }

            MediaToolkitVersion ffprobeVersion;
            if (TryGetFfprobeVersion(out ffprobeVersion) && !ffprobeVersion.IsAtLeast(minimumVersion))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the minimum FFmpeg/ffprobe version required for animated WebP conversion.
        /// </summary>
        /// <returns>Minimum version as a display string (for example <c>7.2.0</c>).</returns>
        public string GetMinimumMediaToolkitVersionDisplay()
        {
            return MediaToolkitVersion.MinimumForAnimatedWebp.ToString();
        }

        /// <summary>
        /// Gets a short summary of detected FFmpeg and ffprobe versions for user-facing messages.
        /// </summary>
        /// <returns>Detected versions or <c>unknown</c> when parsing failed.</returns>
        public string GetMediaToolkitVersionSummary()
        {
            return string.Format(
                "FFmpeg {0}, ffprobe {1}",
                GetDetectedToolkitVersionDisplay(PluginDatabase.PluginSettings.ffmpegFile, IsFfmpegConfigured(), TryGetFfmpegVersion),
                GetDetectedToolkitVersionDisplay(ResolveFfprobePath(), IsFfprobeConfigured(), TryGetFfprobeVersion));
        }

        /// <summary>
        /// Attempts to read the semantic version of the configured FFmpeg executable.
        /// </summary>
        /// <param name="version">Parsed version when successful.</param>
        /// <returns><c>true</c> when the version was parsed from <c>ffmpeg -version</c> output.</returns>
        public bool TryGetFfmpegVersion(out MediaToolkitVersion version)
        {
            return TryGetToolkitVersion(PluginDatabase.PluginSettings.ffmpegFile, out version);
        }

        /// <summary>
        /// Attempts to read the semantic version of the configured ffprobe executable.
        /// </summary>
        /// <param name="version">Parsed version when successful.</param>
        /// <returns><c>true</c> when the version was parsed from <c>ffprobe -version</c> output.</returns>
        public bool TryGetFfprobeVersion(out MediaToolkitVersion version)
        {
            return TryGetToolkitVersion(ResolveFfprobePath(), out version);
        }

        /// <summary>
        /// Resolves the ffprobe executable path from plugin settings or the FFmpeg install directory.
        /// </summary>
        /// <returns>The absolute ffprobe path when found; otherwise <c>null</c>.</returns>
        public string ResolveFfprobePath()
        {
            string configuredPath = PluginDatabase.PluginSettings.ffprobeFile;
            if (!configuredPath.IsNullOrWhiteSpace() && File.Exists(configuredPath))
            {
                return configuredPath;
            }

            string ffmpegPath = PluginDatabase.PluginSettings.ffmpegFile;
            if (!ffmpegPath.IsNullOrWhiteSpace())
            {
                string ffmpegDirectory = Path.GetDirectoryName(ffmpegPath);
                if (!ffmpegDirectory.IsNullOrEmpty())
                {
                    string siblingPath = Path.Combine(ffmpegDirectory, FfprobeFileName);
                    if (File.Exists(siblingPath))
                    {
                        return siblingPath;
                    }
                }
            }

            return null;
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
            if (!IsMediaToolkitConfigured())
            {
                LogDebugMessage("ConvertToMp4Async aborted: FFmpeg/ffprobe toolchain is not fully configured.");
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

            return IndexOfFourCc(data, data.Length, "ANMF") >= 0;
        }

        private static int IndexOfFourCc(byte[] data, int length, string fourCc)
        {
            if (fourCc == null || fourCc.Length != 4 || data == null)
            {
                return -1;
            }

            int scanLength = Math.Min(length, data.Length);
            if (scanLength < 4)
            {
                return -1;
            }

            byte b0 = (byte)fourCc[0];
            byte b1 = (byte)fourCc[1];
            byte b2 = (byte)fourCc[2];
            byte b3 = (byte)fourCc[3];

            for (int i = 0; i <= scanLength - 4; i++)
            {
                if (data[i] == b0 && data[i + 1] == b1 && data[i + 2] == b2 && data[i + 3] == b3)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfFourCc(byte[] data, string fourCc)
        {
            return IndexOfFourCc(data, data?.Length ?? 0, fourCc);
        }

        private bool IsAnimatedPng(string filePath)
        {
            try
            {
                if (IsFfprobeConfigured())
                {
                    bool? ffprobeResult = TryDetectAnimatedPngWithFfprobe(filePath);
                    if (ffprobeResult.HasValue)
                    {
                        LogDebugMessage(string.Format("APNG animation check (ffprobe): {0} -> {1}", filePath, ffprobeResult.Value));
                        return ffprobeResult.Value;
                    }

                    LogDebugMessage(string.Format("APNG ffprobe inconclusive for {0}, falling back to acTL scan.", filePath));
                }

                bool animated = IsAnimatedPngByActlChunk(filePath);
                LogDebugMessage(string.Format("APNG animation check (acTL): {0} -> {1}", filePath, animated));
                return animated;
            }
            catch (Exception ex)
            {
                LogDetectionFailure("APNG", filePath, ex);
                return false;
            }
        }

        private bool? TryDetectAnimatedPngWithFfprobe(string filePath)
        {
            string ffprobePath = ResolveFfprobePath();
            if (ffprobePath.IsNullOrWhiteSpace())
            {
                return null;
            }

            string arguments = string.Format(
                "-v error -show_entries format=format_name -of default=noprint_wrappers=1:nokey=1 \"{0}\"",
                filePath);

            if (!RunFfprobeProcess(ffprobePath, arguments, out string stdout, out string stderr))
            {
                LogDebugMessage(string.Format(
                    "APNG ffprobe failed for {0}. Stderr: {1}",
                    filePath,
                    TruncateForLog(stderr)));
                return null;
            }

            string formatName = stdout?.Trim();
            if (formatName.IsNullOrWhiteSpace())
            {
                return null;
            }

            return formatName.Equals("apng", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAnimatedPngByActlChunk(string filePath)
        {
            using (Stream stream = FileSystem.OpenReadFileStreamSafe(filePath))
            {
                if (stream.Length < 8)
                {
                    return false;
                }

                int scanLength = (int)Math.Min(ApngActlScanMaxBytes, stream.Length);
                byte[] data = new byte[scanLength];
                int bytesRead = stream.Read(data, 0, scanLength);
                if (bytesRead < 8)
                {
                    return false;
                }

                if (data[0] != 0x89 || data[1] != (byte)'P' || data[2] != (byte)'N' || data[3] != (byte)'G')
                {
                    return false;
                }

                return IndexOfFourCc(data, bytesRead, "acTL") >= 0;
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
                    && (inputFormat == MediaConversionInputFormat.AnimatedWebp
                        || inputFormat == MediaConversionInputFormat.Apng
                        || inputFormat == MediaConversionInputFormat.Gif))
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

        private delegate bool TryGetVersionDelegate(out MediaToolkitVersion version);

        private static string GetDetectedToolkitVersionDisplay(
            string executablePath,
            bool isConfigured,
            TryGetVersionDelegate tryGetVersion)
        {
            if (!isConfigured)
            {
                return "not configured";
            }

            MediaToolkitVersion version;
            if (tryGetVersion(out version))
            {
                return version.ToString();
            }

            return executablePath.IsNullOrWhiteSpace() ? "unknown" : "unknown (" + Path.GetFileName(executablePath) + ")";
        }

        private bool TryGetToolkitVersion(string executablePath, out MediaToolkitVersion version)
        {
            version = default(MediaToolkitVersion);

            if (executablePath.IsNullOrWhiteSpace() || !File.Exists(executablePath))
            {
                return false;
            }

            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = "-version",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };

                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return MediaToolkitVersion.TryParseFromVersionOutput(stdout + stderr, out version);
            }
            catch (Exception ex)
            {
                LogDebugMessage(string.Format(
                    "Failed to read toolkit version for {0}: {1}",
                    executablePath,
                    ex.Message));
                return false;
            }
        }

        private static bool RunFfprobeProcess(string ffprobePath, string arguments, out string stdout, out string stderr)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.Start();
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0;
        }

        private static void LogDebugMessage(string message)
        {
            Common.LogDebug(true, LogPrefix + " " + message);
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
