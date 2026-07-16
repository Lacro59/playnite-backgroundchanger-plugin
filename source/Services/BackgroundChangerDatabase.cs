using BackgroundChanger.Models;
using CommonPlayniteShared;
using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.IO;

namespace BackgroundChanger.Services
{
    public class BackgroundChangerDatabase : PluginDatabaseObject<BackgroundChangerSettings, GameBackgroundImages, ItemImage>
    {
        private const string LogPrefix = "[BackgroundChangerDatabase]";

        /// <summary>
        /// Plugin media kinds stored in <see cref="GameBackgroundImages.Items"/>.
        /// </summary>
        public enum PluginMediaKind
        {
            Background,
            Cover,
            Icon
        }

        /// <summary>
        /// Outcome of an in-memory media refresh; drives whether the caller should persist to disk.
        /// </summary>
        private struct RefreshMediaResult
        {
            internal int PurgedCount;
            internal bool FavoriteReapplied;
        }

        public BackgroundChangerDatabase(BackgroundChangerSettings pluginSettings, string pluginUserDataPath) : base(pluginSettings, "BackgroundChanger", pluginUserDataPath)
        {
        }

        /// <summary>
        /// When set, <see cref="ActionAfterGames_ItemUpdated"/> refreshes memory only (ImagesManager already persisted).
        /// </summary>
        internal static bool SuppressGamesItemUpdatedPersist { get; set; }

        /// <summary>
        /// Ephemeral Playnite default mirrors must not be stored in plugin JSON (re-injected on <see cref="Get"/>).
        /// Pending imports (file picker, SteamGridDB cache) also use rooted paths but are not mirrors.
        /// </summary>
        internal static bool IsPlayniteLibraryMirror(ItemImage item)
        {
            if (item == null || !item.FolderName.IsNullOrEmpty() || item.Name.IsNullOrEmpty())
            {
                return false;
            }

            if (!Path.IsPathRooted(item.Name))
            {
                return false;
            }

            if (IsUnderPlayniteLibraryFiles(item.Name))
            {
                return true;
            }

            // Resolved default mirror outside library\files (e.g. alternate Playnite image location).
            return item.IsDefault;
        }

        private static bool IsUnderPlayniteLibraryFiles(string path)
        {
            if (path.IsNullOrEmpty())
            {
                return false;
            }

            string normalized = path.Replace('/', '\\');
            return normalized.IndexOf("\\library\\files\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <inheritdoc/>
        public override void Update(GameBackgroundImages itemToUpdate)
        {
            PrepareItemsForSerialization(itemToUpdate);
            base.Update(itemToUpdate);
            RefreshGameMediaItems(itemToUpdate, "Update");
        }

        /// <summary>
        /// Strips ephemeral Playnite default mirrors and <see cref="ItemImage.IsDefault"/> before JSON persistence.
        /// Default mirrors are re-injected immediately after <see cref="Update"/> and on each <see cref="Get"/>.
        /// </summary>
        internal static void PrepareItemsForSerialization(GameBackgroundImages gameBackgroundImages)
        {
            if (gameBackgroundImages?.Items == null)
            {
                return;
            }

            gameBackgroundImages.Items.RemoveAll(IsPlayniteLibraryMirror);
            gameBackgroundImages.Items.RemoveAll(x => !x.Exist);

            foreach (ItemImage item in gameBackgroundImages.Items)
            {
                item.IsDefault = false;
            }
        }

        public override GameBackgroundImages Get(Guid id, bool onlyCache = false, bool force = false)
        {
            GameBackgroundImages gameBackgroundImages = GetOnlyCache(id);

            if (gameBackgroundImages == null)
            {
                Game game = API.Instance.Database.Games.Get(id);
                if (game != null)
                {
                    gameBackgroundImages = GetDefault(game);
                    Add(gameBackgroundImages);
                }
                else
                {
                    return gameBackgroundImages;
                }
            }

            RefreshGameMediaItems(gameBackgroundImages);

            return gameBackgroundImages;
        }

        /// <summary>
        /// Resyncs Playnite default media mirrors, purges dead entries, and restores favorite flags.
        /// </summary>
        /// <param name="trigger">Optional caller label for diagnostic logs (e.g. Games_ItemUpdated).</param>
        /// <returns>Purge and favorite-reapply flags; disk persist is caller responsibility.</returns>
        private RefreshMediaResult RefreshGameMediaItems(GameBackgroundImages gameBackgroundImages, string trigger = null)
        {
            bool favoriteBackground = gameBackgroundImages.Items.Exists(x => x.IsFavorite && ItemMatchesMediaKind(x, PluginMediaKind.Background) && x.Exist);
            bool favoriteCover = gameBackgroundImages.Items.Exists(x => x.IsFavorite && ItemMatchesMediaKind(x, PluginMediaKind.Cover) && x.Exist);
            bool favoriteIcon = gameBackgroundImages.Items.Exists(x => x.IsFavorite && ItemMatchesMediaKind(x, PluginMediaKind.Icon) && x.Exist);
            int itemsCountBefore = gameBackgroundImages.Items.Count;

            SyncDefaultMediaItem(gameBackgroundImages, PluginMediaKind.Background, gameBackgroundImages.BackgroundImage);
            SyncDefaultMediaItem(gameBackgroundImages, PluginMediaKind.Cover, gameBackgroundImages.CoverImage);
            SyncDefaultMediaItem(gameBackgroundImages, PluginMediaKind.Icon, gameBackgroundImages.Icon);

            int purgedCount = PurgeDeadMediaItems(gameBackgroundImages);

            bool favoriteReappliedBackground = ReapplyFavoriteIfNeeded(gameBackgroundImages, PluginMediaKind.Background, wasFavorite: favoriteBackground);
            bool favoriteReappliedCover = ReapplyFavoriteIfNeeded(gameBackgroundImages, PluginMediaKind.Cover, wasFavorite: favoriteCover);
            bool favoriteReappliedIcon = ReapplyFavoriteIfNeeded(gameBackgroundImages, PluginMediaKind.Icon, wasFavorite: favoriteIcon);
            bool favoriteReapplied = favoriteReappliedBackground || favoriteReappliedCover || favoriteReappliedIcon;

            LogRefreshSummary(
                gameBackgroundImages,
                trigger,
                itemsCountBefore,
                purgedCount,
                favoriteBackground,
                favoriteCover,
                favoriteIcon,
                favoriteReappliedBackground,
                favoriteReappliedCover,
                favoriteReappliedIcon);

            return new RefreshMediaResult
            {
                PurgedCount = purgedCount,
                FavoriteReapplied = favoriteReapplied
            };
        }

        /// <summary>
        /// Replaces the persisted default item for the given media type with a mirror of the Playnite game image field.
        /// </summary>
        private static void SyncDefaultMediaItem(GameBackgroundImages gameBackgroundImages, PluginMediaKind mediaKind, string gameImageReference)
        {
            int defaultIndex = gameBackgroundImages.Items.FindIndex(x => x.IsDefault && ItemMatchesMediaKind(x, mediaKind));
            if (defaultIndex != -1)
            {
                gameBackgroundImages.Items.RemoveAt(defaultIndex);
            }

            if (gameImageReference.IsNullOrEmpty())
            {
                return;
            }

            if (gameBackgroundImages.Items.Exists(x => x.IsDefault && ItemMatchesMediaKind(x, mediaKind)))
            {
                return;
            }

            string pathImage = ResolveGameImagePath(gameImageReference);
            if (pathImage.IsNullOrEmpty())
            {
                return;
            }

            ItemImage defaultItem = new ItemImage
            {
                Name = pathImage,
                IsDefault = true
            };
            SetItemMediaKind(defaultItem, mediaKind);
            gameBackgroundImages.Items.Insert(0, defaultItem);
        }

        /// <summary>
        /// Removes serialized items whose files no longer exist, including ephemeral default mirrors.
        /// Playnite default mirrors are re-injected by <see cref="SyncDefaultMediaItem"/> on the next refresh.
        /// Does not delete files from disk.
        /// </summary>
        /// <returns>Number of entries removed from the in-memory list.</returns>
        private static int PurgeDeadMediaItems(GameBackgroundImages gameBackgroundImages)
        {
            int countBefore = gameBackgroundImages.Items.Count;
            gameBackgroundImages.Items.RemoveAll(x => !x.Exist);
            return countBefore - gameBackgroundImages.Items.Count;
        }

        /// <summary>
        /// Restores <see cref="ItemImage.IsFavorite"/> on the re-injected default item when the flag was lost during resync.
        /// Skips reapplication when a living extension item already carries the favorite for this media type.
        /// </summary>
        /// <returns><c>true</c> when the favorite flag was set on the default item.</returns>
        private static bool ReapplyFavoriteIfNeeded(GameBackgroundImages gameBackgroundImages, PluginMediaKind mediaKind, bool wasFavorite)
        {
            if (!wasFavorite)
            {
                return false;
            }

            if (gameBackgroundImages.Items.Exists(x => ItemMatchesMediaKind(x, mediaKind) && x.IsFavorite && x.Exist))
            {
                return false;
            }

            ItemImage defaultItem = gameBackgroundImages.Items.Find(x => x.IsDefault && ItemMatchesMediaKind(x, mediaKind));
            if (defaultItem != null && defaultItem.Exist)
            {
                defaultItem.IsFavorite = true;
                return true;
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} Favorite not reapplied — no default item (mediaKind={1}, game='{2}')",
                    LogPrefix,
                    mediaKind,
                    gameBackgroundImages.Name));

            return false;
        }

        internal static bool ItemMatchesMediaKind(ItemImage item, PluginMediaKind mediaKind)
        {
            if (item == null)
            {
                return false;
            }

            switch (mediaKind)
            {
                case PluginMediaKind.Background:
                    return item.IsBackgroundMedia;
                case PluginMediaKind.Cover:
                    return item.IsCover && !item.IsIcon;
                case PluginMediaKind.Icon:
                    return item.IsIcon;
                default:
                    return false;
            }
        }

        internal static void SetItemMediaKind(ItemImage item, PluginMediaKind mediaKind)
        {
            switch (mediaKind)
            {
                case PluginMediaKind.Background:
                    item.IsCover = false;
                    item.IsIcon = false;
                    break;
                case PluginMediaKind.Cover:
                    item.IsCover = true;
                    item.IsIcon = false;
                    break;
                case PluginMediaKind.Icon:
                    item.IsCover = false;
                    item.IsIcon = true;
                    break;
            }
        }

        private static string ResolveGameImagePath(string gameImageReference)
        {
            string pathImage = ImageSourceManager.GetImagePath(gameImageReference);
            if (pathImage.IsNullOrEmpty() || !File.Exists(pathImage))
            {
                pathImage = API.Instance.Database.GetFullFilePath(gameImageReference);
            }

            return pathImage;
        }

        private static void LogRefreshSummary(
            GameBackgroundImages gameBackgroundImages,
            string trigger,
            int itemsCountBefore,
            int purgedCount,
            bool favoriteBackground,
            bool favoriteCover,
            bool favoriteIcon,
            bool favoriteReappliedBackground,
            bool favoriteReappliedCover,
            bool favoriteReappliedIcon)
        {
            bool favoriteReapplied = favoriteReappliedBackground || favoriteReappliedCover || favoriteReappliedIcon;
            bool significant = purgedCount > 0 || favoriteReapplied || !trigger.IsNullOrEmpty();

            if (!significant)
            {
                Common.LogDebug(
                    true,
                    string.Format(
                        "{0} RefreshMedia skipped log (no purge/favorite change), game='{1}', items={2}",
                        LogPrefix,
                        gameBackgroundImages.Name,
                        gameBackgroundImages.Items.Count));
                return;
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} RefreshMedia game='{1}', trigger={2}, items {3}->{4}, purged={5}, favCaptured cover={6} bg={7} icon={8}, favReapplied cover={9} bg={10} icon={11}",
                    LogPrefix,
                    gameBackgroundImages.Name,
                    trigger ?? "Get",
                    itemsCountBefore,
                    gameBackgroundImages.Items.Count,
                    purgedCount,
                    favoriteCover,
                    favoriteBackground,
                    favoriteIcon,
                    favoriteReappliedCover,
                    favoriteReappliedBackground,
                    favoriteReappliedIcon));
        }

        /// <inheritdoc/>
        public override void ActionAfterGames_ItemUpdated(Game gameOld, Game gameNew)
        {
            if (gameOld == null || gameNew == null)
            {
                return;
            }

            bool coverChanged = !gameOld.CoverImage.IsEqual(gameNew.CoverImage);
            bool backgroundChanged = !gameOld.BackgroundImage.IsEqual(gameNew.BackgroundImage);
            bool iconChanged = !gameOld.Icon.IsEqual(gameNew.Icon);
            if (!coverChanged && !backgroundChanged && !iconChanged)
            {
                return;
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} Games_ItemUpdated game='{1}', coverChanged={2}, backgroundChanged={3}, iconChanged={4}",
                    LogPrefix,
                    gameNew.Name,
                    coverChanged,
                    backgroundChanged,
                    iconChanged));

            GameBackgroundImages gameBackgroundImages = GetOnlyCache(gameNew.Id);
            if (gameBackgroundImages == null)
            {
                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} Games_ItemUpdated skip — no plugin cache for game='{1}'",
                        LogPrefix,
                        gameNew.Name));
                return;
            }

            RefreshMediaResult refreshResult = RefreshGameMediaItems(gameBackgroundImages, "Games_ItemUpdated");

            if (SuppressGamesItemUpdatedPersist)
            {
                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} Games_ItemUpdated memory refresh only (persist suppressed) for game='{1}'",
                        LogPrefix,
                        gameNew.Name));
                return;
            }

            bool shouldPersist = refreshResult.PurgedCount > 0
                || refreshResult.FavoriteReapplied
                || coverChanged
                || backgroundChanged
                || iconChanged;

            if (shouldPersist)
            {
                Update(gameBackgroundImages);
                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} Games_ItemUpdated persisted refresh for game='{1}' (purged={2}, favReapplied={3}, coverChanged={4}, bgChanged={5}, iconChanged={6})",
                        LogPrefix,
                        gameNew.Name,
                        refreshResult.PurgedCount,
                        refreshResult.FavoriteReapplied,
                        coverChanged,
                        backgroundChanged,
                        iconChanged));
            }
            else
            {
                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} Games_ItemUpdated memory refresh only (no persist) for game='{1}'",
                        LogPrefix,
                        gameNew.Name));
            }
        }

        public override void SetThemesResources(Game game)
        {
            GameBackgroundImages gameBackgroundImages = Get(game, true);

            if (gameBackgroundImages == null)
            {
                PluginSettings.HasDataBackground = false;
                PluginSettings.HasDataCover = false;
                PluginSettings.HasDataIcon = false;

                return;
            }

            PluginSettings.HasDataBackground = gameBackgroundImages.HasDataBackground;
            PluginSettings.HasDataCover = gameBackgroundImages.HasDataCover;
            PluginSettings.HasDataIcon = gameBackgroundImages.HasDataIcon;
        }
    }
}
