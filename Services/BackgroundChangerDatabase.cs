using BackgroundChanger.Models;
using CommonPluginsShared.Collections;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
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
                string FullPath = gameBackgroundImages.Items.Find(x => x.IsDefault && !x.IsCover).FullPath;
                if (!File.Exists(FullPath))
                {
                    gameBackgroundImages.Items.RemoveAt(0);
                }
            }
            if (!gameBackgroundImages.BackgroundImage.IsNullOrEmpty() && gameBackgroundImages.Items.Find(x => x.IsDefault && !x.IsCover) == null)
            {
                gameBackgroundImages.Items.Insert(0, new ItemImage
                {
                    Name = PlayniteApi.Database.GetFullFilePath(gameBackgroundImages.BackgroundImage),
                    IsCover = false,
                    IsDefault = true
                });
            }

            // Check default cover
            if (gameBackgroundImages.Items.Find(x => x.IsDefault && x.IsCover) != null)
            {
                string FullPath = gameBackgroundImages.Items.Find(x => x.IsDefault && x.IsCover).FullPath;
                if (!File.Exists(FullPath))
                {
                    gameBackgroundImages.Items.RemoveAt(0);
                }
            }
            if (!gameBackgroundImages.CoverImage.IsNullOrEmpty() && gameBackgroundImages.Items.Find(x => x.IsDefault && x.IsCover) == null)
            {
                gameBackgroundImages.Items.Insert(0, new ItemImage
                {
                    Name = PlayniteApi.Database.GetFullFilePath(gameBackgroundImages.CoverImage),
                    IsCover = true,
                    IsDefault = true
                });
            }

            return gameBackgroundImages;
        }


        public override void SetThemesResources(Game game)
        {
            GameBackgroundImages gameBackgroundImages = Get(game, true);

            PluginSettings.Settings.HasData = gameBackgroundImages.HasData;
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
