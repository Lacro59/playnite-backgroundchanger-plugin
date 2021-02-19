using BackgroundChanger.Models;
using CommonPluginsShared.Collections;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
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
            Database.SetGameInfo<BackgroundImage>(PlayniteApi);
            GetPluginTags();

            return true;
        }

        public override GameBackgroundImages Get(Guid Id, bool OnlyCache = false)
        {
            GameBackgroundImages gameBackgroundImages = GetOnlyCache(Id);

            if (gameBackgroundImages == null)
            {
                Game game = PlayniteApi.Database.Games.Get(Id);
                gameBackgroundImages = GetDefault(game);
                Add(gameBackgroundImages);
            }

            if (!gameBackgroundImages.BackgroundImage.IsNullOrEmpty() && gameBackgroundImages.Items.Find(x => x.IsDefault) == null)
            {
                gameBackgroundImages.Items.Insert(0, new BackgroundImage
                {
                    Name = PlayniteApi.Database.GetFullFilePath(gameBackgroundImages.BackgroundImage),
                    IsDefault = true
                });
            }

            return gameBackgroundImages;
        }


        public override void Update(GameBackgroundImages itemToUpdate)
        {
            BackgroundImage BackgroundDefault = itemToUpdate.Items.Find(x => x.IsDefault);
            itemToUpdate.Items.Remove(BackgroundDefault);

            base.Update(itemToUpdate);
        }
    }
}
