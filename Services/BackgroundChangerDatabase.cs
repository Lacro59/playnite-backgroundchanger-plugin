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
    public class BackgroundChangerDatabase : PluginDatabaseObject<BackgroundChangerSettings, BackgroundImagesCollection, GameBackgroundImages>
    {
        public BackgroundChangerDatabase(IPlayniteAPI PlayniteApi, BackgroundChangerSettings PluginSettings, string PluginUserDataPath) : base(PlayniteApi, PluginSettings, PluginUserDataPath)
        {
            PluginName = "BackgroundChanger";

            ControlAndCreateDirectory(PluginUserDataPath, "BackgroundChanger");
        }

        protected override bool LoadDatabase()
        {
            IsLoaded = false;
            Database = new BackgroundImagesCollection(PluginDatabaseDirectory);
            Database.SetGameInfo<BackgroundImage>(_PlayniteApi);

            GameSelectedData = new GameBackgroundImages();
            GetPluginTags();

            IsLoaded = true;
            return true;
        }

        public override GameBackgroundImages Get(Guid Id, bool OnlyCache = false)
        {
            GameIsLoaded = false;
            GameBackgroundImages gameBackgroundImages = base.GetOnlyCache(Id);
#if DEBUG
            logger.Debug($"{PluginName} [Ignored] - GetFromDb({Id.ToString()}) - gameAchievements: {JsonConvert.SerializeObject(gameBackgroundImages)}");
#endif

            if (gameBackgroundImages == null)
            {
                Game game = _PlayniteApi.Database.Games.Get(Id);
                gameBackgroundImages = GetDefault(game);
                Add(gameBackgroundImages);
            }

            if (!gameBackgroundImages.BackgroundImage.IsNullOrEmpty() && gameBackgroundImages.Items.Find(x => x.IsDefault) == null)
            {
                gameBackgroundImages.Items.Insert(0, new BackgroundImage
                {
                    Name = _PlayniteApi.Database.GetFullFilePath(gameBackgroundImages.BackgroundImage),
                    IsDefault = true
                });
            }

            GameIsLoaded = true;
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
