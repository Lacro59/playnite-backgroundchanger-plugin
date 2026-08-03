using BackgroundChanger.Models;
using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;
using System;
using System.Linq;
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
        private static readonly ILogger Logger = LogManager.GetLogger();
        private static readonly Guid PluginId = Guid.Parse("3afdd02b-db6c-4b60-8faa-2971d6dfad2a");
        private const string LogPrefix = "[SteamGridDbApi]";
        private const string NotificationIdMissingApiKey = "BackgroundChanger-steamgriddb-missing-apikey";
        private const string NotificationIdUnauthorized = "BackgroundChanger-steamgriddb-unauthorized";
        private const string NotificationIdForbidden = "BackgroundChanger-steamgriddb-forbidden";
        private const string NotificationIdRateLimit = "BackgroundChanger-steamgriddb-ratelimit";
        private const string NotificationIdParseError = "BackgroundChanger-steamgriddb-parse-error";
        private const string NotificationIdGenericError = "BackgroundChanger-steamgriddb-generic-error";

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
        private static string UrlGameBySteam => UrlBase + "/api/v2/games/steam/{0}";

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamGridDbApi"/> class using the API key from plugin settings.
        /// </summary>
        public SteamGridDbApi()
        {
            ApiKey = PluginDatabase.PluginSettings.SteamGridDbApiKey;
        }

        /// <summary>
        /// Resolves a SteamGridDB game by Steam AppId (<c>/games/steam/{appId}</c>).
        /// </summary>
        /// <param name="steamAppId">Steam application id.</param>
        /// <returns>Game payload when found; otherwise <c>null</c>.</returns>
        public SteamGridDbGame GetGameBySteamAppId(uint steamAppId)
        {
            if (steamAppId == 0 || !EnsureApiKeyConfigured("GetGameBySteamAppId"))
            {
                return null;
            }

            string url = string.Format(UrlGameBySteam, steamAppId);
            string context = string.Format(
                "operation=GetGameBySteamAppId endpoint=games/steam/{0}",
                steamAppId);

            try
            {
                string response = DownloadApiResponse(url, context);
                if (!Serialization.TryFromJson(response, out SteamGridDbGameResponse resultData, out Exception parseEx))
                {
                    Logger.Warn(string.Format(
                        "{0} GetGameBySteamAppId JSON parse failed {1} responseLength={2}",
                        LogPrefix,
                        context,
                        response?.Length ?? 0));
                    if (parseEx != null)
                    {
                        Common.LogError(parseEx, false, LogPrefix + " GetGameBySteamAppId parse error", false, PluginDatabase.PluginName);
                    }

                    return null;
                }

                if (resultData == null || !resultData.Success || resultData.Data == null || resultData.Data.Id <= 0)
                {
                    Common.LogDebug(true, string.Format("{0} GetGameBySteamAppId no game steamAppId={1}", LogPrefix, steamAppId));
                    return null;
                }

                Common.LogDebug(true,
                    string.Format(
                        "{0} GetGameBySteamAppId success steamAppId={1} sgdbId={2} name='{3}'",
                        LogPrefix,
                        steamAppId,
                        resultData.Data.Id,
                        resultData.Data.Name));
                return resultData.Data;
            }
            catch (Exception ex)
            {
                LogApiFailure(ex, url, context);
            }

            return null;
        }

        /// <summary>
        /// Searches for a game on SteamGridDB using the given name.
        /// </summary>
        /// <param name="name">The game name to search for.</param>
        /// <returns>A <see cref="SteamGridDbSearchResultData"/> object containing the search results, or null if an error occurs.</returns>
        public SteamGridDbSearchResultData SearchGame(string name)
        {
            if (!EnsureApiKeyConfigured("SearchGame"))
            {
                return null;
            }

            string url = string.Format(UrlSearch, WebUtility.UrlEncode(name));
            string context = string.Format(
                "operation=SearchGame endpoint=search/autocomplete term='{0}'",
                name ?? string.Empty);

            try
            {
                string response = DownloadApiResponse(url, context);
                if (!Serialization.TryFromJson(response, out SteamGridDbSearchResultData resultData, out Exception parseEx))
                {
                    Logger.Warn(string.Format(
                        "{0} SearchGame JSON parse failed {1} responseLength={2}",
                        LogPrefix,
                        context,
                        response?.Length ?? 0));
                    if (parseEx != null)
                    {
                        Common.LogError(parseEx, false, LogPrefix + " SearchGame parse error", false, PluginDatabase.PluginName);
                    }

                    NotifyUser(
                        NotificationIdParseError,
                        "LOCBcSteamGridDbParseErrorNotification",
                        NotificationType.Error,
                        openSettingsOnActivate: true);
                    return null;
                }

                int resultCount = resultData?.Data?.Count ?? 0;
                Common.LogDebug(true,
                    string.Format("{0} SearchGame success term='{1}' results={2}", LogPrefix, name, resultCount));
                return resultData;
            }
            catch (Exception ex)
            {
                LogApiFailure(ex, url, context);
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
            return BuildSearchUrl(gameId, assetType, activeFilters, page, limit: null);
        }

        /// <summary>
        /// Builds the full search URL for a game asset list, optionally including API <c>limit</c>.
        /// </summary>
        /// <param name="gameId">SteamGridDB game ID.</param>
        /// <param name="assetType">Grid, hero, or icon asset type.</param>
        /// <param name="activeFilters">Checked filter values to serialize as query parameters.</param>
        /// <param name="page">Pagination index.</param>
        /// <param name="limit">Optional page size (1–50).</param>
        public static string BuildSearchUrl(
            int gameId,
            SteamGridDbType assetType,
            SteamGridFilters activeFilters,
            int page,
            int? limit)
        {
            string resource = SteamGridFilterHelper.ResolveApiResource(assetType);
            string query = SteamGridFilterHelper.BuildSearchQueryString(assetType, activeFilters, page, limit);
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
            return SearchElement(id, steamGridDbType, activeFilters, page, limit: null);
        }

        /// <summary>
        /// Searches for images for the specified game ID using active filter selections and an optional page <c>limit</c>.
        /// </summary>
        /// <param name="id">The SteamGridDB game ID.</param>
        /// <param name="steamGridDbType">The type of image to search (grid, hero, or icon).</param>
        /// <param name="activeFilters">Active filter lists from the UI or persisted settings.</param>
        /// <param name="page">The page number for paginated results.</param>
        /// <param name="limit">Optional API page size (1–50).</param>
        /// <returns>A <see cref="SteamGridDbResultData"/> object containing the results, or null if an error occurs.</returns>
        public SteamGridDbResultData SearchElement(
            int id,
            SteamGridDbType steamGridDbType,
            SteamGridFilters activeFilters,
            int page,
            int? limit)
        {
            if (!EnsureApiKeyConfigured("SearchElement"))
            {
                return null;
            }

            string resource = SteamGridFilterHelper.ResolveApiResource(steamGridDbType);
            string url = BuildSearchUrl(id, steamGridDbType, activeFilters, page, limit);
            string context = string.Format(
                "operation=SearchElement endpoint={0}/game/{1} assetType={2} page={3} limit={4}",
                resource,
                id,
                steamGridDbType,
                page,
                limit.HasValue ? limit.Value.ToString() : "default");

            try
            {
                string response = DownloadApiResponse(url, context);
                if (!Serialization.TryFromJson(response, out SteamGridDbResultData resultData, out Exception parseEx))
                {
                    Logger.Warn(string.Format(
                        "{0} SearchElement JSON parse failed {1} responseLength={2}",
                        LogPrefix,
                        context,
                        response?.Length ?? 0));
                    if (parseEx != null)
                    {
                        Common.LogError(parseEx, false, LogPrefix + " SearchElement parse error", false, PluginDatabase.PluginName);
                    }

                    NotifyUser(
                        NotificationIdParseError,
                        "LOCBcSteamGridDbParseErrorNotification",
                        NotificationType.Error,
                        openSettingsOnActivate: true);
                    return null;
                }

                int resultCount = resultData?.Data?.Count ?? 0;
                Common.LogDebug(true,
                    string.Format("{0} SearchElement success {1} results={2}", LogPrefix, context, resultCount));
                return resultData;
            }
            catch (Exception ex)
            {
                LogApiFailure(ex, url, context);
            }

            return null;
        }

        private bool EnsureApiKeyConfigured(string operation)
        {
            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                Common.LogDebug(
                    true,
                    string.Format("{0} {1} apiKeyLength={2}", LogPrefix, operation, ApiKey.Length));
                return true;
            }

            Logger.Warn(string.Format(
                "{0} {1} skipped: SteamGridDB API key is not configured (plugin settings).",
                LogPrefix,
                operation));
            NotifyUser(
                NotificationIdMissingApiKey,
                "LOCBcSteamGridDbApiKeyMissingNotification",
                NotificationType.Error,
                openSettingsOnActivate: true);
            return false;
        }

        private string DownloadApiResponse(string url, string context)
        {
            Common.LogDebug(true, string.Format("{0} request {1} url={2}", LogPrefix, context, url));
            return Web.DownloadStringData(url, ApiKey).GetAwaiter().GetResult();
        }

        private void LogApiFailure(Exception ex, string url, string context)
        {
            string statusCode = TryGetHttpStatusCode(ex);
            if (statusCode == "401")
            {
                Logger.Warn(string.Format(
                    "{0} HTTP 401 Unauthorized on {1}. Verify the SteamGridDB API key in plugin settings (missing, invalid, expired, or revoked). url={2}",
                    LogPrefix,
                    context,
                    url));
                Common.LogError(ex, true, LogPrefix + " HTTP 401", false, PluginDatabase.PluginName);
                NotifyUser(
                    NotificationIdUnauthorized,
                    "LOCBcSteamGridDbUnauthorizedNotification",
                    NotificationType.Error,
                    openSettingsOnActivate: true);
                return;
            }

            if (statusCode == "403")
            {
                Logger.Warn(string.Format(
                    "{0} HTTP 403 Forbidden on {1}. The API key may lack permissions for this endpoint. url={2}",
                    LogPrefix,
                    context,
                    url));
                Common.LogError(ex, true, LogPrefix + " HTTP 403", false, PluginDatabase.PluginName);
                NotifyUser(
                    NotificationIdForbidden,
                    "LOCBcSteamGridDbForbiddenNotification",
                    NotificationType.Error,
                    openSettingsOnActivate: true);
                return;
            }

            if (statusCode == "429")
            {
                Logger.Warn(string.Format(
                    "{0} HTTP 429 Too Many Requests on {1}. SteamGridDB rate limit reached; retry later. url={2}",
                    LogPrefix,
                    context,
                    url));
                Common.LogError(ex, true, LogPrefix + " HTTP 429", false, PluginDatabase.PluginName);
                NotifyUser(
                    NotificationIdRateLimit,
                    "LOCBcSteamGridDbRateLimitNotification",
                    NotificationType.Info,
                    openSettingsOnActivate: false);
                return;
            }

            if (statusCode == "404")
            {
                Logger.Warn(string.Format(
                    "{0} HTTP 404 Not Found on {1}. url={2}",
                    LogPrefix,
                    context,
                    url));
                Common.LogError(ex, true, LogPrefix + " HTTP 404", false, PluginDatabase.PluginName);
                return;
            }

            string statusLabel = string.IsNullOrEmpty(statusCode) ? "unknown" : "HTTP " + statusCode;
            Common.LogError(
                ex,
                false,
                string.Format("{0} request failed status={1} {2} url={3}", LogPrefix, statusLabel, context, url),
                false,
                PluginDatabase.PluginName);
            NotifyUser(
                NotificationIdGenericError,
                "LOCBcSteamGridDbGenericErrorNotification",
                NotificationType.Error,
                openSettingsOnActivate: false);
        }

        private void NotifyUser(
            string notificationId,
            string resourceKey,
            NotificationType notificationType,
            bool openSettingsOnActivate)
        {
            string pluginName = PluginDatabase.PluginName;
            string message = ResourceProvider.GetString(resourceKey);
            string notificationText = pluginName + Environment.NewLine + message;

            if (openSettingsOnActivate)
            {
                API.Instance.Notifications.Add(new NotificationMessage(
                    notificationId,
                    notificationText,
                    notificationType,
                    OpenPluginSettings));
                return;
            }

            API.Instance.Notifications.Add(new NotificationMessage(
                notificationId,
                notificationText,
                notificationType));
        }

        private static void OpenPluginSettings()
        {
            Plugin plugin = API.Instance.Addons.Plugins.FirstOrDefault(x => x.Id == PluginId);
            if (plugin != null)
            {
                _ = plugin.OpenSettingsView();
            }
        }

        private static string TryGetHttpStatusCode(Exception ex)
        {
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                string message = current.Message ?? string.Empty;
                if (message.IndexOf("(401)", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("401 (Unauthorized)", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "401";
                }

                if (message.IndexOf("(403)", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("403 (Forbidden)", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "403";
                }

                if (message.IndexOf("(404)", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("404 (Not Found)", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "404";
                }

                if (message.IndexOf("(429)", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("429 (Too Many Requests)", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "429";
                }
            }

            return null;
        }
    }
}
