using BackgroundChanger.Services;

namespace BackgroundChanger.Models
{
    /// <summary>
    /// Named Steam CDN / product-info asset used for official media import.
    /// </summary>
    public enum SteamOfficialAssetKind
    {
        /// <summary>Library hero banner (<c>library_hero.jpg</c>).</summary>
        LibraryHero,

        /// <summary>High-resolution library hero (<c>library_hero_2x.jpg</c>).</summary>
        LibraryHero2x,

        /// <summary>Store page background (<c>page_bg_generated_v6b.jpg</c>).</summary>
        PageBgGeneratedV6b,

        /// <summary>Legacy store page background (<c>page.bg.jpg</c>).</summary>
        PageBg,

        /// <summary>Generated store page background (<c>page_bg_generated.jpg</c>).</summary>
        PageBgGenerated,

        /// <summary>Vertical library cover 2× (<c>library_600x900_2x.jpg</c>).</summary>
        Library600x9002x,

        /// <summary>Vertical library cover (<c>library_600x900.jpg</c>).</summary>
        Library600x900,

        /// <summary>Store header (<c>header.jpg</c>).</summary>
        Header,

        /// <summary>Client icon from product info (<c>.ico</c>).</summary>
        ClientIcon,

        /// <summary>Fallback community icon from product info (<c>.jpg</c>).</summary>
        CommunityIcon
    }

    /// <summary>
    /// Candidate URL for importing an official Steam media asset.
    /// </summary>
    public sealed class SteamOfficialMediaCandidate
    {
        /// <summary>
        /// Gets or sets a stable identifier (asset kind name).
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets a user-facing label (localized later when wired to UI).
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the absolute HTTP(S) URL of the asset.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the plugin media kind this candidate targets.
        /// </summary>
        public BackgroundChangerDatabase.PluginMediaKind MediaKind { get; set; }

        /// <summary>
        /// Gets or sets the Steam asset kind.
        /// </summary>
        public SteamOfficialAssetKind AssetKind { get; set; }

        /// <summary>
        /// Gets or sets the image width in pixels when probed.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the image height in pixels when probed.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the MIME subtype (e.g. <c>jpeg</c>, <c>ico</c>).
        /// </summary>
        public string Mime { get; set; }

        /// <summary>
        /// Gets a compact metadata label for the selection grid (dimensions and format).
        /// </summary>
        public string MetadataDisplay
        {
            get
            {
                bool hasDimensions = Width > 0 && Height > 0;
                string format = FormatDisplay;
                if (hasDimensions && !string.IsNullOrEmpty(format))
                {
                    return string.Format("{0} x {1} - {2}", Width, Height, format);
                }

                if (hasDimensions)
                {
                    return string.Format("{0} x {1}", Width, Height);
                }

                return format;
            }
        }

        /// <summary>
        /// Gets the upper-case format label derived from <see cref="Mime"/>.
        /// </summary>
        public string FormatDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Mime))
                {
                    return null;
                }

                return Mime.Trim().ToUpperInvariant();
            }
        }
    }
}
