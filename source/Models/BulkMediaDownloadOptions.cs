using BackgroundChanger.Models;
using BackgroundChanger.Services;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BackgroundChanger
{
    /// <summary>
    /// How SteamGridDB assets are selected from the filtered API pool for bulk download.
    /// Sorting is client-side; the API has no sort parameter.
    /// </summary>
    public enum BulkMediaPickMode
    {
        /// <summary>
        /// Pick randomly among filtered results.
        /// </summary>
        Random = 0,

        /// <summary>
        /// Prefer highest API <c>score</c>.
        /// </summary>
        BestScore = 1,

        /// <summary>
        /// Prefer highest API <c>upvotes</c>.
        /// </summary>
        BestUpvotes = 2,

        /// <summary>
        /// Prefer highest asset id (proxy for newest, same as the browse UI date sort).
        /// </summary>
        NewestById = 3
    }

    /// <summary>
    /// Which Playnite game list feeds the bulk download (replaces <c>OptionsDownloadData</c> source radios).
    /// </summary>
    public enum BulkGameSource
    {
        /// <summary>
        /// All non-hidden library games.
        /// </summary>
        All = 0,

        /// <summary>
        /// Current main-view filtered games.
        /// </summary>
        Filtered = 1,

        /// <summary>
        /// Current main-view selection.
        /// </summary>
        Selected = 2
    }

    /// <summary>
    /// Optional installation / favorite constraint for bulk game selection.
    /// </summary>
    public enum BulkGameInstallFilter
    {
        /// <summary>
        /// No installation filter.
        /// </summary>
        None = 0,

        /// <summary>
        /// Installed games only.
        /// </summary>
        Installed = 1,

        /// <summary>
        /// Uninstalled games only.
        /// </summary>
        NotInstalled = 2,

        /// <summary>
        /// Favorite games only.
        /// </summary>
        Favorite = 3
    }

    /// <summary>
    /// Optional time-based constraint for bulk game selection.
    /// </summary>
    public enum BulkGameTimeFilter
    {
        /// <summary>
        /// No time filter.
        /// </summary>
        None = 0,

        /// <summary>
        /// Games whose plugin data is older than <see cref="BulkMediaDownloadOptions.GameFilterMonths"/> months.
        /// </summary>
        OldData = 1,

        /// <summary>
        /// Games played within the last <see cref="BulkMediaDownloadOptions.GameFilterMonths"/> months.
        /// </summary>
        RecentlyPlayed = 2,

        /// <summary>
        /// Games added within the last <see cref="BulkMediaDownloadOptions.GameFilterMonths"/> months.
        /// </summary>
        RecentlyAdded = 3
    }

    /// <summary>
    /// Persisted and dialog options for bulk media download (Steam CDN + SteamGridDB).
    /// </summary>
    public class BulkMediaDownloadOptions
    {
        /// <summary>
        /// Default number of assets to import per media kind (total across sources).
        /// </summary>
        public const int DefaultQuantity = 5;

        /// <summary>
        /// Default FuzzySharp ratio threshold (aligned with <c>SteamApi</c> AppId resolution).
        /// </summary>
        public const int DefaultFuzzyThreshold = 90;

        /// <summary>
        /// Maximum <c>limit</c> accepted by the SteamGridDB API per page.
        /// </summary>
        public const int MaxSteamGridApiPageLimit = 50;

        /// <summary>
        /// Default month window for time-based game filters.
        /// </summary>
        public const int DefaultGameFilterMonths = 1;

        /// <summary>
        /// Gets or sets which Playnite game list is used as the bulk input set.
        /// </summary>
        public BulkGameSource GameSource { get; set; } = BulkGameSource.All;

        /// <summary>
        /// Gets or sets an optional installed / favorite constraint.
        /// </summary>
        public BulkGameInstallFilter InstallFilter { get; set; } = BulkGameInstallFilter.None;

        /// <summary>
        /// Gets or sets an optional time-based constraint.
        /// </summary>
        public BulkGameTimeFilter TimeFilter { get; set; } = BulkGameTimeFilter.None;

        /// <summary>
        /// Gets or sets the month window for <see cref="TimeFilter"/> (minimum 1).
        /// </summary>
        public int GameFilterMonths { get; set; } = DefaultGameFilterMonths;

        /// <summary>
        /// Gets or sets whether to keep only games that have no plugin media of any kind.
        /// Distinct from <see cref="OnlyMissing"/> (per-kind skip during import).
        /// </summary>
        public bool OnlyGamesWithoutPluginData { get; set; }

        /// <summary>
        /// Gets or sets whether Steam official CDN is used as a source.
        /// </summary>
        public bool EnableSteam { get; set; } = true;

        /// <summary>
        /// Gets or sets whether SteamGridDB is used as a source.
        /// </summary>
        public bool EnableSteamGridDb { get; set; } = true;

        /// <summary>
        /// Gets or sets whether backgrounds (heroes) are downloaded.
        /// </summary>
        public bool EnableBackground { get; set; } = true;

        /// <summary>
        /// Gets or sets whether covers (grids) are downloaded.
        /// </summary>
        public bool EnableCover { get; set; }

        /// <summary>
        /// Gets or sets whether icons are downloaded.
        /// </summary>
        public bool EnableIcon { get; set; }

        /// <summary>
        /// Gets or sets whether to skip a media kind when the game already has plugin images for that kind.
        /// Playnite-native media alone does not count as present.
        /// </summary>
        public bool OnlyMissing { get; set; } = true;

        /// <summary>
        /// Gets or sets the max assets to import per kind (total Steam then SGDB). Ignored when <see cref="DownloadAll"/> is true.
        /// </summary>
        public int Quantity { get; set; } = DefaultQuantity;

        /// <summary>
        /// Gets or sets whether to import the full filtered pool (paginated) instead of <see cref="Quantity"/>.
        /// </summary>
        public bool DownloadAll { get; set; }

        /// <summary>
        /// Gets or sets the FuzzySharp ratio threshold (0–100). Matches at or above are accepted automatically.
        /// </summary>
        public int FuzzyMatchThreshold { get; set; } = DefaultFuzzyThreshold;

        /// <summary>
        /// Gets or sets whether to prompt for a manual game match when the best score is below <see cref="FuzzyMatchThreshold"/>.
        /// When false, the game is skipped.
        /// </summary>
        public bool PromptOnLowFuzzyMatch { get; set; }

        /// <summary>
        /// Gets or sets whether animated SteamGridDB assets are allowed (opt-in; default static only).
        /// </summary>
        public bool AllowAnimated { get; set; }

        /// <summary>
        /// Gets or sets how assets are chosen from the SteamGridDB pool.
        /// </summary>
        public BulkMediaPickMode PickMode { get; set; } = BulkMediaPickMode.Random;

        /// <summary>
        /// Gets or sets SteamGridDB filters for backgrounds (heroes).
        /// </summary>
        public SteamGridFilters BackgroundFilters { get; set; }

        /// <summary>
        /// Gets or sets SteamGridDB filters for covers (grids).
        /// </summary>
        public SteamGridFilters CoverFilters { get; set; }

        /// <summary>
        /// Gets or sets SteamGridDB filters for icons.
        /// </summary>
        public SteamGridFilters IconFilters { get; set; }

        /// <summary>
        /// Creates default bulk options with static-only SteamGridDB filter slots.
        /// </summary>
        /// <returns>A new options instance ready for dialog or persistence.</returns>
        public static BulkMediaDownloadOptions CreateDefaults()
        {
            BulkMediaDownloadOptions options = new BulkMediaDownloadOptions();
            options.EnsureInitialized();
            return options;
        }

        /// <summary>
        /// Ensures filter slots exist, coerces quantity / fuzzy bounds, and applies the animated policy to type filters.
        /// </summary>
        public void EnsureInitialized()
        {
            if (BackgroundFilters == null)
            {
                BackgroundFilters = SteamGridFilterHelper.CreateDefaultFilters(SteamGridDbType.heroes);
            }
            else
            {
                BackgroundFilters = SteamGridFilterHelper.NormalizeFilters(BackgroundFilters, SteamGridDbType.heroes);
            }

            if (CoverFilters == null)
            {
                CoverFilters = SteamGridFilterHelper.CreateDefaultFilters(SteamGridDbType.grids);
            }
            else
            {
                CoverFilters = SteamGridFilterHelper.NormalizeFilters(CoverFilters, SteamGridDbType.grids);
            }

            if (IconFilters == null)
            {
                IconFilters = SteamGridFilterHelper.CreateDefaultFilters(SteamGridDbType.icons);
            }
            else
            {
                IconFilters = SteamGridFilterHelper.NormalizeFilters(IconFilters, SteamGridDbType.icons);
            }

            if (Quantity < 1)
            {
                Quantity = DefaultQuantity;
            }

            if (FuzzyMatchThreshold < 0)
            {
                FuzzyMatchThreshold = 0;
            }
            else if (FuzzyMatchThreshold > 100)
            {
                FuzzyMatchThreshold = 100;
            }

            if (GameFilterMonths < 1)
            {
                GameFilterMonths = DefaultGameFilterMonths;
            }

            if (!Enum.IsDefined(typeof(BulkMediaPickMode), PickMode))
            {
                PickMode = BulkMediaPickMode.Random;
            }

            if (!Enum.IsDefined(typeof(BulkGameSource), GameSource))
            {
                GameSource = BulkGameSource.All;
            }

            if (!Enum.IsDefined(typeof(BulkGameInstallFilter), InstallFilter))
            {
                InstallFilter = BulkGameInstallFilter.None;
            }

            if (!Enum.IsDefined(typeof(BulkGameTimeFilter), TimeFilter))
            {
                TimeFilter = BulkGameTimeFilter.None;
            }

            if (TimeFilter == BulkGameTimeFilter.OldData)
            {
                OnlyGamesWithoutPluginData = false;
            }

            ApplyAnimatedPolicyToFilters();
        }

        /// <summary>
        /// Returns a deep clone suitable for dialog editing without mutating persisted settings until save.
        /// </summary>
        /// <returns>Cloned options with initialized filter slots.</returns>
        public BulkMediaDownloadOptions Clone()
        {
            BulkMediaDownloadOptions clone = Serialization.GetClone(this) ?? CreateDefaults();
            clone.EnsureInitialized();
            return clone;
        }

        /// <summary>
        /// Resolves the SteamGridDB API <c>limit</c> for one request page from the current quantity settings.
        /// </summary>
        /// <returns>Value between 1 and <see cref="MaxSteamGridApiPageLimit"/>.</returns>
        public int ResolveSteamGridApiLimit()
        {
            if (DownloadAll)
            {
                return MaxSteamGridApiPageLimit;
            }

            int quantity = Quantity < 1 ? DefaultQuantity : Quantity;
            return Math.Min(quantity, MaxSteamGridApiPageLimit);
        }

        /// <summary>
        /// Returns the filter bucket for the given plugin media kind.
        /// </summary>
        /// <param name="mediaKind">Background, cover, or icon.</param>
        /// <returns>The corresponding <see cref="SteamGridFilters"/> instance.</returns>
        public SteamGridFilters GetFilters(BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            EnsureInitialized();

            switch (mediaKind)
            {
                case BackgroundChangerDatabase.PluginMediaKind.Cover:
                    return CoverFilters;
                case BackgroundChangerDatabase.PluginMediaKind.Icon:
                    return IconFilters;
                case BackgroundChangerDatabase.PluginMediaKind.Background:
                default:
                    return BackgroundFilters;
            }
        }

        /// <summary>
        /// Sets the filter bucket for the given plugin media kind.
        /// </summary>
        /// <param name="mediaKind">Background, cover, or icon.</param>
        /// <param name="filters">Filters to store; null is ignored.</param>
        public void SetFilters(BackgroundChangerDatabase.PluginMediaKind mediaKind, SteamGridFilters filters)
        {
            if (filters == null)
            {
                return;
            }

            switch (mediaKind)
            {
                case BackgroundChangerDatabase.PluginMediaKind.Cover:
                    CoverFilters = filters;
                    break;
                case BackgroundChangerDatabase.PluginMediaKind.Icon:
                    IconFilters = filters;
                    break;
                case BackgroundChangerDatabase.PluginMediaKind.Background:
                default:
                    BackgroundFilters = filters;
                    break;
            }
        }

        /// <summary>
        /// Lists media kinds enabled for this bulk run.
        /// </summary>
        /// <returns>Enabled kinds in Background, Cover, Icon order.</returns>
        public IReadOnlyList<BackgroundChangerDatabase.PluginMediaKind> GetEnabledMediaKinds()
        {
            List<BackgroundChangerDatabase.PluginMediaKind> kinds = new List<BackgroundChangerDatabase.PluginMediaKind>(3);
            if (EnableBackground)
            {
                kinds.Add(BackgroundChangerDatabase.PluginMediaKind.Background);
            }

            if (EnableCover)
            {
                kinds.Add(BackgroundChangerDatabase.PluginMediaKind.Cover);
            }

            if (EnableIcon)
            {
                kinds.Add(BackgroundChangerDatabase.PluginMediaKind.Icon);
            }

            return kinds;
        }

        /// <summary>
        /// Returns whether at least one download source is enabled.
        /// </summary>
        public bool HasAnySource => EnableSteam || EnableSteamGridDb;

        /// <summary>
        /// Returns whether at least one media kind is enabled.
        /// </summary>
        public bool HasAnyMediaKind => EnableBackground || EnableCover || EnableIcon;

        private void ApplyAnimatedPolicyToFilters()
        {
            ApplyAnimatedPolicy(BackgroundFilters);
            ApplyAnimatedPolicy(CoverFilters);
            ApplyAnimatedPolicy(IconFilters);
        }

        private void ApplyAnimatedPolicy(SteamGridFilters filters)
        {
            if (filters?.CheckTypes == null)
            {
                return;
            }

            foreach (CheckData type in filters.CheckTypes.Where(x => x != null))
            {
                if (string.Equals(type.Data, "static", StringComparison.OrdinalIgnoreCase))
                {
                    type.IsChecked = true;
                }
                else if (string.Equals(type.Data, "animated", StringComparison.OrdinalIgnoreCase))
                {
                    type.IsChecked = AllowAnimated;
                }
            }
        }
    }
}
