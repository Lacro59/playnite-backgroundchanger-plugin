using BackgroundChanger.Models;
using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using FuzzySharp;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using SteamGridFilters = BackgroundChanger.SteamGridFilters;

namespace BackgroundChanger.Services
{
    /// <summary>
    /// Bulk-downloads plugin media from Steam CDN and/or SteamGridDB for a list of games.
    /// </summary>
    public class BulkMediaDownloadService
    {
        private const string LogPrefix = "[BulkMediaDownload]";

        /// <summary>
        /// Total download attempts (1 initial + retries) for transient CDN / Cloudflare failures.
        /// </summary>
        private const int DownloadMaxAttempts = 3;

        private static readonly TimeSpan DownloadRetryBaseDelay = TimeSpan.FromSeconds(1);

        private readonly SteamOfficialMediaService _steamMediaService;
        private readonly SteamGridDbApi _steamGridDbApi;
        private readonly MediaImportService _mediaImportService;
        private readonly Random _random = new Random();

        /// <summary>
        /// Initializes a new instance with default Steam / SteamGridDB / import helpers.
        /// </summary>
        public BulkMediaDownloadService()
            : this(new SteamOfficialMediaService(), new SteamGridDbApi(), new MediaImportService())
        {
        }

        /// <summary>
        /// Initializes a new instance with injected dependencies (tests).
        /// </summary>
        /// <param name="steamMediaService">Steam CDN resolver.</param>
        /// <param name="steamGridDbApi">SteamGridDB API client.</param>
        /// <param name="mediaImportService">Local import / conversion helper.</param>
        public BulkMediaDownloadService(
            SteamOfficialMediaService steamMediaService,
            SteamGridDbApi steamGridDbApi,
            MediaImportService mediaImportService)
        {
            _steamMediaService = steamMediaService ?? throw new ArgumentNullException(nameof(steamMediaService));
            _steamGridDbApi = steamGridDbApi ?? throw new ArgumentNullException(nameof(steamGridDbApi));
            _mediaImportService = mediaImportService ?? throw new ArgumentNullException(nameof(mediaImportService));
        }

        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        /// <summary>
        /// Runs the bulk download for the given games and options.
        /// </summary>
        /// <param name="games">Games to process.</param>
        /// <param name="options">Confirmed bulk options.</param>
        /// <param name="cancellationToken">Optional cancellation token (e.g. from GlobalProgress).</param>
        /// <param name="progress">Optional progress reporter (current game index, total, label).</param>
        /// <returns>Aggregate counters for the run.</returns>
        public BulkMediaDownloadResult Run(
            IList<Game> games,
            BulkMediaDownloadOptions options,
            CancellationToken cancellationToken = default(CancellationToken),
            Action<int, int, string> progress = null)
        {
            BulkMediaDownloadResult result = new BulkMediaDownloadResult();
            if (games == null || games.Count == 0 || options == null)
            {
                return result;
            }

            options.EnsureInitialized();
            if (!options.HasAnySource || !options.HasAnyMediaKind)
            {
                Common.LogDebug(false, LogPrefix + " Aborted: no source or media kind enabled.");
                return result;
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} Run games={1} OnlyMissing={2} Steam={3} SGDB={4} BG={5} Cover={6} Icon={7} Qty={8} DownloadAll={9} Pick={10}",
                    LogPrefix,
                    games.Count,
                    options.OnlyMissing,
                    options.EnableSteam,
                    options.EnableSteamGridDb,
                    options.EnableBackground,
                    options.EnableCover,
                    options.EnableIcon,
                    options.Quantity,
                    options.DownloadAll,
                    options.PickMode));

            int total = games.Count;
            for (int index = 0; index < total; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    break;
                }

                Game game = games[index];
                if (game == null)
                {
                    continue;
                }

                progress?.Invoke(index + 1, total, game.Name ?? game.Id.ToString());

                try
                {
                    ProcessGame(game, options, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} Done games={1} imported={2} skipped={3} errors={4} cancelled={5}",
                    LogPrefix,
                    total,
                    result.ImportedCount,
                    result.SkippedCount,
                    result.ErrorCount,
                    result.Cancelled));

            return result;
        }

        private void ProcessGame(
            Game game,
            BulkMediaDownloadOptions options,
            BulkMediaDownloadResult result,
            CancellationToken cancellationToken)
        {
            GameBackgroundImages gameData = PluginDatabase.Get(game);
            if (gameData == null)
            {
                result.SkippedCount++;
                Common.LogDebug(false, string.Format("{0} Skip game (no DB entry) id={1}", LogPrefix, game.Id));
                return;
            }

            LogPluginMediaCounts("BEFORE", game.Name, gameData);

            uint steamAppId = 0;
            if (options.EnableSteam || options.EnableSteamGridDb)
            {
                // Avoid SteamApi.ResolveAppId / SteamApps catalogue (WebToken 403 + old-data notifications in bulk).
                steamAppId = _steamMediaService.ResolveAppIdViaStoreSearch(game, options.FuzzyMatchThreshold);
            }

            int? steamGridGameId = null;
            bool steamGridMatchResolved = false;
            bool steamGridMatchFailed = false;

            foreach (BackgroundChangerDatabase.PluginMediaKind mediaKind in options.GetEnabledMediaKinds())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    LogPluginMediaCounts("AFTER-CANCEL", game.Name, gameData);
                    return;
                }

                int countBeforeKind = CountPluginOwnedMedia(gameData, mediaKind);
                if (options.OnlyMissing && countBeforeKind > 0)
                {
                    result.SkippedCount++;
                    Common.LogDebug(
                        false,
                        string.Format(
                            "{0} Skip kind (bulk OnlyMissing=true) game='{1}' kind={2} count={3}",
                            LogPrefix,
                            game.Name,
                            mediaKind,
                            countBeforeKind));
                    continue;
                }

                int remaining = options.DownloadAll
                    ? int.MaxValue
                    : Math.Max(1, options.Quantity);

                HashSet<string> usedUrls = new HashSet<string>(StringComparer.Ordinal);
                int steamImported = 0;
                int importedAfterSteam = result.ImportedCount;

                if (options.EnableSteam && remaining > 0)
                {
                    steamImported = ImportSteamCandidates(
                        game,
                        gameData,
                        mediaKind,
                        steamAppId,
                        options.PickMode,
                        remaining,
                        usedUrls,
                        result);
                    remaining = options.DownloadAll ? int.MaxValue : remaining - steamImported;
                    importedAfterSteam = result.ImportedCount;
                }

                if (options.EnableSteamGridDb && remaining > 0)
                {
                    if (!steamGridMatchResolved && !steamGridMatchFailed)
                    {
                        steamGridGameId = ResolveSteamGridGameId(game, steamAppId, options, result);
                        steamGridMatchResolved = true;
                        if (!steamGridGameId.HasValue)
                        {
                            steamGridMatchFailed = true;
                        }
                    }

                    if (steamGridGameId.HasValue)
                    {
                        ImportSteamGridCandidates(
                            gameData,
                            mediaKind,
                            steamGridGameId.Value,
                            options,
                            remaining,
                            usedUrls,
                            result,
                            cancellationToken);
                    }
                }

                int countAfterKind = CountPluginOwnedMedia(gameData, mediaKind);
                int sgdbImported = Math.Max(0, result.ImportedCount - importedAfterSteam);
                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} Kind counts game='{1}' kind={2} before={3} after={4} delta={5} steam+={6} sgdb+={7} remainAfterSteam={8}",
                        LogPrefix,
                        game.Name,
                        mediaKind,
                        countBeforeKind,
                        countAfterKind,
                        countAfterKind - countBeforeKind,
                        steamImported,
                        sgdbImported,
                        remaining == int.MaxValue ? "all" : remaining.ToString()));
            }

            LogPluginMediaCounts("AFTER", game.Name, gameData);
        }

        /// <summary>
        /// Logs plugin-owned media counts for Background / Cover / Icon (Playnite mirrors excluded).
        /// </summary>
        /// <param name="phase">Label such as BEFORE / AFTER.</param>
        /// <param name="gameName">Game display name.</param>
        /// <param name="gameData">Plugin DB entry.</param>
        private static void LogPluginMediaCounts(string phase, string gameName, GameBackgroundImages gameData)
        {
            int bg = CountPluginOwnedMedia(gameData, BackgroundChangerDatabase.PluginMediaKind.Background);
            int cover = CountPluginOwnedMedia(gameData, BackgroundChangerDatabase.PluginMediaKind.Cover);
            int icon = CountPluginOwnedMedia(gameData, BackgroundChangerDatabase.PluginMediaKind.Icon);
            Common.LogDebug(
                false,
                string.Format(
                    "{0} Counts {1} game='{2}' bg={3} cover={4} icon={5} total={6}",
                    LogPrefix,
                    phase,
                    gameName,
                    bg,
                    cover,
                    icon,
                    bg + cover + icon));
        }

        private int ImportSteamCandidates(
            Game game,
            GameBackgroundImages gameData,
            BackgroundChangerDatabase.PluginMediaKind mediaKind,
            uint steamAppId,
            BulkMediaPickMode pickMode,
            int remaining,
            HashSet<string> usedUrls,
            BulkMediaDownloadResult result)
        {
            if (steamAppId == 0)
            {
                Common.LogDebug(false, string.Format("{0} Steam skip (no AppId) game='{1}'", LogPrefix, game.Name));
                return 0;
            }

            List<string> urls = new List<string>();
            if (mediaKind == BackgroundChangerDatabase.PluginMediaKind.Icon)
            {
                SteamOfficialMediaCandidate icon = _steamMediaService.GetIconCandidate(steamAppId);
                if (icon != null && !icon.Url.IsNullOrEmpty())
                {
                    urls.Add(icon.Url);
                }
            }
            else
            {
                IReadOnlyList<SteamOfficialMediaCandidate> candidates =
                    _steamMediaService.GetAvailableCdnCandidates(steamAppId, mediaKind);
                foreach (SteamOfficialMediaCandidate candidate in candidates)
                {
                    if (candidate != null && !candidate.Url.IsNullOrEmpty())
                    {
                        urls.Add(candidate.Url);
                    }
                }
            }

            urls = SelectUrls(urls, pickMode, remaining, usedUrls);
            int imported = 0;
            foreach (string url in urls)
            {
                if (TryImportUrl(gameData, mediaKind, url, result))
                {
                    usedUrls.Add(url);
                    imported++;
                }
            }

            return imported;
        }

        private void ImportSteamGridCandidates(
            GameBackgroundImages gameData,
            BackgroundChangerDatabase.PluginMediaKind mediaKind,
            int steamGridGameId,
            BulkMediaDownloadOptions options,
            int remaining,
            HashSet<string> usedUrls,
            BulkMediaDownloadResult result,
            CancellationToken cancellationToken)
        {
            SteamGridDbType assetType = SteamGridFilterHelper.ResolveApiType(mediaKind);
            SteamGridFilters filters = options.GetFilters(mediaKind);
            List<SteamGridDbResult> pool = FetchSteamGridPool(
                steamGridGameId,
                assetType,
                filters,
                options,
                remaining,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                result.Cancelled = true;
                return;
            }

            pool = SteamGridFilterHelper.ApplyClientSideFilters(pool, filters);
            List<string> urls;
            if (options.DownloadAll)
            {
                urls = SelectUrls(
                    pool.Where(x => x != null && !x.Url.IsNullOrEmpty()).Select(x => x.Url).ToList(),
                    options.PickMode,
                    int.MaxValue,
                    usedUrls);
            }
            else
            {
                urls = SelectOrderedSteamGridUrls(pool, options.PickMode, remaining, usedUrls);
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} SGDB pool game='{1}' kind={2} filtered={3} selected={4} pick={5} remain={6}",
                    LogPrefix,
                    gameData.Name,
                    mediaKind,
                    pool.Count,
                    urls.Count,
                    options.PickMode,
                    remaining == int.MaxValue ? "all" : remaining.ToString()));

            foreach (string url in urls)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    return;
                }

                if (TryImportUrl(gameData, mediaKind, url, result))
                {
                    usedUrls.Add(url);
                }
            }
        }

        private List<SteamGridDbResult> FetchSteamGridPool(
            int steamGridGameId,
            SteamGridDbType assetType,
            SteamGridFilters filters,
            BulkMediaDownloadOptions options,
            int remaining,
            CancellationToken cancellationToken)
        {
            List<SteamGridDbResult> pool = new List<SteamGridDbResult>();
            int apiLimit = options.ResolveSteamGridApiLimit();
            bool needPagination = options.DownloadAll
                || (!options.DownloadAll && options.Quantity > BulkMediaDownloadOptions.MaxSteamGridApiPageLimit);

            int page = 0;
            int maxPages = needPagination ? 40 : 1;
            int targetCount = options.DownloadAll
                ? int.MaxValue
                : Math.Max(remaining, options.Quantity);

            while (page < maxPages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                SteamGridDbResultData pageData = _steamGridDbApi.SearchElement(
                    steamGridGameId,
                    assetType,
                    filters,
                    page,
                    apiLimit);

                List<SteamGridDbResult> pageItems = pageData?.Data;
                if (pageItems == null || pageItems.Count == 0)
                {
                    break;
                }

                pool.AddRange(pageItems);

                if (!needPagination || pageItems.Count < apiLimit || pool.Count >= targetCount)
                {
                    break;
                }

                page++;
            }

            return pool;
        }

        private List<string> SelectOrderedSteamGridUrls(
            List<SteamGridDbResult> pool,
            BulkMediaPickMode pickMode,
            int remaining,
            HashSet<string> usedUrls)
        {
            if (pool == null || pool.Count == 0 || remaining <= 0)
            {
                return new List<string>();
            }

            IEnumerable<SteamGridDbResult> ordered;
            switch (pickMode)
            {
                case BulkMediaPickMode.BestScore:
                    ordered = pool.OrderByDescending(x => x.Score).ThenByDescending(x => x.Id);
                    break;
                case BulkMediaPickMode.BestUpvotes:
                    ordered = pool.OrderByDescending(x => x.Upvotes).ThenByDescending(x => x.Id);
                    break;
                case BulkMediaPickMode.NewestById:
                    ordered = pool.OrderByDescending(x => x.Id);
                    break;
                case BulkMediaPickMode.Random:
                default:
                    ordered = pool.OrderBy(_ => _random.Next());
                    break;
            }

            List<string> selected = new List<string>();
            foreach (SteamGridDbResult item in ordered)
            {
                if (item == null || item.Url.IsNullOrEmpty())
                {
                    continue;
                }

                if (usedUrls.Contains(item.Url) || selected.Contains(item.Url))
                {
                    continue;
                }

                selected.Add(item.Url);
                if (selected.Count >= remaining)
                {
                    break;
                }
            }

            return selected;
        }

        private List<string> SelectUrls(
            List<string> urls,
            BulkMediaPickMode pickMode,
            int remaining,
            HashSet<string> usedUrls)
        {
            if (urls == null || urls.Count == 0 || remaining <= 0)
            {
                return new List<string>();
            }

            List<string> candidates = urls
                .Where(u => !u.IsNullOrEmpty() && !usedUrls.Contains(u))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (pickMode == BulkMediaPickMode.Random)
            {
                candidates = candidates.OrderBy(_ => _random.Next()).ToList();
            }

            if (remaining == int.MaxValue)
            {
                return candidates;
            }

            return candidates.Take(remaining).ToList();
        }

        private int? ResolveSteamGridGameId(
            Game game,
            uint steamAppId,
            BulkMediaDownloadOptions options,
            BulkMediaDownloadResult result)
        {
            if (steamAppId != 0)
            {
                SteamGridDbGame bySteam = _steamGridDbApi.GetGameBySteamAppId(steamAppId);
                if (bySteam != null && bySteam.Id > 0)
                {
                    return bySteam.Id;
                }
            }

            SteamGridDbSearchResultData search = _steamGridDbApi.SearchGame(game.Name);
            List<SteamGridDbSearchResult> matches = search?.Data?
                .Where(x => x != null && x.Id > 0 && !x.Name.IsNullOrEmpty())
                .ToList() ?? new List<SteamGridDbSearchResult>();

            if (matches.Count == 0)
            {
                result.SkippedCount++;
                Common.LogDebug(false, string.Format("{0} SGDB match: no results game='{1}'", LogPrefix, game.Name));
                return null;
            }

            string gameName = (game.Name ?? string.Empty).ToLowerInvariant();
            var scored = matches
                .Select(x => new
                {
                    Match = x,
                    Score = Fuzz.Ratio(gameName, x.Name.ToLowerInvariant())
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Match.Verified)
                .ToList();

            var best = scored[0];
            if (best.Score >= options.FuzzyMatchThreshold)
            {
                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} SGDB fuzzy auto game='{1}' match='{2}' score={3}",
                        LogPrefix,
                        game.Name,
                        best.Match.Name,
                        best.Score));
                return best.Match.Id;
            }

            if (!options.PromptOnLowFuzzyMatch)
            {
                result.SkippedCount++;
                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} SGDB fuzzy skip game='{1}' best='{2}' score={3} threshold={4}",
                        LogPrefix,
                        game.Name,
                        best.Match.Name,
                        best.Score,
                        options.FuzzyMatchThreshold));
                return null;
            }

            int? prompted = PromptSteamGridMatch(game, matches);
            if (!prompted.HasValue)
            {
                result.SkippedCount++;
            }

            return prompted;
        }

        private int? PromptSteamGridMatch(Game game, List<SteamGridDbSearchResult> candidates)
        {
            try
            {
                List<GenericItemOption> initial = candidates
                    .Select(x => new GenericItemOption(x.Name, x.Id.ToString()))
                    .ToList();

                GenericItemOption selected = API.Instance.Dialogs.ChooseItemWithSearch(
                    initial,
                    searchTerm =>
                    {
                        if (searchTerm.IsNullOrWhiteSpace())
                        {
                            return new List<GenericItemOption>();
                        }

                        SteamGridDbSearchResultData data = _steamGridDbApi.SearchGame(searchTerm);
                        if (data?.Data == null)
                        {
                            return new List<GenericItemOption>();
                        }

                        return data.Data
                            .Where(x => x != null && x.Id > 0)
                            .Select(x => new GenericItemOption(x.Name, x.Id.ToString()))
                            .ToList();
                    },
                    game.Name,
                    ResourceProvider.GetString("LOCBcBulkFuzzyPrompt"));

                if (selected == null || selected.Description.IsNullOrEmpty())
                {
                    return null;
                }

                if (int.TryParse(selected.Description, out int id) && id > 0)
                {
                    return id;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return null;
        }

        private bool TryImportUrl(
            GameBackgroundImages gameData,
            BackgroundChangerDatabase.PluginMediaKind mediaKind,
            string url,
            BulkMediaDownloadResult result)
        {
            string cachedFile = null;
            Exception downloadError = null;

            for (int attempt = 1; attempt <= DownloadMaxAttempts; attempt++)
            {
                try
                {
                    cachedFile = HttpFileCache.GetWebFile(url);
                    downloadError = null;
                    break;
                }
                catch (Exception ex)
                {
                    downloadError = ex;
                    if (attempt < DownloadMaxAttempts && IsTransientDownloadFailure(ex))
                    {
                        Common.LogDebug(
                            false,
                            string.Format(
                                "{0} Download retry {1}/{2} url={3} reason={4}",
                                LogPrefix,
                                attempt,
                                DownloadMaxAttempts,
                                url,
                                ex.Message));
                        Thread.Sleep(TimeSpan.FromMilliseconds(DownloadRetryBaseDelay.TotalMilliseconds * attempt));
                        continue;
                    }

                    break;
                }
            }

            if (downloadError != null)
            {
                result.ErrorCount++;
                bool transient = IsTransientDownloadFailure(downloadError);
                string message = string.Format("Download failed url={0}", url);
                // Transient CDN / Cloudflare codes: log without toasting the raw WebException.
                Common.LogError(
                    downloadError,
                    isIgnored: false,
                    message,
                    showNotification: !transient,
                    PluginDatabase.PluginName);
                return false;
            }

            try
            {
                if (cachedFile.IsNullOrEmpty() || !File.Exists(cachedFile))
                {
                    result.ErrorCount++;
                    Common.LogDebug(false, string.Format("{0} Download miss url={1}", LogPrefix, url));
                    return false;
                }

                MediaImportService.ImportPrepareResult prepare = _mediaImportService.TryPrepareImportPath(cachedFile);
                if (prepare.Status != MediaImportService.ImportPrepareStatus.Success
                    || prepare.PreparedPath.IsNullOrEmpty())
                {
                    result.ErrorCount++;
                    Common.LogDebug(
                        false,
                        string.Format("{0} Import prepare failed status={1} url={2}", LogPrefix, prepare.Status, url));
                    return false;
                }

                string originalPath = prepare.PreparedPath;
                string ext = Path.GetExtension(originalPath);
                Guid imageGuid = Guid.NewGuid();
                ItemImage itemImage = new ItemImage
                {
                    Name = imageGuid.ToString() + ext,
                    FolderName = gameData.Id.ToString()
                };
                BackgroundChangerDatabase.SetItemMediaKind(itemImage, mediaKind);

                string dir = Path.GetDirectoryName(itemImage.FullPath);
                FileSystem.CreateDirectory(dir);
                File.Copy(originalPath, itemImage.FullPath, overwrite: true);

                if (gameData.Items == null)
                {
                    gameData.Items = new List<ItemImage>();
                }

                gameData.Items.Add(itemImage);
                PluginDatabase.Update(gameData);
                result.ImportedCount++;

                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} Imported game='{1}' kind={2} file={3}",
                        LogPrefix,
                        gameData.Name,
                        mediaKind,
                        itemImage.FullPath));
                return true;
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                Common.LogError(
                    ex,
                    false,
                    string.Format("Import failed after download url={0}", url),
                    true,
                    PluginDatabase.PluginName);
                return false;
            }
        }

        /// <summary>
        /// Returns true for transient download failures (rate limit / Cloudflare origin errors).
        /// </summary>
        /// <param name="ex">Exception from <see cref="HttpFileCache.GetWebFile"/>.</param>
        /// <returns><c>true</c> when a short retry is warranted.</returns>
        private static bool IsTransientDownloadFailure(Exception ex)
        {
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                WebException webException = current as WebException;
                if (webException == null)
                {
                    continue;
                }

                HttpWebResponse response = webException.Response as HttpWebResponse;
                if (response != null)
                {
                    int statusCode = (int)response.StatusCode;
                    if (IsTransientHttpStatusCode(statusCode))
                    {
                        return true;
                    }
                }

                // Cloudflare often surfaces "(520)" / "(525)" in the message with a null Response.
                string message = webException.Message ?? string.Empty;
                if (ContainsHttpStatusMarker(message, 429)
                    || ContainsHttpStatusMarker(message, 520)
                    || ContainsHttpStatusMarker(message, 525))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTransientHttpStatusCode(int statusCode)
        {
            return statusCode == 429 || statusCode == 520 || statusCode == 525;
        }

        private static bool ContainsHttpStatusMarker(string message, int statusCode)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            return message.IndexOf("(" + statusCode + ")", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasPluginOwnedMedia(
            GameBackgroundImages gameData,
            BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            return CountPluginOwnedMedia(gameData, mediaKind) > 0;
        }

        /// <summary>
        /// Counts plugin-owned images for a media kind (excludes Playnite library mirrors).
        /// </summary>
        /// <param name="gameData">Plugin DB entry.</param>
        /// <param name="mediaKind">Background, cover, or icon.</param>
        /// <returns>Number of existing plugin-owned items for the kind.</returns>
        private static int CountPluginOwnedMedia(
            GameBackgroundImages gameData,
            BackgroundChangerDatabase.PluginMediaKind mediaKind)
        {
            if (gameData?.Items == null)
            {
                return 0;
            }

            int count = 0;
            foreach (ItemImage item in gameData.Items)
            {
                if (item == null)
                {
                    continue;
                }

                if (!BackgroundChangerDatabase.ItemMatchesMediaKind(item, mediaKind))
                {
                    continue;
                }

                if (!item.Exist
                    || BackgroundChangerDatabase.IsPlayniteLibraryMirror(item)
                    || item.FolderName.IsNullOrEmpty())
                {
                    continue;
                }

                count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Aggregate counters for a bulk media download run.
    /// </summary>
    public class BulkMediaDownloadResult
    {
        /// <summary>
        /// Gets or sets how many assets were successfully imported.
        /// </summary>
        public int ImportedCount { get; set; }

        /// <summary>
        /// Gets or sets how many games/kinds were skipped (missing match, already present, etc.).
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Gets or sets how many download / import failures occurred.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Gets or sets whether the run was cancelled mid-batch.
        /// </summary>
        public bool Cancelled { get; set; }
    }
}
