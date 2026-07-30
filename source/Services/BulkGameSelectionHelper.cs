using BackgroundChanger.Models;
using CommonPluginsShared;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Resolves the Playnite game list for bulk download from persisted/dialog filter options
    /// (logic formerly provided by <c>OptionsDownloadData</c>).
    /// </summary>
    public static class BulkGameSelectionHelper
    {
        private const string LogPrefix = "[BulkMediaDownload]";

        /// <summary>
        /// Builds the game list for a bulk run from <paramref name="options"/> and the plugin database.
        /// </summary>
        /// <param name="options">Bulk options including game-source filters.</param>
        /// <param name="pluginDatabase">Plugin database (library filter + old-data lookup).</param>
        /// <returns>Filtered non-hidden games; never null.</returns>
        public static List<Game> ResolveGames(BulkMediaDownloadOptions options, IPluginDatabase pluginDatabase)
        {
            if (options == null)
            {
                return new List<Game>();
            }

            options.EnsureInitialized();

            List<Game> games = ResolveGameSource(options.GameSource);
            int months = options.GameFilterMonths;

            switch (options.TimeFilter)
            {
                case BulkGameTimeFilter.RecentlyPlayed:
                    games = games
                        .Where(x => x.LastActivity != null && (DateTime)x.LastActivity >= DateTime.Now.AddMonths(-months))
                        .ToList();
                    break;
                case BulkGameTimeFilter.RecentlyAdded:
                    games = games
                        .Where(x => x.Added != null && (DateTime)x.Added >= DateTime.Now.AddMonths(-months))
                        .ToList();
                    break;
                case BulkGameTimeFilter.OldData:
                    if (pluginDatabase != null)
                    {
                        HashSet<Guid> oldDataIds = new HashSet<Guid>(
                            pluginDatabase.GetGamesOldData(months).Select(x => x.Id));
                        games = games.Where(x => oldDataIds.Contains(x.Id)).ToList();
                    }

                    break;
            }

            switch (options.InstallFilter)
            {
                case BulkGameInstallFilter.Installed:
                    games = games.Where(x => x.IsInstalled).ToList();
                    break;
                case BulkGameInstallFilter.NotInstalled:
                    games = games.Where(x => !x.IsInstalled).ToList();
                    break;
                case BulkGameInstallFilter.Favorite:
                    games = games.Where(x => x.Favorite).ToList();
                    break;
            }

            if (pluginDatabase?.FilterSettings != null)
            {
                int beforeLibraryFilter = games.Count;
                games = PlayniteTools.FilterLibraryGames(games, pluginDatabase.FilterSettings).ToList();
                Common.LogDebug(true, string.Format(
                    "{0} LibraryFilter: {1} -> {2} (IncludeEmulatedGames={3}, SourceFilter={4})",
                    LogPrefix,
                    beforeLibraryFilter,
                    games.Count,
                    pluginDatabase.FilterSettings.IncludeEmulatedGames,
                    PlayniteTools.FormatSourceFilterForLog(pluginDatabase.FilterSettings)));
            }

            if (options.OnlyGamesWithoutPluginData && pluginDatabase != null)
            {
                BackgroundChangerDatabase database = pluginDatabase as BackgroundChangerDatabase;
                if (database != null)
                {
                    games = games
                        .Where(game =>
                        {
                            GameBackgroundImages data = database.Get(game.Id, true);
                            return data == null || !data.HasData;
                        })
                        .ToList();
                }
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} Game selection Source={1} Install={2} Time={3} Months={4} OnlyGamesWithoutData={5} Games={6}",
                    LogPrefix,
                    options.GameSource,
                    options.InstallFilter,
                    options.TimeFilter,
                    months,
                    options.OnlyGamesWithoutPluginData,
                    games.Count));

            return games;
        }

        private static List<Game> ResolveGameSource(BulkGameSource source)
        {
            switch (source)
            {
                case BulkGameSource.Filtered:
                    return API.Instance.MainView.FilteredGames?.ToList()
                        ?? new List<Game>();
                case BulkGameSource.Selected:
                    return API.Instance.MainView.SelectedGames?.ToList()
                        ?? new List<Game>();
                case BulkGameSource.All:
                default:
                    return API.Instance.Database.Games.Where(x => x.Hidden == false).ToList();
            }
        }
    }
}
