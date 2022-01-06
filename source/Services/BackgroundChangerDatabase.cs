using BackgroundChanger.Models;
using CommonPlayniteShared;
using CommonPluginsShared.Collections;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using CommonPluginsShared;

namespace BackgroundChanger.Services
{
    public class BackgroundChangerDatabase : PluginDatabaseObject<BackgroundChangerSettingsViewModel, BackgroundImagesCollection, GameBackgroundImages, ItemImage>
    {
        public BackgroundChangerDatabase(IPlayniteAPI PlayniteApi, BackgroundChangerSettingsViewModel PluginSettings, string PluginUserDataPath) : base(PlayniteApi, PluginSettings, "BackgroundChanger", PluginUserDataPath)
        {

        }


        protected override bool LoadDatabase()
        {
            try
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                Database = new BackgroundImagesCollection(Paths.PluginDatabasePath);
                Database.SetGameInfo<ItemImage>(PlayniteApi);

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                logger.Info($"LoadDatabase with {Database.Count} items - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
                return false;
            }

            return true;
        }


        public override GameBackgroundImages Get(Guid Id, bool OnlyCache = false, bool Force = false)
        {
            GameBackgroundImages gameBackgroundImages = GetOnlyCache(Id);

            if (gameBackgroundImages == null)
            {
                Game game = PlayniteApi.Database.Games.Get(Id);
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

            // Check default background
            if (gameBackgroundImages.Items.Find(x => x.IsDefault && !x.IsCover) != null)
            {
                int Index = gameBackgroundImages.Items.FindIndex(x => x.IsDefault && !x.IsCover);
                if (Index != -1)
                {
                    gameBackgroundImages.Items.RemoveAt(Index);
                }
            }
            if (!gameBackgroundImages.BackgroundImage.IsNullOrEmpty() && gameBackgroundImages.Items.Find(x => x.IsDefault && !x.IsCover) == null)
            {
                string PathImage = ImageSourceManager.GetImagePath(gameBackgroundImages.BackgroundImage);
                if (PathImage.IsNullOrEmpty() && !File.Exists(PathImage))
                {
                    PathImage = PlayniteApi.Database.GetFullFilePath(gameBackgroundImages.BackgroundImage);
                }

                gameBackgroundImages.Items.Insert(0, new ItemImage
                {
                    Name = PathImage,
                    IsCover = false,
                    IsDefault = true
                });
            }

            // Check default cover
            if (gameBackgroundImages.Items.Find(x => x.IsDefault && x.IsCover) != null)
            {
                int Index = gameBackgroundImages.Items.FindIndex(x => x.IsDefault && x.IsCover);
                if (Index != -1)
                {
                    gameBackgroundImages.Items.RemoveAt(Index);
                }
            }
            if (!gameBackgroundImages.CoverImage.IsNullOrEmpty() && gameBackgroundImages.Items.Find(x => x.IsDefault && x.IsCover) == null)
            {
                string PathImage = ImageSourceManager.GetImagePath(gameBackgroundImages.CoverImage);
                if (PathImage.IsNullOrEmpty() && !File.Exists(PathImage))
                {
                    PathImage = PlayniteApi.Database.GetFullFilePath(gameBackgroundImages.CoverImage);
                }

                gameBackgroundImages.Items.Insert(0, new ItemImage
                {
                    Name = PathImage,
                    IsCover = true,
                    IsDefault = true
                });
            }

            return gameBackgroundImages;
        }


        public override void SetThemesResources(Game game)
        {
            GameBackgroundImages gameBackgroundImages = Get(game, true);

            if (gameBackgroundImages == null)
            {
                PluginSettings.Settings.HasDataBackground = false;
                PluginSettings.Settings.HasDataCover = false;

                return;
            }

            PluginSettings.Settings.HasDataBackground = gameBackgroundImages.HasDataBackground;
            PluginSettings.Settings.HasDataCover = gameBackgroundImages.HasDataCover;
        }
    }
}
