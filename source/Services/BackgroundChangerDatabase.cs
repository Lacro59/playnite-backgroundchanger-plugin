using BackgroundChanger.Models;
using CommonPlayniteShared;
using CommonPluginsShared.Collections;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.IO;

namespace BackgroundChanger.Services
{
    public class BackgroundChangerDatabase : PluginDatabaseObject<BackgroundChangerSettingsViewModel, BackgroundImagesCollection, GameBackgroundImages, ItemImage>
    {
        public BackgroundChangerDatabase(BackgroundChangerSettingsViewModel pluginSettings, string pluginUserDataPath) : base(pluginSettings, "BackgroundChanger", pluginUserDataPath)
        {
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

            // Check default background
            if (gameBackgroundImages.Items.Find(x => x.IsDefault && !x.IsCover) != null)
            {
                int index = gameBackgroundImages.Items.FindIndex(x => x.IsDefault && !x.IsCover);
                if (index != -1)
                {
                    gameBackgroundImages.Items.RemoveAt(index);
                }
            }
            if (!gameBackgroundImages.BackgroundImage.IsNullOrEmpty() && gameBackgroundImages.Items.Find(x => x.IsDefault && !x.IsCover) == null)
            {
                string pathImage = ImageSourceManager.GetImagePath(gameBackgroundImages.BackgroundImage);
                if (pathImage.IsNullOrEmpty() && !File.Exists(pathImage))
                {
                    pathImage = API.Instance.Database.GetFullFilePath(gameBackgroundImages.BackgroundImage);
                }

                gameBackgroundImages.Items.Insert(0, new ItemImage
                {
                    Name = pathImage,
                    IsCover = false,
                    IsDefault = true
                });
            }

            // Check default cover
            if (gameBackgroundImages.Items.Find(x => x.IsDefault && x.IsCover) != null)
            {
                int index = gameBackgroundImages.Items.FindIndex(x => x.IsDefault && x.IsCover);
                if (index != -1)
                {
                    gameBackgroundImages.Items.RemoveAt(index);
                }
            }
            if (!gameBackgroundImages.CoverImage.IsNullOrEmpty() && gameBackgroundImages.Items.Find(x => x.IsDefault && x.IsCover) == null)
            {
                string pathImage = ImageSourceManager.GetImagePath(gameBackgroundImages.CoverImage);
                if (pathImage.IsNullOrEmpty() && !File.Exists(pathImage))
                {
                    pathImage = API.Instance.Database.GetFullFilePath(gameBackgroundImages.CoverImage);
                }

                gameBackgroundImages.Items.Insert(0, new ItemImage
                {
                    Name = pathImage,
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