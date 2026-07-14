using BackgroundChanger.Models;
using System.Collections.Generic;
using System.Linq;
using SteamGridFilters = BackgroundChanger.SteamGridFilters;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Builds default SteamGridDB filter lists and merges persisted selections without duplicates.
    /// </summary>
    public static class SteamGridFilterHelper
    {
        /// <summary>
        /// Returns the canonical default filter lists for the given SteamGridDB asset type.
        /// </summary>
        /// <param name="steamGridDbType">Heroes (backgrounds) or grids (covers).</param>
        public static SteamGridFilters CreateDefaultFilters(SteamGridDbType steamGridDbType)
        {
            if (steamGridDbType == SteamGridDbType.heroes)
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
                    CheckTags = new List<CheckData>
                    {
                        new CheckData { Name = "Humor", Data = "Humor" },
                        new CheckData { Name = "Adult Content", Data = "Adult Content", IsChecked = false },
                        new CheckData { Name = "Epilepsy", Data = "Epilepsy" },
                        new CheckData { Name = "Untagged", Data = "Untagged" }
                    }
                };
            }

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
                CheckTags = new List<CheckData>
                {
                    new CheckData { Name = "Humor", Data = "Humor" },
                    new CheckData { Name = "Adult Content", Data = "Adult Content", IsChecked = false },
                    new CheckData { Name = "Epilepsy", Data = "Epilepsy" },
                    new CheckData { Name = "Untagged", Data = "Untagged" }
                }
            };
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
                SortByDateAsc = saved?.SortByDateAsc ?? defaults.SortByDateAsc
            };
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
