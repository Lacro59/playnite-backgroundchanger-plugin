using BackgroundChanger.Models;
using CommonPluginsShared;
using Playnite.SDK.Data;
using System;
using System.Net;
using SteamGridFilters = BackgroundChanger.SteamGridFilters;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Defines the types of images available from SteamGridDB.
    /// </summary>
    public enum SteamGridDbType
    {
        /// <summary>
        /// Grid images (box art and other formats).
        /// </summary>
        grids,

        /// <summary>
        /// Hero images (banner-style backgrounds).
        /// </summary>
        heroes,

        /// <summary>
        /// Game icons (PNG or ICO, scalar dimensions in API queries).
        /// </summary>
        icons
    }

    /// <summary>
    /// Provides methods for querying the SteamGridDB API.
    /// Supports searching for games and retrieving grid, hero, or icon images.
    /// </summary>
    public class SteamGridDbApi
    {
        /// <summary>
        /// Gets the plugin's database instance.
        /// </summary>
        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        /// <summary>
        /// Gets the SteamGridDB API key from plugin settings.
        /// </summary>
        private string ApiKey { get; set; }

        private static string UrlBase => @"https://www.steamgriddb.com";
        private static string UrlSearch => UrlBase + "/api/v2/search/autocomplete/{0}";

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamGridDbApi"/> class using the API key from plugin settings.
        /// </summary>
        public SteamGridDbApi()
        {
            ApiKey = PluginDatabase.PluginSettings.SteamGridDbApiKey;
        }

        /// <summary>
        /// Searches for a game on SteamGridDB using the given name.
        /// </summary>
        /// <param name="name">The game name to search for.</param>
        /// <returns>A <see cref="SteamGridDbSearchResultData"/> object containing the search results, or null if an error occurs.</returns>
        public SteamGridDbSearchResultData SearchGame(string name)
        {
            try
            {
                string response = Web.DownloadStringData(string.Format(UrlSearch, WebUtility.UrlEncode(name)), ApiKey).GetAwaiter().GetResult();
                SteamGridDbSearchResultData resultData = Serialization.FromJson<SteamGridDbSearchResultData>(response);
                return resultData;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return null;
        }

        /// <summary>
        /// Builds the full search URL for a game asset list using active filter selections.
        /// </summary>
        /// <param name="gameId">SteamGridDB game ID.</param>
        /// <param name="assetType">Grid, hero, or icon asset type.</param>
        /// <param name="activeFilters">Checked filter values to serialize as query parameters.</param>
        /// <param name="page">Pagination index.</param>
        public static string BuildSearchUrl(int gameId, SteamGridDbType assetType, SteamGridFilters activeFilters, int page)
        {
            string resource = SteamGridFilterHelper.ResolveApiResource(assetType);
            string query = SteamGridFilterHelper.BuildSearchQueryString(assetType, activeFilters, page);
            return string.Format("{0}/api/v2/{1}/game/{2}?{3}", UrlBase, resource, gameId, query);
        }

        /// <summary>
        /// Searches for images for the specified game ID using active filter selections.
        /// </summary>
        /// <param name="id">The SteamGridDB game ID.</param>
        /// <param name="steamGridDbType">The type of image to search (grid, hero, or icon).</param>
        /// <param name="activeFilters">Active filter lists from the UI or persisted settings.</param>
        /// <param name="page">The page number for paginated results (default is 0).</param>
        /// <returns>A <see cref="SteamGridDbResultData"/> object containing the results, or null if an error occurs.</returns>
        public SteamGridDbResultData SearchElement(int id, SteamGridDbType steamGridDbType, SteamGridFilters activeFilters, int page = 0)
        {
            try
            {
                string url = BuildSearchUrl(id, steamGridDbType, activeFilters, page);
                string response = Web.DownloadStringData(url, ApiKey).GetAwaiter().GetResult();
                _ = Serialization.TryFromJson(response, out SteamGridDbResultData resultData);
                return resultData;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return null;
        }
    }
}
