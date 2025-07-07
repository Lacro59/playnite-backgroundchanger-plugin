using BackgroundChanger.Models;
using CommonPluginsShared;
using Playnite.SDK.Data;
using System;
using System.Net;

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
        heroes
    }

    /// <summary>
    /// Provides methods for querying the SteamGridDB API.
    /// Supports searching for games and retrieving grid or hero images.
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
        private static string UrlSearchGrids => UrlBase + "/api/v2/grids/game/{0}?dimensions=600x900,920x430,460x215,1024x1024,342x482&mimes=image/png,image/webp,image/jpeg&types=static,animated&styles=alternate,blurred,material,white_logo,no_logo&nsfw=any&humor=any&page={1}";
        private static string UrlSearchHeroes => UrlBase + "/api/v2/heroes/game/{0}?dimensions=1920x620,3840x1240,1600x650&mimes=image/png,image/webp,image/jpeg&types=static,animated&styles=alternate,blurred,material&nsfw=any&humor=any&page={1}";
        private static string UrlGame => UrlBase + "/game/{0}";

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamGridDbApi"/> class using the API key from plugin settings.
        /// </summary>
        public SteamGridDbApi()
        {
            ApiKey = PluginDatabase.PluginSettings.Settings.SteamGridDbApiKey;
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
        /// Searches for images (grid or hero) for the specified game ID.
        /// </summary>
        /// <param name="id">The SteamGridDB game ID.</param>
        /// <param name="steamGridDbType">The type of image to search (grid or hero).</param>
        /// <param name="page">The page number for paginated results (default is 0).</param>
        /// <returns>A <see cref="SteamGridDbResultData"/> object containing the results, or null if an error occurs.</returns>
        public SteamGridDbResultData SearchElement(int id, SteamGridDbType steamGridDbType, int page = 0)
        {
            try
            {
                string url = steamGridDbType == SteamGridDbType.grids ? UrlSearchGrids : UrlSearchHeroes;
                string response = Web.DownloadStringData(string.Format(url, id, page), ApiKey).GetAwaiter().GetResult();
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