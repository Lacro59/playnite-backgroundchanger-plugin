using System;

namespace BackgroundChanger.Models
{
    /// <summary>
    /// Shared default values for animated media conversion to MP4.
    /// </summary>
    public class MediaConversionDefaults
    {
        public const int DefaultCrf = 23;
        public const int DefaultFramerate = 25;
        public const int MinCrf = 0;
        public const int MaxCrf = 51;
        public const int MinFramerate = 1;
        public const int MaxFramerate = 120;

        /// <summary>
        /// Gets or sets the default H.264 CRF used when a format-specific CRF is not set (0).
        /// </summary>
        public int Crf { get; set; } = DefaultCrf;
    }

    /// <summary>
    /// Per-format conversion options. A CRF of 0 inherits <see cref="MediaConversionDefaults.Crf"/>.
    /// </summary>
    public class MediaConversionFormatSettings
    {
        /// <summary>
        /// Gets or sets the H.264 CRF for this format. 0 inherits the default CRF.
        /// </summary>
        public int Crf { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether the frame rate is derived from the source animation metadata.
        /// </summary>
        public bool UseAutoFramerate { get; set; } = true;

        /// <summary>
        /// Gets or sets the fixed output frame rate when <see cref="UseAutoFramerate"/> is false.
        /// </summary>
        public int FixedFramerate { get; set; } = MediaConversionDefaults.DefaultFramerate;
    }

    /// <summary>
    /// WebM-specific conversion options.
    /// </summary>
    public class MediaConversionWebmSettings : MediaConversionFormatSettings
    {
        /// <summary>
        /// Gets or sets whether WebM is always re-encoded to H.264. When false, FFmpeg attempts stream copy.
        /// </summary>
        public bool ForceReencode { get; set; } = true;
    }

    /// <summary>
    /// Plugin settings for animated media conversion grouped by input format.
    /// </summary>
    public class MediaConversionSettings
    {
        /// <summary>
        /// Gets or sets shared conversion defaults.
        /// </summary>
        public MediaConversionDefaults Defaults { get; set; } = new MediaConversionDefaults();

        /// <summary>
        /// Gets or sets options for animated WebP (frame export + FFmpeg).
        /// </summary>
        public MediaConversionFormatSettings AnimatedWebp { get; set; } = new MediaConversionFormatSettings();

        /// <summary>
        /// Gets or sets options for WebM inputs.
        /// </summary>
        public MediaConversionWebmSettings Webm { get; set; } = new MediaConversionWebmSettings();

        /// <summary>
        /// Gets or sets options for animated PNG (APNG).
        /// </summary>
        public MediaConversionFormatSettings Apng { get; set; } = new MediaConversionFormatSettings();

        /// <summary>
        /// Gets or sets options for animated GIF.
        /// </summary>
        public MediaConversionFormatSettings Gif { get; set; } = new MediaConversionFormatSettings();

        /// <summary>
        /// Resolves the effective CRF for a format block.
        /// </summary>
        /// <param name="formatSettings">Format-specific settings.</param>
        /// <returns>Clamped CRF value.</returns>
        public int ResolveCrf(MediaConversionFormatSettings formatSettings)
        {
            if (formatSettings != null && formatSettings.Crf > 0)
            {
                return ClampCrf(formatSettings.Crf);
            }

            int defaultCrf = Defaults != null ? Defaults.Crf : MediaConversionDefaults.DefaultCrf;
            if (defaultCrf <= 0)
            {
                defaultCrf = MediaConversionDefaults.DefaultCrf;
            }

            return ClampCrf(defaultCrf);
        }

        /// <summary>
        /// Resolves the output frame rate for formats that support manual override.
        /// </summary>
        /// <param name="formatSettings">Format-specific settings.</param>
        /// <param name="autoFramerate">Frame rate derived from source metadata when auto mode is enabled.</param>
        /// <returns>Clamped frame rate.</returns>
        public int ResolveFramerate(MediaConversionFormatSettings formatSettings, int autoFramerate)
        {
            if (formatSettings != null && !formatSettings.UseAutoFramerate)
            {
                return ClampFramerate(formatSettings.FixedFramerate);
            }

            return ClampFramerate(autoFramerate);
        }

        /// <summary>
        /// Clamps a CRF value to the supported x264 range.
        /// </summary>
        /// <param name="crf">Requested CRF.</param>
        /// <returns>Clamped CRF.</returns>
        public static int ClampCrf(int crf)
        {
            if (crf < MediaConversionDefaults.MinCrf)
            {
                return MediaConversionDefaults.MinCrf;
            }

            if (crf > MediaConversionDefaults.MaxCrf)
            {
                return MediaConversionDefaults.MaxCrf;
            }

            return crf;
        }

        /// <summary>
        /// Clamps a frame rate to the supported conversion range.
        /// </summary>
        /// <param name="framerate">Requested frame rate.</param>
        /// <returns>Clamped frame rate.</returns>
        public static int ClampFramerate(int framerate)
        {
            if (framerate < MediaConversionDefaults.MinFramerate)
            {
                return MediaConversionDefaults.MinFramerate;
            }

            if (framerate > MediaConversionDefaults.MaxFramerate)
            {
                return MediaConversionDefaults.MaxFramerate;
            }

            return framerate;
        }

        /// <summary>
        /// Returns whether the given value is a valid default CRF (not inherit).
        /// </summary>
        /// <param name="crf">Default CRF to validate.</param>
        /// <returns><c>true</c> when the value is within the supported range.</returns>
        public static bool IsValidDefaultCrf(int crf)
        {
            return crf >= MediaConversionDefaults.MinCrf && crf <= MediaConversionDefaults.MaxCrf;
        }

        /// <summary>
        /// Returns whether the given CRF is valid for defaults or a format override (0 means inherit).
        /// </summary>
        /// <param name="crf">CRF to validate.</param>
        /// <returns><c>true</c> when the value is 0 or within the supported range.</returns>
        public static bool IsValidCrf(int crf)
        {
            return crf == 0 || (crf >= MediaConversionDefaults.MinCrf && crf <= MediaConversionDefaults.MaxCrf);
        }

        /// <summary>
        /// Returns whether the given frame rate is within the supported range.
        /// </summary>
        /// <param name="framerate">Frame rate to validate.</param>
        /// <returns><c>true</c> when the value is valid.</returns>
        public static bool IsValidFramerate(int framerate)
        {
            return framerate >= MediaConversionDefaults.MinFramerate && framerate <= MediaConversionDefaults.MaxFramerate;
        }
    }
}
