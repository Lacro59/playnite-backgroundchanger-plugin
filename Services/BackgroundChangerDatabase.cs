using BackgroundChanger.Models;
using CommonPlayniteShared;
using CommonPluginsShared.Collections;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Services
{
    public class BackgroundChangerDatabase : PluginDatabaseObject<BackgroundChangerSettingsViewModel, BackgroundImagesCollection, GameBackgroundImages>
    {
        public BackgroundChangerDatabase(IPlayniteAPI PlayniteApi, BackgroundChangerSettingsViewModel PluginSettings, string PluginUserDataPath) : base(PlayniteApi, PluginSettings, "BackgroundChanger", PluginUserDataPath)
        {

        }


        protected override bool LoadDatabase()
        {
            Database = new BackgroundImagesCollection(Paths.PluginDatabasePath);
            Database.SetGameInfo<ItemImage>(PlayniteApi);
            GetPluginTags();

            return true;
        }


        public override GameBackgroundImages Get(Guid Id, bool OnlyCache = false, bool Force = false)
        {
            GameBackgroundImages gameBackgroundImages = GetOnlyCache(Id);

            if (gameBackgroundImages == null)
            {
                Game game = PlayniteApi.Database.Games.Get(Id);
                gameBackgroundImages = GetDefault(game);
                Add(gameBackgroundImages);
            }

            // Check default background
            if (gameBackgroundImages.Items.Find(x => x.IsDefault && !x.IsCover) != null)
            {
                int Index = gameBackgroundImages.Items.FindIndex(x => x.IsDefault && !x.IsCover);
                if (Index != -1)
                {
                    string FullPath = gameBackgroundImages.Items[Index].FullPath;
                    if (!File.Exists(FullPath))
                    {
                        gameBackgroundImages.Items.RemoveAt(Index);
                    }
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
                    string FullPath = gameBackgroundImages.Items[Index].FullPath;
                    if (!File.Exists(FullPath))
                    {
                        gameBackgroundImages.Items.RemoveAt(0);
                    }
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

            //PluginSettings.Settings.HasData = gameBackgroundImages.HasData;
            PluginSettings.Settings.HasDataBackground = gameBackgroundImages.HasDataBackground;
            PluginSettings.Settings.HasDataCover = gameBackgroundImages.HasDataCover;
        }

        public override void Games_ItemUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            foreach (var GameUpdated in e.UpdatedItems)
            {
                Database.SetGameInfo<ItemImage>(PlayniteApi, GameUpdated.NewData.Id);
            }
        }
    }
}
