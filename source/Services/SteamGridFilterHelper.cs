using BackgroundChanger;
using BackgroundChanger.Models;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using SteamGridFilters = BackgroundChanger.SteamGridFilters;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Persisted SteamGridDB filter bucket (one slot per <see cref="BackgroundChangerDatabase.PluginMediaKind"/>).
    /// </summary>
    public enum SteamGridFilterSlot
    {
        /// <summary>Cover art — API grids.</summary>
        Grids,

        /// <summary>Background heroes — API heroes.</summary>
        Heroes,

        /// <summary>Game icons — API icons.</summary>
        Icons
    }

    /// <summary>
    /// Builds default SteamGridDB filter lists and merges persisted selections without duplicates.
    /// </summary>
    public static class SteamGridFilterHelper
    {
        /// <summary>
        /// Maps a plugin media kind to the SteamGridDB API asset type.
        /// </summary>
        /// <param name="mediaKind">Background, cover, or icon context.</param>
        /// <returns>The corresponding <see cref="SteamGridDbType"/>.</returns>
        public static SteamGridDbType ResolveApiType(BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            switch (mediaKind)
            {
                case BackgroundChangerDatabase.PluginMediaKind.Background:
                    return SteamGridDbType.heroes;
                case BackgroundChangerDatabase.PluginMediaKind.Cover:
                    return SteamGridDbType.grids;
                case BackgroundChangerDatabase.PluginMediaKind.Icon:
                    return SteamGridDbType.icons;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mediaKind), mediaKind, null);
            }
        }

        /// <summary>
        /// Maps a plugin media kind to the settings filter slot used for load/save.
        /// </summary>
        /// <param name="mediaKind">Background, cover, or icon context.</param>
        /// <returns>The persisted filter bucket identifier.</returns>
        public static SteamGridFilterSlot ResolveFilterSlot(BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            switch (mediaKind)
            {
                case BackgroundChangerDatabase.PluginMediaKind.Background:
                    return SteamGridFilterSlot.Heroes;
                case BackgroundChangerDatabase.PluginMediaKind.Cover:
                    return SteamGridFilterSlot.Grids;
                case BackgroundChangerDatabase.PluginMediaKind.Icon:
                    return SteamGridFilterSlot.Icons;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mediaKind), mediaKind, null);
            }
        }

        /// <summary>
        /// Maps a filter slot to the SteamGridDB API asset type.
        /// </summary>
        /// <param name="filterSlot">Persisted filter bucket.</param>
        /// <returns>The corresponding <see cref="SteamGridDbType"/>.</returns>
        public static SteamGridDbType ResolveApiType(SteamGridFilterSlot filterSlot)
        {
            switch (filterSlot)
            {
                case SteamGridFilterSlot.Heroes:
                    return SteamGridDbType.heroes;
                case SteamGridFilterSlot.Grids:
                    return SteamGridDbType.grids;
                case SteamGridFilterSlot.Icons:
                    return SteamGridDbType.icons;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filterSlot), filterSlot, null);
            }
        }

        /// <summary>
        /// Returns the canonical default filter lists for the given SteamGridDB asset type.
        /// </summary>
        /// <param name="steamGridDbType">Heroes, grids, or icons.</param>
        public static SteamGridFilters CreateDefaultFilters(SteamGridDbType steamGridDbType)
        {
            switch (steamGridDbType)
            {
                case SteamGridDbType.heroes:
                    return CreateDefaultHeroesFilters();
                case SteamGridDbType.icons:
                    return CreateDefaultIconsFilters();
                case SteamGridDbType.grids:
                    return CreateDefaultGridsFilters();
                default:
                    throw new ArgumentOutOfRangeException(nameof(steamGridDbType), steamGridDbType, null);
            }
        }

        /// <summary>
        /// Ensures all SteamGridDB filter slots exist after settings load (migration from two-slot JSON).
        /// </summary>
        /// <param name="settings">Plugin settings instance.</param>
        public static void EnsureFilterSlots(BackgroundChangerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.SgIconsFilters == null)
            {
                settings.SgIconsFilters = CreateDefaultFilters(SteamGridDbType.icons);
                Common.LogDebug(true, "[SteamGridFilterHelper] Migrated SgIconsFilters to icon defaults (legacy two-slot settings JSON)");
            }
        }

        /// <summary>
        /// Gets the persisted filter bucket for the given slot.
        /// </summary>
        /// <param name="settings">Plugin settings instance.</param>
        /// <param name="filterSlot">Grids, heroes, or icons slot.</param>
        public static SteamGridFilters GetFilters(BackgroundChangerSettings settings, SteamGridFilterSlot filterSlot)
        {
            EnsureFilterSlots(settings);

            switch (filterSlot)
            {
                case SteamGridFilterSlot.Heroes:
                    return settings.SgHeroesFilters;
                case SteamGridFilterSlot.Grids:
                    return settings.SgGridsFilters;
                case SteamGridFilterSlot.Icons:
                    return settings.SgIconsFilters;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filterSlot), filterSlot, null);
            }
        }

        /// <summary>
        /// Assigns the persisted filter bucket for the given slot.
        /// </summary>
        /// <param name="settings">Plugin settings instance.</param>
        /// <param name="filterSlot">Grids, heroes, or icons slot.</param>
        /// <param name="filters">Filter lists to persist.</param>
        public static void SetFilters(BackgroundChangerSettings settings, SteamGridFilterSlot filterSlot, SteamGridFilters filters)
        {
            EnsureFilterSlots(settings);

            switch (filterSlot)
            {
                case SteamGridFilterSlot.Heroes:
                    settings.SgHeroesFilters = filters;
                    break;
                case SteamGridFilterSlot.Grids:
                    settings.SgGridsFilters = filters;
                    break;
                case SteamGridFilterSlot.Icons:
                    settings.SgIconsFilters = filters;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filterSlot), filterSlot, null);
            }
        }

        /// <summary>
        /// Merges persisted selections into the canonical default list, removing duplicate entries by <see cref="CheckData.Data"/>.
        /// </summary>
        public static List<CheckData> MergeWithDefaults(List<CheckData> saved, List<CheckData> defaults)
        {
            if (defaults == null || defaults.Count == 0)
            {
                return DedupeByData(saved);
            }

            Dictionary<string, bool> savedStates = DedupeByData(saved)
                .Where(x => !string.IsNullOrEmpty(x.Data))
                .ToDictionary(x => x.Data, x => x.IsChecked);

            return defaults.Select(defaultItem => new CheckData
            {
                Name = defaultItem.Name,
                Data = defaultItem.Data,
                IsChecked = savedStates.ContainsKey(defaultItem.Data)
                    ? savedStates[defaultItem.Data]
                    : defaultItem.IsChecked
            }).ToList();
        }

        /// <summary>
        /// Returns a deep clone of the filter lists with duplicate <see cref="CheckData.Data"/> values removed.
        /// </summary>
        public static SteamGridFilters NormalizeFilters(SteamGridFilters saved, SteamGridDbType steamGridDbType)
        {
            SteamGridFilters defaults = CreateDefaultFilters(steamGridDbType);

            return new SteamGridFilters
            {
                CheckDimensions = MergeWithDefaults(saved?.CheckDimensions, defaults.CheckDimensions),
                CheckStyles = MergeWithDefaults(saved?.CheckStyles, defaults.CheckStyles),
                CheckTypes = MergeWithDefaults(saved?.CheckTypes, defaults.CheckTypes),
                CheckTags = MergeWithDefaults(saved?.CheckTags, defaults.CheckTags),
                CheckMimes = defaults.CheckMimes != null
                    ? MergeWithDefaults(saved?.CheckMimes, defaults.CheckMimes)
                    : null,
                SortByDateAsc = saved?.SortByDateAsc ?? defaults.SortByDateAsc
            };
        }

        /// <summary>
        /// Builds the query string for a SteamGridDB asset search from active UI filter selections.
        /// </summary>
        /// <param name="assetType">Grids, heroes, or icons.</param>
        /// <param name="filters">Active filter lists (checked items are serialized).</param>
        /// <param name="page">Pagination index.</param>
        public static string BuildSearchQueryString(SteamGridDbType assetType, SteamGridFilters filters, int page)
        {
            return BuildSearchQueryString(assetType, filters, page, limit: null);
        }

        /// <summary>
        /// Builds the query string for a SteamGridDB asset search, optionally capping page size with <c>limit</c>.
        /// </summary>
        /// <param name="assetType">Grids, heroes, or icons.</param>
        /// <param name="filters">Active filter lists (checked items are serialized).</param>
        /// <param name="page">Pagination index.</param>
        /// <param name="limit">Optional API <c>limit</c> (clamped 1–50). Null keeps the API default.</param>
        public static string BuildSearchQueryString(SteamGridDbType assetType, SteamGridFilters filters, int page, int? limit)
        {
            if (filters == null)
            {
                filters = CreateDefaultFilters(assetType);
            }

            StringBuilder query = new StringBuilder();

            AppendQueryParam(query, "dimensions", JoinCheckedData(filters.CheckDimensions));
            AppendQueryParam(query, "styles", JoinCheckedData(filters.CheckStyles));

            string mimes = assetType == SteamGridDbType.icons
                ? JoinCheckedData(filters.CheckMimes)
                : "image/png,image/webp,image/jpeg";
            AppendQueryParam(query, "mimes", mimes);

            string types = JoinCheckedData(filters.CheckTypes);
            AppendQueryParam(query, "types", string.IsNullOrEmpty(types) ? "static,animated" : types);

            // Tag OR-logic stays client-side; broad fetch matches legacy behaviour.
            AppendQueryParam(query, "nsfw", "any");
            AppendQueryParam(query, "humor", "any");
            AppendQueryParam(query, "epilepsy", "any");
            AppendQueryParam(query, "page", page.ToString());

            if (limit.HasValue)
            {
                int clamped = Math.Max(1, Math.Min(limit.Value, BulkMediaDownloadOptions.MaxSteamGridApiPageLimit));
                AppendQueryParam(query, "limit", clamped.ToString());
            }

            return query.ToString();
        }

        /// <summary>
        /// Applies client-side tag OR-filter and static/animated edge cases (same rules as <c>SteamGridDbView</c>).
        /// </summary>
        /// <param name="results">Raw API results; may be null.</param>
        /// <param name="filters">Active filters; null returns an empty list.</param>
        /// <returns>Filtered list (never null).</returns>
        public static List<SteamGridDbResult> ApplyClientSideFilters(IEnumerable<SteamGridDbResult> results, SteamGridFilters filters)
        {
            List<SteamGridDbResult> filtered = results?.Where(x => x != null).ToList() ?? new List<SteamGridDbResult>();
            if (filters == null || filtered.Count == 0)
            {
                return filtered;
            }

            List<CheckData> listTags = filters.CheckTags;
            bool humor = IsFilterChecked(listTags, "Humor");
            bool nsfw = IsFilterChecked(listTags, "Adult Content");
            bool epilepsy = IsFilterChecked(listTags, "Epilepsy");
            bool untagged = IsFilterChecked(listTags, "Untagged");
            filtered = filtered
                .Where(x => (humor && x.Humor) || (nsfw && x.Nsfw) || (epilepsy && x.Epilepsy) || (untagged && x.Untagged))
                .ToList();

            List<CheckData> listTypes = filters.CheckTypes;
            bool staticChecked = IsFilterChecked(listTypes, "static");
            bool animatedChecked = IsFilterChecked(listTypes, "animated");
            if (staticChecked && !animatedChecked)
            {
                filtered = filtered.Where(x => !x.IsAnimated).ToList();
            }
            else if (!staticChecked && animatedChecked)
            {
                filtered = filtered.Where(x => x.IsAnimated).ToList();
            }

            return filtered;
        }

        private static bool IsFilterChecked(List<CheckData> items, string dataValue)
        {
            if (items == null || string.IsNullOrEmpty(dataValue))
            {
                return false;
            }

            CheckData match = items.FirstOrDefault(x =>
                x != null && string.Equals(x.Data, dataValue, StringComparison.OrdinalIgnoreCase));
            return match != null && match.IsChecked;
        }

        /// <summary>
        /// Compact summary of checked filter values for debug logging during smoke tests.
        /// </summary>
        public static string BuildActiveFiltersDebugSummary(SteamGridFilters filters)
        {
            if (filters == null)
            {
                return "filters=null";
            }

            return string.Format(
                "dimensions=[{0}] styles=[{1}] types=[{2}] mimes=[{3}] tags=[{4}]",
                JoinCheckedData(filters.CheckDimensions) ?? "-",
                JoinCheckedData(filters.CheckStyles) ?? "-",
                JoinCheckedData(filters.CheckTypes) ?? "-",
                JoinCheckedData(filters.CheckMimes) ?? "-",
                JoinCheckedData(filters.CheckTags) ?? "-");
        }

        /// <summary>
        /// Returns the API resource segment for the given asset type (<c>grids</c>, <c>heroes</c>, <c>icons</c>).
        /// </summary>
        public static string ResolveApiResource(SteamGridDbType assetType)
        {
            switch (assetType)
            {
                case SteamGridDbType.grids:
                    return "grids";
                case SteamGridDbType.heroes:
                    return "heroes";
                case SteamGridDbType.icons:
                    return "icons";
                default:
                    throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null);
            }
        }

        private static void AppendQueryParam(StringBuilder query, string name, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (query.Length > 0)
            {
                query.Append('&');
            }

            query.Append(name);
            query.Append('=');
            query.Append(WebUtility.UrlEncode(value));
        }

        private static string JoinCheckedData(List<CheckData> items)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            List<string> values = items
                .Where(x => x.IsChecked && !string.IsNullOrEmpty(x.Data))
                .Select(x => x.Data)
                .ToList();

            return values.Count > 0 ? string.Join(",", values) : null;
        }

        /// <summary>
        /// Builds a comma-separated summary for the filter dropdown header.
        /// </summary>
        /// <param name="items">Filter items.</param>
        /// <param name="useShortSummary">When true, dimensions show only the resolution segment.</param>
        public static string BuildSummary(IEnumerable<CheckData> items, bool useShortSummary)
        {
            if (items == null)
            {
                return string.Empty;
            }

            IEnumerable<CheckData> checkedItems = items.Where(x => x.IsChecked);
            if (useShortSummary)
            {
                return string.Join(", ", checkedItems.Select(GetDimensionSummary));
            }

            return string.Join(", ", checkedItems.Select(x => x.Name));
        }

        private static SteamGridFilters CreateDefaultHeroesFilters()
        {
            return new SteamGridFilters
            {
                CheckDimensions = new List<CheckData>
                {
                    new CheckData { Name = "Steam - 96:31 - 1920x620", Data = "1920x620" },
                    new CheckData { Name = "Steam - 96:31 - 3840x1240", Data = "3840x1240" },
                    new CheckData { Name = "Galaxy 2.0 - 32:13 - 1600x650", Data = "1600x650" }
                },
                CheckStyles = new List<CheckData>
                {
                    new CheckData { Name = "Alternate", Data = "alternate" },
                    new CheckData { Name = "Material", Data = "material" },
                    new CheckData { Name = "Blurred", Data = "blurred" }
                },
                CheckTypes = new List<CheckData>
                {
                    new CheckData { Name = "Static", Data = "static" },
                    new CheckData { Name = "Animated", Data = "animated" }
                },
                CheckTags = CreateDefaultTags()
            };
        }

        private static SteamGridFilters CreateDefaultGridsFilters()
        {
            return new SteamGridFilters
            {
                CheckDimensions = new List<CheckData>
                {
                    new CheckData { Name = "Steam Vertical - 2:3 - 600x900", Data = "600x900" },
                    new CheckData { Name = "Steam Horizontal - 92:43 - 920x430", Data = "920x430" },
                    new CheckData { Name = "Steam Horizontal - 92:43 - 460x215", Data = "460x215" },
                    new CheckData { Name = "Square - 1:1 - 1024x1024", Data = "1024x1024" },
                    new CheckData { Name = "Square - 1:1 - 512x512", Data = "512x512" },
                    new CheckData { Name = "Galaxy 2.0 - 22:31 - 660x930", Data = "660x930" },
                    new CheckData { Name = "Galaxy 2.0 - 22:31 - 342x482", Data = "342x482" }
                },
                CheckStyles = new List<CheckData>
                {
                    new CheckData { Name = "Alternate", Data = "alternate" },
                    new CheckData { Name = "White Logo", Data = "white_logo" },
                    new CheckData { Name = "Material", Data = "material" },
                    new CheckData { Name = "Blurred", Data = "blurred" },
                    new CheckData { Name = "No Logo", Data = "no_logo" }
                },
                CheckTypes = new List<CheckData>
                {
                    new CheckData { Name = "Static", Data = "static" },
                    new CheckData { Name = "Animated", Data = "animated" }
                },
                CheckTags = CreateDefaultTags()
            };
        }

        /// <summary>
        /// Default filters for SteamGridDB icons API (scalar dimensions, official/custom styles).
        /// </summary>
        private static SteamGridFilters CreateDefaultIconsFilters()
        {
            return new SteamGridFilters
            {
                CheckDimensions = new List<CheckData>
                {
                    new CheckData { Name = "32 px", Data = "32" },
                    new CheckData { Name = "64 px", Data = "64" },
                    new CheckData { Name = "128 px", Data = "128" },
                    new CheckData { Name = "256 px", Data = "256" },
                    new CheckData { Name = "512 px", Data = "512" },
                    new CheckData { Name = "1024 px", Data = "1024" }
                },
                CheckStyles = new List<CheckData>
                {
                    new CheckData { Name = "Official", Data = "official" },
                    new CheckData { Name = "Custom", Data = "custom" }
                },
                CheckTypes = new List<CheckData>
                {
                    new CheckData { Name = "Static", Data = "static" },
                    new CheckData { Name = "Animated", Data = "animated" }
                },
                CheckTags = CreateDefaultTags(),
                CheckMimes = CreateDefaultIconMimes()
            };
        }

        private static List<CheckData> CreateDefaultIconMimes()
        {
            return new List<CheckData>
            {
                new CheckData { Name = "PNG", Data = "image/png" },
                new CheckData { Name = "ICO", Data = "image/vnd.microsoft.icon" }
            };
        }

        private static List<CheckData> CreateDefaultTags()
        {
            return new List<CheckData>
            {
                new CheckData { Name = "Humor", Data = "Humor" },
                new CheckData { Name = "Adult Content", Data = "Adult Content", IsChecked = false },
                new CheckData { Name = "Epilepsy", Data = "Epilepsy" },
                new CheckData { Name = "Untagged", Data = "Untagged" }
            };
        }

        private static List<CheckData> DedupeByData(List<CheckData> items)
        {
            if (items == null || items.Count == 0)
            {
                return new List<CheckData>();
            }

            HashSet<string> seen = new HashSet<string>();
            List<CheckData> result = new List<CheckData>();
            foreach (CheckData item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.Data) || !seen.Add(item.Data))
                {
                    continue;
                }

                result.Add(item);
            }

            return result;
        }

        private static string GetDimensionSummary(CheckData item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(item.Data))
            {
                return item.Data;
            }

            string[] parts = item.Name?.Split('-');
            if (parts != null && parts.Length >= 3)
            {
                return parts[2].Trim();
            }

            return item.Name ?? string.Empty;
        }
    }
}
