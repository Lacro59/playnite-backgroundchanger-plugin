using BackgroundChanger.Models;
using BackgroundChanger.Views;
using CommonPluginsShared;
using CommonPluginsShared.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using System.Windows;

namespace BackgroundChanger.Services
{
    public class BackgroundChangerWindows : PluginWindows
    {
        public BackgroundChangerWindows(string pluginName, BackgroundChangerDatabase pluginDatabase) : base(pluginName, pluginDatabase)
        {
        }

        public void ShowImagesManagerWindow(Game game, BackgroundChangerDatabase.PluginMediaKind mediaKind, BackgroundChanger plugin)
        {
            if (game == null)
            {
                return;
            }

            BackgroundChangerDatabase database = (BackgroundChangerDatabase)PluginDatabase;
            GameBackgroundImages gameData = database.Get(game);
            if (gameData == null)
            {
                return;
            }

            ImagesManager viewExtension = new ImagesManager(gameData, mediaKind, plugin);
            string mediaTitleKey;
            switch (mediaKind)
            {
                case BackgroundChangerDatabase.PluginMediaKind.Cover:
                    mediaTitleKey = "LOCGameCoverImageTitle";
                    break;
                case BackgroundChangerDatabase.PluginMediaKind.Icon:
                    mediaTitleKey = "LOCGameIconTitle";
                    break;
                default:
                    mediaTitleKey = "LOCGameBackgroundTitle";
                    break;
            }

            string title = string.Format(
                "{0} - {1}",
                ResourceProvider.GetString("LOCBc"),
                ResourceProvider.GetString(mediaTitleKey));

            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(title, viewExtension);
            _ = windowExtension.ShowDialog();
        }

        /// <summary>
        /// Opens the images manager for background or cover (legacy bool discriminant).
        /// </summary>
        public void ShowImagesManagerWindow(Game game, bool isCover, BackgroundChanger plugin)
        {
            ShowImagesManagerWindow(
                game,
                isCover ? BackgroundChangerDatabase.PluginMediaKind.Cover : BackgroundChangerDatabase.PluginMediaKind.Background,
                plugin);
        }
    }
}
