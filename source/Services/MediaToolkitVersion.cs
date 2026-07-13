using System;
using System.Text.RegularExpressions;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Parsed semantic version of an FFmpeg or ffprobe executable.
    /// </summary>
    public struct MediaToolkitVersion
    {
        private static readonly Regex VersionLineRegex = new Regex(
            @"version\s+(\d+)\.(\d+)(?:\.(\d+))?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// Minimum FFmpeg/ffprobe version required for animated WebP decode via libavcodec libwebp.
        /// </summary>
        public static readonly MediaToolkitVersion MinimumForAnimatedWebp = new MediaToolkitVersion(7, 2, 0);

        /// <summary>
        /// Initializes a new semantic version.
        /// </summary>
        /// <param name="major">Major version component.</param>
        /// <param name="minor">Minor version component.</param>
        /// <param name="patch">Patch version component.</param>
        public MediaToolkitVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Gets the major version component.
        /// </summary>
        public int Major { get; }

        /// <summary>
        /// Gets the minor version component.
        /// </summary>
        public int Minor { get; }

        /// <summary>
        /// Gets the patch version component.
        /// </summary>
        public int Patch { get; }

        /// <summary>
        /// Determines whether this version is greater than or equal to <paramref name="other"/>.
        /// </summary>
        /// <param name="other">Version to compare against.</param>
        /// <returns><c>true</c> when this version meets or exceeds <paramref name="other"/>.</returns>
        public bool IsAtLeast(MediaToolkitVersion other)
        {
            if (Major != other.Major)
            {
                return Major > other.Major;
            }

            if (Minor != other.Minor)
            {
                return Minor > other.Minor;
            }

            return Patch >= other.Patch;
        }

        /// <summary>
        /// Attempts to parse a semantic version from FFmpeg/ffprobe <c>-version</c> output.
        /// </summary>
        /// <param name="versionOutput">Combined stdout and stderr from a <c>-version</c> invocation.</param>
        /// <param name="version">Parsed version when successful.</param>
        /// <returns><c>true</c> when a numeric release version was parsed.</returns>
        public static bool TryParseFromVersionOutput(string versionOutput, out MediaToolkitVersion version)
        {
            version = default(MediaToolkitVersion);

            if (string.IsNullOrWhiteSpace(versionOutput))
            {
                return false;
            }

            Match match = VersionLineRegex.Match(versionOutput);
            if (!match.Success)
            {
                return false;
            }

            int major = int.Parse(match.Groups[1].Value);
            int minor = int.Parse(match.Groups[2].Value);
            int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            version = new MediaToolkitVersion(major, minor, patch);
            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0}.{1}.{2}", Major, Minor, Patch);
        }
    }
}
