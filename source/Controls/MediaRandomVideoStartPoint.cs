using System;
using System.Windows.Controls;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// Seeks a <see cref="MediaElement"/> to a random start position after media open.
    /// Subscribe from <see cref="MediaElement.MediaOpened"/> so loops via
    /// <c>MediaElementBehaviors.Repeat</c> (restart at zero on <c>MediaEnded</c>) are not re-randomized.
    /// </summary>
    public static class MediaRandomVideoStartPoint
    {
        /// <summary>Trace outcome when a random seek was applied.</summary>
        public const string OutcomeApplied = "applied";

        /// <summary>Trace outcome when the media element was null.</summary>
        public const string OutcomeSkippedNull = "skipped null";

        /// <summary>Trace outcome when natural duration is unknown.</summary>
        public const string OutcomeSkippedNoDuration = "skipped no-duration";

        /// <summary>Trace outcome when duration is too short to seek safely (&lt;= 1s).</summary>
        public const string OutcomeSkippedTooShort = "skipped too-short";

        /// <summary>Trace outcome when the settings flag is off.</summary>
        public const string OutcomeSkippedSettingOff = "skipped setting=off";

        private static readonly Random SharedRandom = new Random();

        /// <summary>
        /// Builds a stable smoke-test detail line for <c>video-start</c> traces.
        /// </summary>
        /// <param name="kind"><c>background</c> or <c>cover</c>.</param>
        /// <param name="outcome">Outcome constant (applied / skipped …).</param>
        /// <param name="positionSeconds">Seek position in seconds (0 when not applied).</param>
        /// <param name="durationSeconds">Natural duration in seconds (0 when unknown).</param>
        /// <returns>Formatted detail string.</returns>
        public static string FormatDetail(string kind, string outcome, double positionSeconds, double durationSeconds)
        {
            return string.Format(
                "kind={0}, {1}, position={2:F1}s, duration={3:F1}s",
                kind ?? "(null)",
                outcome ?? "(null)",
                positionSeconds,
                durationSeconds);
        }

        /// <summary>
        /// Attempts to seek <paramref name="video"/> to a random position in
        /// <c>[0, duration - 1s)</c> when natural duration is known and longer than one second.
        /// </summary>
        /// <param name="video">Opened media element; may be null.</param>
        /// <param name="random">Optional random source; uses a shared instance when null.</param>
        /// <param name="outcome">Applied or skipped reason for diagnostics.</param>
        /// <param name="positionSeconds">Seek offset when applied; otherwise 0.</param>
        /// <param name="durationSeconds">Natural duration when known; otherwise 0.</param>
        /// <returns><c>true</c> when a seek was applied; otherwise <c>false</c>.</returns>
        public static bool TrySeekRandom(
            MediaElement video,
            Random random,
            out string outcome,
            out double positionSeconds,
            out double durationSeconds)
        {
            positionSeconds = 0;
            durationSeconds = 0;

            if (video == null)
            {
                outcome = OutcomeSkippedNull;
                return false;
            }

            if (!video.NaturalDuration.HasTimeSpan)
            {
                outcome = OutcomeSkippedNoDuration;
                return false;
            }

            durationSeconds = video.NaturalDuration.TimeSpan.TotalSeconds;
            if (durationSeconds <= 1)
            {
                outcome = OutcomeSkippedTooShort;
                return false;
            }

            Random rng = random ?? SharedRandom;
            double maxSeconds = durationSeconds - 1;
            positionSeconds = rng.NextDouble() * maxSeconds;
            video.Position = TimeSpan.FromSeconds(positionSeconds);
            outcome = OutcomeApplied;
            return true;
        }
    }
}
