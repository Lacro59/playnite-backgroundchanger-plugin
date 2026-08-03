using BackgroundChanger.Models;
using CommonPlayniteShared.Common.Web;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Images;
using CommonPluginsStores.Steam;
using FuzzySharp;
using Playnite.SDK;
using Playnite.SDK.Models;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using static CommonPluginsShared.PlayniteTools;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Resolves Steam AppIds, builds official CDN media candidate URLs, and probes availability.
    /// </summary>
    public class SteamOfficialMediaService
    {
        private const string LogPrefix = "[SteamOfficialMediaService]";

        /// <summary>
        /// Historical CDN host used by Universal Steam Metadata / Steam Library.
        /// </summary>
        private const string CdnHost = "https://steamcdn-a.akamaihd.net";

        private readonly SteamApi _steamApi;

        /// <summary>
        /// Initializes a new instance using the plugin database name for Steam store paths.
        /// </summary>
        public SteamOfficialMediaService()
            : this(new SteamApi(BackgroundChanger.PluginDatabase.PluginName, ExternalPlugin.None))
        {
        }

        /// <summary>
        /// Initializes a new instance with an injected <see cref="SteamApi"/> (tests / shared lifetime).
        /// </summary>
        /// <param name="steamApi">Steam store API used for AppId resolution.</param>
        public SteamOfficialMediaService(SteamApi steamApi)
        {
            _steamApi = steamApi ?? throw new ArgumentNullException(nameof(steamApi));
        }

        /// <summary>
        /// Resolves the Steam AppId for <paramref name="game"/> (native library, links, store search).
        /// Prefer <see cref="ResolveAppIdViaStoreSearch"/> for bulk flows: this method may load the full Steam apps list
        /// (auth / cache notifications) when falling through <see cref="SteamApi.ResolveAppId"/>.
        /// </summary>
        /// <param name="game">Playnite game.</param>
        /// <returns>Steam AppId, or <c>0</c> when not found.</returns>
        public uint ResolveAppId(Game game)
        {
            if (game == null)
            {
                return 0;
            }

            try
            {
                return _steamApi.ResolveAppId(game);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, BackgroundChanger.PluginDatabase.PluginName);
                return 0;
            }
        }

        /// <summary>
        /// Resolves a Steam AppId without loading the Steam apps catalogue (avoids WebToken 403 / old-data notifications).
        /// Order: native Steam library id → Steam link → store search (<see cref="GetSearchGame"/>) with FuzzySharp.
        /// </summary>
        /// <param name="game">Playnite game.</param>
        /// <param name="fuzzyThreshold">Minimum <see cref="Fuzz.Ratio"/> (0–100) to accept a search hit.</param>
        /// <returns>Steam AppId, or <c>0</c> when not found / below threshold.</returns>
        public uint ResolveAppIdViaStoreSearch(Game game, int fuzzyThreshold = 90)
        {
            if (game == null)
            {
                return 0;
            }

            if (fuzzyThreshold < 0)
            {
                fuzzyThreshold = 0;
            }
            else if (fuzzyThreshold > 100)
            {
                fuzzyThreshold = 100;
            }

            try
            {
                if (game.PluginId == GetPluginId(ExternalPlugin.SteamLibrary)
                    && uint.TryParse(game.GameId, out uint nativeAppId)
                    && nativeAppId > 0)
                {
                    Common.LogDebug(true,
                        string.Format("{0} AppId via native Steam library gameId={1}", LogPrefix, nativeAppId));
                    return nativeAppId;
                }

                uint fromLink = TryGetAppIdFromSteamLink(game);
                if (fromLink > 0)
                {
                    Common.LogDebug(true,
                        string.Format("{0} AppId via Steam link appId={1} game='{2}'", LogPrefix, fromLink, game.Name));
                    return fromLink;
                }

                if (game.Name.IsNullOrWhiteSpace())
                {
                    return 0;
                }

                IReadOnlyList<GenericItemOption> search = SearchGames(game.Name);
                if (search == null || search.Count == 0)
                {
                    Common.LogDebug(true,
                        string.Format("{0} Store search empty game='{1}'", LogPrefix, game.Name));
                    return 0;
                }

                string gameName = game.Name.ToLowerInvariant();
                GenericItemOption best = null;
                int bestScore = -1;
                foreach (GenericItemOption option in search)
                {
                    if (option == null || option.Name.IsNullOrEmpty())
                    {
                        continue;
                    }

                    int score = Fuzz.Ratio(gameName, option.Name.ToLowerInvariant());
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = option;
                    }
                }

                if (best == null || bestScore < fuzzyThreshold)
                {
                    Common.LogDebug(true,
                        string.Format(
                            "{0} Store search below threshold game='{1}' best='{2}' score={3} threshold={4}",
                            LogPrefix,
                            game.Name,
                            best?.Name,
                            bestScore,
                            fuzzyThreshold));
                    return 0;
                }

                uint appId = ParseAppIdFromSearchOption(best);
                Common.LogDebug(true,
                    string.Format(
                        "{0} AppId via store search game='{1}' match='{2}' score={3} appId={4}",
                        LogPrefix,
                        game.Name,
                        best.Name,
                        bestScore,
                        appId));
                return appId;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, BackgroundChanger.PluginDatabase.PluginName);
                return 0;
            }
        }

        /// <summary>
        /// Reads a Steam AppId from a game link named "Steam" (<c>/app/{id}</c>), same rules as <c>SteamApi.GetAppIdFromLinks</c>.
        /// </summary>
        /// <param name="game">Playnite game.</param>
        /// <returns>AppId, or <c>0</c> when absent.</returns>
        private static uint TryGetAppIdFromSteamLink(Game game)
        {
            Link steamLink = game.Links?.FirstOrDefault(link =>
                link != null
                && !link.Name.IsNullOrEmpty()
                && link.Name.Equals("steam", StringComparison.OrdinalIgnoreCase));

            if (steamLink == null || steamLink.Url.IsNullOrEmpty())
            {
                return 0;
            }

            string[] linkSplit = steamLink.Url.Split(new[] { "/app/" }, StringSplitOptions.None);
            string steamIdString = linkSplit.Length > 1
                ? linkSplit[1].Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                : null;

            if (steamIdString.IsNullOrEmpty())
            {
                return 0;
            }

            return uint.TryParse(steamIdString, out uint steamId) ? steamId : 0;
        }

        /// <summary>
        /// Searches Steam store games by name (store search HTTP API).
        /// </summary>
        /// <param name="searchTerm">User search text.</param>
        /// <returns>Matching games; empty when the request fails.</returns>
        public IReadOnlyList<GenericItemOption> SearchGames(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<GenericItemOption>();
            }

            try
            {
                return _steamApi.GetSearchGame(searchTerm, noDlcs: true) ?? new List<GenericItemOption>();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, BackgroundChanger.PluginDatabase.PluginName);
                return new List<GenericItemOption>();
            }
        }

        /// <summary>
        /// Parses the Steam AppId from a <see cref="GenericItemOption"/> returned by <see cref="SearchGames"/>.
        /// </summary>
        /// <param name="option">Search result item.</param>
        /// <returns>AppId, or <c>0</c> when parsing fails.</returns>
        public static uint ParseAppIdFromSearchOption(GenericItemOption option)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.Description))
            {
                return 0;
            }

            string idPart = option.Description.Split(new[] { " - " }, StringSplitOptions.None)[0].Trim();
            return uint.TryParse(idPart, out uint appId) ? appId : 0;
        }

        /// <summary>
        /// Builds the ordered CDN candidate list for Cover or Background (no existence probe).
        /// Prefer <see cref="GetAvailableCdnCandidates(uint, BackgroundChangerDatabase.PluginMediaKind)"/> when callers need live URLs.
        /// Icon candidates require product info and are not returned here.
        /// </summary>
        /// <param name="appId">Steam AppId.</param>
        /// <param name="mediaKind">Target plugin media kind.</param>
        /// <returns>Candidates in display preference order; empty for Icon or invalid AppId.</returns>
        public IReadOnlyList<SteamOfficialMediaCandidate> GetCdnCandidates(
            uint appId,
            BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            List<SteamOfficialMediaCandidate> candidates = new List<SteamOfficialMediaCandidate>();
            if (appId == 0)
            {
                return candidates;
            }

            switch (mediaKind)
            {
                case BackgroundChangerDatabase.PluginMediaKind.Background:
                    AddCandidate(
                        candidates,
                        appId,
                        mediaKind,
                        SteamOfficialAssetKind.LibraryHero,
                        "Library Hero",
                        string.Format("{0}/steam/apps/{1}/library_hero.jpg", CdnHost, appId));
                    AddCandidate(
                        candidates,
                        appId,
                        mediaKind,
                        SteamOfficialAssetKind.LibraryHero2x,
                        "Library Hero 2x",
                        string.Format("{0}/steam/apps/{1}/library_hero_2x.jpg", CdnHost, appId));
                    AddCandidate(
                        candidates,
                        appId,
                        mediaKind,
                        SteamOfficialAssetKind.PageBgGeneratedV6b,
                        "Store Page Background",
                        string.Format("{0}/steam/apps/{1}/page_bg_generated_v6b.jpg", CdnHost, appId));
                    AddCandidate(
                        candidates,
                        appId,
                        mediaKind,
                        SteamOfficialAssetKind.PageBg,
                        "Store Page Background (legacy)",
                        string.Format("{0}/steam/apps/{1}/page.bg.jpg", CdnHost, appId));
                    AddCandidate(
                        candidates,
                        appId,
                        mediaKind,
                        SteamOfficialAssetKind.PageBgGenerated,
                        "Store Page Background (generated)",
                        string.Format("{0}/steam/apps/{1}/page_bg_generated.jpg", CdnHost, appId));
                    break;

                case BackgroundChangerDatabase.PluginMediaKind.Cover:
                    AddCandidate(
                        candidates,
                        appId,
                        mediaKind,
                        SteamOfficialAssetKind.Library600x9002x,
                        "Library Cover 2x",
                        string.Format("{0}/steam/apps/{1}/library_600x900_2x.jpg", CdnHost, appId));
                    AddCandidate(
                        candidates,
                        appId,
                        mediaKind,
                        SteamOfficialAssetKind.Library600x900,
                        "Library Cover",
                        string.Format("{0}/steam/apps/{1}/library_600x900.jpg", CdnHost, appId));
                    AddCandidate(
                        candidates,
                        appId,
                        mediaKind,
                        SteamOfficialAssetKind.Header,
                        "Store Header",
                        string.Format("{0}/steam/apps/{1}/header.jpg", CdnHost, appId));
                    break;

                case BackgroundChangerDatabase.PluginMediaKind.Icon:
                    Common.LogDebug(true, LogPrefix + " CDN catalog has no Icon candidates; use product-info resolution.");
                    break;

                default:
                    break;
            }

            return candidates;
        }

        /// <summary>
        /// Returns CDN candidates that respond with HTTP 200 to a HEAD request.
        /// For Cover, omits <see cref="SteamOfficialAssetKind.Library600x900"/> when the 2× portrait is available.
        /// </summary>
        /// <param name="appId">Steam AppId.</param>
        /// <param name="mediaKind">Target plugin media kind (Cover or Background).</param>
        /// <returns>Available candidates in preference order; empty when none exist or kind is Icon.</returns>
        public IReadOnlyList<SteamOfficialMediaCandidate> GetAvailableCdnCandidates(
            uint appId,
            BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            List<SteamOfficialMediaCandidate> available = new List<SteamOfficialMediaCandidate>();
            IReadOnlyList<SteamOfficialMediaCandidate> catalog = GetCdnCandidates(appId, mediaKind);
            if (catalog.Count == 0)
            {
                return available;
            }

            bool hasLibraryCover2x = false;
            foreach (SteamOfficialMediaCandidate candidate in catalog)
            {
                if (!TryProbeAsset(candidate.Url, out int width, out int height, out string mime))
                {
                    Common.LogDebug(true,
                        string.Format(
                            "{0} Probe miss appId={1} kind={2} url={3}",
                            LogPrefix,
                            appId,
                            candidate.AssetKind,
                            candidate.Url));
                    continue;
                }

                candidate.Width = width;
                candidate.Height = height;
                candidate.Mime = mime;

                if (candidate.AssetKind == SteamOfficialAssetKind.Library600x9002x)
                {
                    hasLibraryCover2x = true;
                }

                if (candidate.AssetKind == SteamOfficialAssetKind.Library600x900 && hasLibraryCover2x)
                {
                    Common.LogDebug(true,
                        string.Format(
                            "{0} Skip 1x cover; 2x available appId={1}",
                            LogPrefix,
                            appId));
                    continue;
                }

                available.Add(candidate);
                Common.LogDebug(true,
                    string.Format(
                        "{0} Probe hit appId={1} kind={2} media={3} size={4}x{5} mime={6} url={7}",
                        LogPrefix,
                        appId,
                        candidate.AssetKind,
                        candidate.MediaKind,
                        width,
                        height,
                        mime ?? "(none)",
                        candidate.Url));
            }

            return available;
        }

        /// <summary>
        /// Resolves the game AppId then returns available CDN candidates for Cover or Background.
        /// </summary>
        /// <param name="game">Playnite game.</param>
        /// <param name="mediaKind">Target plugin media kind.</param>
        /// <returns>Available candidates; empty when AppId is missing or no asset responds 200.</returns>
        public IReadOnlyList<SteamOfficialMediaCandidate> GetAvailableCdnCandidates(
            Game game,
            BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            uint appId = ResolveAppId(game);
            if (appId == 0)
            {
                Common.LogDebug(true, LogPrefix + " No AppId; CDN probe skipped.");
                return new List<SteamOfficialMediaCandidate>();
            }

            return GetAvailableCdnCandidates(appId, mediaKind);
        }

        /// <summary>
        /// Resolves an Icon candidate via SteamKit anonymous PICS (Universal Steam Metadata pattern).
        /// </summary>
        /// <param name="appId">Steam AppId.</param>
        /// <returns>Icon candidate when product info exposes a hash; otherwise <c>null</c>.</returns>
        public SteamOfficialMediaCandidate GetIconCandidate(uint appId)
        {
            if (appId == 0)
            {
                return null;
            }

            KeyValue productInfo = new SteamAnonymousProductInfoClient().GetProductInfo(appId);
            if (productInfo == null)
            {
                Common.LogDebug(true, LogPrefix + " No product info for Icon appId=" + appId);
                return null;
            }

            string clientIcon = GetKeyValueString(productInfo, "common", "clienticon");
            if (!string.IsNullOrWhiteSpace(clientIcon))
            {
                string url = BuildCommunityIconUrl(appId, clientIcon, true);
                if (!string.IsNullOrEmpty(url))
                {
                    SteamOfficialMediaCandidate candidate = new SteamOfficialMediaCandidate
                    {
                        Id = SteamOfficialAssetKind.ClientIcon.ToString(),
                        DisplayName = "Client Icon",
                        Url = url,
                        MediaKind = BackgroundChangerDatabase.PluginMediaKind.Icon,
                        AssetKind = SteamOfficialAssetKind.ClientIcon
                    };
                    ApplyProbeMetadata(candidate);
                    LogIconCandidateResolved(appId, candidate);
                    return candidate;
                }
            }

            string communityIcon = GetKeyValueString(productInfo, "common", "icon");
            if (!string.IsNullOrWhiteSpace(communityIcon))
            {
                string url = BuildCommunityIconUrl(appId, communityIcon, false);
                if (!string.IsNullOrEmpty(url))
                {
                    SteamOfficialMediaCandidate candidate = new SteamOfficialMediaCandidate
                    {
                        Id = SteamOfficialAssetKind.CommunityIcon.ToString(),
                        DisplayName = "Community Icon",
                        Url = url,
                        MediaKind = BackgroundChangerDatabase.PluginMediaKind.Icon,
                        AssetKind = SteamOfficialAssetKind.CommunityIcon
                    };
                    ApplyProbeMetadata(candidate);
                    LogIconCandidateResolved(appId, candidate);
                    return candidate;
                }
            }

            Common.LogDebug(true, LogPrefix + " No icon hash in product info appId=" + appId);
            return null;
        }

        private static void LogIconCandidateResolved(uint appId, SteamOfficialMediaCandidate candidate)
        {
            Common.LogDebug(true,
                string.Format(
                    "{0} Icon resolved appId={1} kind={2} size={3}x{4} mime={5} url={6}",
                    LogPrefix,
                    appId,
                    candidate.AssetKind,
                    candidate.Width,
                    candidate.Height,
                    candidate.Mime ?? "(none)",
                    candidate.Url));
        }

        /// <summary>
        /// Checks whether <paramref name="url"/> exists via HTTP HEAD (Universal Steam Metadata pattern).
        /// </summary>
        /// <param name="url">Absolute HTTP(S) URL.</param>
        /// <returns><c>true</c> when the response status is 200 OK.</returns>
        public bool UrlExists(string url)
        {
            return TryProbeAsset(url, out _, out _, out _);
        }

        /// <summary>
        /// Probes asset availability and reads dimensions / MIME for display.
        /// </summary>
        /// <param name="url">Absolute HTTP(S) URL.</param>
        /// <param name="width">Image width in pixels when readable.</param>
        /// <param name="height">Image height in pixels when readable.</param>
        /// <param name="mime">MIME subtype (e.g. <c>jpeg</c>).</param>
        /// <returns><c>true</c> when the asset responds with HTTP 200.</returns>
        public bool TryProbeAsset(string url, out int width, out int height, out string mime)
        {
            width = 0;
            height = 0;
            mime = null;

            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            try
            {
                HttpStatusCode status = HttpDownloader.GetResponseCode(url, out Dictionary<string, string> headers);
                if (status != HttpStatusCode.OK)
                {
                    return false;
                }

                mime = ResolveMime(headers, url);
                TryReadImageDimensions(url, out width, out height);
                return true;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, BackgroundChanger.PluginDatabase.PluginName);
                return false;
            }
        }

        private void ApplyProbeMetadata(SteamOfficialMediaCandidate candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Url))
            {
                return;
            }

            if (TryProbeAsset(candidate.Url, out int width, out int height, out string mime))
            {
                candidate.Width = width;
                candidate.Height = height;
                candidate.Mime = mime;
            }
        }

        private static void TryReadImageDimensions(string url, out int width, out int height)
        {
            width = 0;
            height = 0;

            try
            {
                byte[] data = HttpDownloader.DownloadData(url);
                using (MemoryStream stream = new MemoryStream(data))
                {
                    ImageProperty properties = ImageTools.GetImageProperty(stream);
                    if (properties != null)
                    {
                        width = properties.Width;
                        height = properties.Height;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, string.Format("{0} Failed to read image dimensions url={1}", LogPrefix, url));
            }
        }

        private static string ResolveMime(Dictionary<string, string> headers, string url)
        {
            if (headers != null && headers.TryGetValue("Content-Type", out string contentType)
                && !string.IsNullOrWhiteSpace(contentType))
            {
                int separatorIndex = contentType.IndexOf(';');
                if (separatorIndex >= 0)
                {
                    contentType = contentType.Substring(0, separatorIndex);
                }

                contentType = contentType.Trim();
                if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return contentType.Substring("image/".Length);
                }

                return contentType;
            }

            return ResolveMimeFromUrl(url);
        }

        private static string ResolveMimeFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            string extension = Path.GetExtension(url);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            extension = extension.TrimStart('.').ToLowerInvariant();
            if (extension == "jpg")
            {
                return "jpeg";
            }

            return extension;
        }

        /// <summary>
        /// Builds a community CDN icon URL from a product-info hash (Universal Steam Metadata pattern).
        /// </summary>
        /// <param name="appId">Steam AppId.</param>
        /// <param name="hash">Icon hash from <c>clienticon</c> or <c>icon</c>.</param>
        /// <param name="useIcoExtension"><c>true</c> for <c>.ico</c> (<c>clienticon</c>); otherwise <c>.jpg</c>.</param>
        /// <returns>Absolute URL, or <c>null</c> when inputs are invalid.</returns>
        public string BuildCommunityIconUrl(uint appId, string hash, bool useIcoExtension)
        {
            if (appId == 0 || string.IsNullOrWhiteSpace(hash))
            {
                return null;
            }

            string extension = useIcoExtension ? ".ico" : ".jpg";
            return string.Format(
                "{0}/steamcommunity/public/images/apps/{1}/{2}{3}",
                CdnHost,
                appId,
                hash.Trim(),
                extension);
        }

        private static void AddCandidate(
            List<SteamOfficialMediaCandidate> candidates,
            uint appId,
            BackgroundChangerDatabase.PluginMediaKind mediaKind,
            SteamOfficialAssetKind assetKind,
            string displayName,
            string url)
        {
            candidates.Add(new SteamOfficialMediaCandidate
            {
                Id = assetKind.ToString(),
                DisplayName = displayName,
                Url = url,
                MediaKind = mediaKind,
                AssetKind = assetKind
            });

            Common.LogDebug(true,
                string.Format(
                    "{0} Candidate appId={1} kind={2} media={3} url={4}",
                    LogPrefix,
                    appId,
                    assetKind,
                    mediaKind,
                    url));
        }

        private static string GetKeyValueString(KeyValue root, string section, string key)
        {
            if (root == null)
            {
                return null;
            }

            KeyValue sectionNode = root[section];
            if (sectionNode == KeyValue.Invalid)
            {
                return null;
            }

            KeyValue keyNode = sectionNode[key];
            if (keyNode == KeyValue.Invalid)
            {
                return null;
            }

            return keyNode.Value;
        }
    }
}
