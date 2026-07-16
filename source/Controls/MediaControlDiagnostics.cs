using Playnite.SDK.Models;
using System;
using System.Diagnostics;
using System.IO;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Shared diagnostics helper for <see cref="PluginBackgroundImage"/>, <see cref="PluginCoverImage"/>, and <see cref="PluginIconImage"/>.
    /// Provides unified log detail formatting, filename sanitization, and Stopwatch scopes.
    /// </summary>
    public static class MediaControlDiagnostics
    {
        /// <summary>Phase: game data assignment.</summary>
        public const string PhaseSetData = "SetData";

        /// <summary>Phase: media path selection (random, auto, favorite, default).</summary>
        public const string PhaseMediaSelect = "MediaSelect";

        /// <summary>Phase: source assignment before decode/load.</summary>
        public const string PhaseSourceSet = "SourceSet";

        /// <summary>Phase: background image swap with optional fade.</summary>
        public const string PhaseLoadNewSource = "LoadNewSource";

        /// <summary>Phase: auto-changer or video-delay timer tick.</summary>
        public const string PhaseTimerTick = "TimerTick";

        /// <summary>Phase: theme property sync on settings window close.</summary>
        public const string PhaseThemeSync = "ThemeSync";

        /// <summary>Phase: adaptive blur application.</summary>
        public const string PhaseBlur = "Blur";

        /// <summary>Phase: generic performance anomaly.</summary>
        public const string PhasePerf = "Perf";

        /// <summary>Default slow-operation threshold (~one frame at 60 Hz).</summary>
        public const int SlowOperationThresholdMs = 16;

        /// <summary>Theme sync duration above which a Release-visible issue is logged.</summary>
        public const int ThemeSyncSlowThresholdMs = 100;

        /// <summary>
        /// Starts a timed scope. On dispose, logs trace with elapsed ms and issues when over threshold.
        /// </summary>
        /// <param name="phase">Diagnostic phase name (use <see cref="PhaseSetData"/> constants).</param>
        /// <param name="logTrace">Callback matching <c>LogControlTrace(phase, detail)</c>.</param>
        /// <param name="logIssue">Callback matching <c>LogControlIssue(message)</c>.</param>
        /// <param name="detail">Optional detail appended to the completion message.</param>
        /// <param name="slowThresholdMs">Duration in ms above which <paramref name="logIssue"/> is called.</param>
        public static MediaControlDiagnosticsScope BeginScope(
            string phase,
            Action<string, string> logTrace,
            Action<string> logIssue,
            string detail = null,
            int slowThresholdMs = SlowOperationThresholdMs)
        {
            if (string.IsNullOrEmpty(phase))
            {
                throw new ArgumentException("Phase is required.", nameof(phase));
            }

            if (logTrace == null)
            {
                throw new ArgumentNullException(nameof(logTrace));
            }

            if (logIssue == null)
            {
                throw new ArgumentNullException(nameof(logIssue));
            }

            return new MediaControlDiagnosticsScope(phase, logTrace, logIssue, detail, slowThresholdMs);
        }

        /// <summary>Logs a trace entry with unified detail formatting.</summary>
        public static void Trace(Action<string, string> logTrace, string phase, string detail)
        {
            if (logTrace == null)
            {
                throw new ArgumentNullException(nameof(logTrace));
            }

            logTrace(phase, detail);
        }

        /// <summary>Logs a Release-visible issue with the Perf phase prefix.</summary>
        public static void Issue(Action<string> logIssue, string phase, string detail)
        {
            if (logIssue == null)
            {
                throw new ArgumentNullException(nameof(logIssue));
            }

            logIssue(string.Format("{0} — {1}", phase ?? PhasePerf, detail ?? string.Empty));
        }

        /// <summary>Returns only the file name — never the full user path.</summary>
        public static string FormatFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "(null)";
            }

            try
            {
                if (Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && uri.IsFile)
                {
                    string localName = Path.GetFileName(uri.LocalPath);
                    if (!string.IsNullOrEmpty(localName))
                    {
                        return localName;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to Path.GetFileName.
            }

            string fileName = Path.GetFileName(path);
            return string.IsNullOrEmpty(fileName) ? path : fileName;
        }

        /// <summary>Compact game reference for diagnostic messages.</summary>
        public static string FormatGameRef(Game game)
        {
            if (game == null)
            {
                return "null";
            }

            return string.Format("'{0}' ({1})", game.Name ?? "?", game.Id);
        }

        /// <summary>Detail line for <see cref="PhaseMediaSelect"/>.</summary>
        public static string FormatMediaSelectDetail(Game game, string branch, int index, int total, string filePath)
        {
            return string.Format(
                "game={0}, branch={1}, index={2}/{3}, file={4}",
                FormatGameRef(game),
                branch ?? "?",
                index,
                total,
                FormatFileName(filePath));
        }

        /// <summary>Detail line for <see cref="PhaseSourceSet"/>.</summary>
        public static string FormatSourceSetDetail(string filePath, bool exists, bool isVideo, string dispatcherPriority = null)
        {
            if (string.IsNullOrEmpty(dispatcherPriority))
            {
                return string.Format(
                    "file={0}, exists={1}, isVideo={2}",
                    FormatFileName(filePath),
                    exists,
                    isVideo);
            }

            return string.Format(
                "file={0}, exists={1}, isVideo={2}, priority={3}",
                FormatFileName(filePath),
                exists,
                isVideo,
                dispatcherPriority);
        }

        /// <summary>Detail line for <see cref="PhaseLoadNewSource"/>.</summary>
        public static string FormatLoadNewSourceDetail(
            string oldFile,
            string newFile,
            bool skippedDuplicate,
            string fadeBranch,
            string currentImageSlot)
        {
            return string.Format(
                "old={0}, new={1}, skippedDuplicate={2}, fadeBranch={3}, slot={4}",
                FormatFileName(oldFile),
                FormatFileName(newFile),
                skippedDuplicate,
                fadeBranch ?? "?",
                currentImageSlot ?? "?");
        }

        /// <summary>Detail line for <see cref="PhaseTimerTick"/>.</summary>
        public static string FormatTimerTickDetail(string timerType, string filePath, bool lifecycleActive)
        {
            return string.Format(
                "timer={0}, path={1}, lifecycle={2}",
                timerType ?? "?",
                FormatFileName(filePath),
                lifecycleActive ? "active" : "inactive");
        }

        /// <summary>Detail line for <see cref="PhaseThemeSync"/> completion.</summary>
        public static string FormatThemeSyncDetail(long elapsedMs, int propsCopied, bool partFound, int partDepth)
        {
            return string.Format(
                "completed in {0}ms, props={1}, partFound={2}, partDepth={3}",
                elapsedMs,
                propsCopied,
                partFound,
                partDepth);
        }
    }

    /// <summary>
    /// Stopwatch scope that logs phase duration on dispose via <see cref="MediaControlDiagnostics"/>.
    /// </summary>
    public sealed class MediaControlDiagnosticsScope : IDisposable
    {
        private readonly string _phase;
        private readonly Action<string, string> _logTrace;
        private readonly Action<string> _logIssue;
        private readonly string _startDetail;
        private readonly int _slowThresholdMs;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        internal MediaControlDiagnosticsScope(
            string phase,
            Action<string, string> logTrace,
            Action<string> logIssue,
            string detail,
            int slowThresholdMs)
        {
            _phase = phase;
            _logTrace = logTrace;
            _logIssue = logIssue;
            _startDetail = detail;
            _slowThresholdMs = slowThresholdMs;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>Stops the timer and emits trace/issue logs.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopwatch.Stop();
            long elapsed = _stopwatch.ElapsedMilliseconds;

            string completionDetail = string.IsNullOrEmpty(_startDetail)
                ? string.Format("completed in {0}ms", elapsed)
                : string.Format("{0}, completed in {1}ms", _startDetail, elapsed);

            _logTrace(_phase, completionDetail);

            if (elapsed > _slowThresholdMs)
            {
                MediaControlDiagnostics.Issue(
                    _logIssue,
                    MediaControlDiagnostics.PhasePerf,
                    string.Format("{0} took {1}ms (threshold {2}ms)", _phase, elapsed, _slowThresholdMs));
            }
        }
    }
}
