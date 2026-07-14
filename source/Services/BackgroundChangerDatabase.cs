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
        /// Playnite library paths must not be stored in plugin JSON (re-injected on <see cref="Get"/>).
        /// </summary>
        internal static bool IsPlayniteLibraryMirror(ItemImage item)
        {
            return item != null
                && item.FolderName.IsNullOrEmpty()
                && !item.Name.IsNullOrEmpty()
                && Path.IsPathRooted(item.Name);
        }

        /// <inheritdoc/>
        public override void Update(GameBackgroundImages itemToUpdate)
        {
            PrepareItemsForSerialization(itemToUpdate);
            base.Update(itemToUpdate);
        }

        /// <summary>
        /// Strips ephemeral Playnite default mirrors and <see cref="ItemImage.IsDefault"/> before JSON persistence.
        /// Default mirrors are re-injected on the next <see cref="Get"/>.
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
            bool favoriteBackground = gameBackgroundImages.Items.Exists(x => x.IsFavorite && !x.IsCover && x.Exist);
            bool favoriteCover = gameBackgroundImages.Items.Exists(x => x.IsFavorite && x.IsCover && x.Exist);
            int itemsCountBefore = gameBackgroundImages.Items.Count;

            SyncDefaultMediaItem(gameBackgroundImages, isCover: false, gameBackgroundImages.BackgroundImage);
            SyncDefaultMediaItem(gameBackgroundImages, isCover: true, gameBackgroundImages.CoverImage);

            int purgedCount = PurgeDeadMediaItems(gameBackgroundImages);

            bool favoriteReappliedBackground = ReapplyFavoriteIfNeeded(gameBackgroundImages, isCover: false, wasFavorite: favoriteBackground);
            bool favoriteReappliedCover = ReapplyFavoriteIfNeeded(gameBackgroundImages, isCover: true, wasFavorite: favoriteCover);
            bool favoriteReapplied = favoriteReappliedBackground || favoriteReappliedCover;

            LogRefreshSummary(
                gameBackgroundImages,
                trigger,
                itemsCountBefore,
                purgedCount,
                favoriteBackground,
                favoriteCover,
                favoriteReappliedBackground,
                favoriteReappliedCover);

            return new RefreshMediaResult
            {
                PurgedCount = purgedCount,
                FavoriteReapplied = favoriteReapplied
            };
        }

        /// <summary>
        /// Replaces the persisted default item for the given media type with a mirror of the Playnite game image field.
        /// </summary>
        private static void SyncDefaultMediaItem(GameBackgroundImages gameBackgroundImages, bool isCover, string gameImageReference)
        {
            int defaultIndex = gameBackgroundImages.Items.FindIndex(x => x.IsDefault && x.IsCover == isCover);
            if (defaultIndex != -1)
            {
                gameBackgroundImages.Items.RemoveAt(defaultIndex);
            }

            if (gameImageReference.IsNullOrEmpty())
            {
                return;
            }

            if (gameBackgroundImages.Items.Exists(x => x.IsDefault && x.IsCover == isCover))
            {
                return;
            }

            string pathImage = ResolveGameImagePath(gameImageReference);
            if (pathImage.IsNullOrEmpty())
            {
                return;
            }

            gameBackgroundImages.Items.Insert(0, new ItemImage
            {
                Name = pathImage,
                IsCover = isCover,
                IsDefault = true
            });
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
        private static bool ReapplyFavoriteIfNeeded(GameBackgroundImages gameBackgroundImages, bool isCover, bool wasFavorite)
        {
            if (!wasFavorite)
            {
                return false;
            }

            if (gameBackgroundImages.Items.Exists(x => x.IsCover == isCover && x.IsFavorite && x.Exist))
            {
                return false;
            }

            ItemImage defaultItem = gameBackgroundImages.Items.Find(x => x.IsDefault && x.IsCover == isCover);
            if (defaultItem != null && defaultItem.Exist)
            {
                defaultItem.IsFavorite = true;
                return true;
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} Favorite not reapplied — no default item (isCover={1}, game='{2}')",
                    LogPrefix,
                    isCover,
                    gameBackgroundImages.Name));

            return false;
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
            bool favoriteReappliedBackground,
            bool favoriteReappliedCover)
        {
            bool favoriteReapplied = favoriteReappliedBackground || favoriteReappliedCover;
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
                    "{0} RefreshMedia game='{1}', trigger={2}, items {3}->{4}, purged={5}, favCaptured cover={6} bg={7}, favReapplied cover={8} bg={9}",
                    LogPrefix,
                    gameBackgroundImages.Name,
                    trigger ?? "Get",
                    itemsCountBefore,
                    gameBackgroundImages.Items.Count,
                    purgedCount,
                    favoriteCover,
                    favoriteBackground,
                    favoriteReappliedCover,
                    favoriteReappliedBackground));
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
            if (!coverChanged && !backgroundChanged)
            {
                return;
            }

            Common.LogDebug(
                false,
                string.Format(
                    "{0} Games_ItemUpdated game='{1}', coverChanged={2}, backgroundChanged={3}",
                    LogPrefix,
                    gameNew.Name,
                    coverChanged,
                    backgroundChanged));

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
                || backgroundChanged;

            if (shouldPersist)
            {
                Update(gameBackgroundImages);
                Common.LogDebug(
                    false,
                    string.Format(
                        "{0} Games_ItemUpdated persisted refresh for game='{1}' (purged={2}, favReapplied={3}, coverChanged={4}, bgChanged={5})",
                        LogPrefix,
                        gameNew.Name,
                        refreshResult.PurgedCount,
                        refreshResult.FavoriteReapplied,
                        coverChanged,
                        backgroundChanged));
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

                return;
            }

            PluginSettings.HasDataBackground = gameBackgroundImages.HasDataBackground;
            PluginSettings.HasDataCover = gameBackgroundImages.HasDataCover;
        }
    }
}
