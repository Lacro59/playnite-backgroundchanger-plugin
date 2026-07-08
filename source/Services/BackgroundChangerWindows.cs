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

        public void ShowImagesManagerWindow(Game game, bool isCover, BackgroundChanger plugin)
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

            ImagesManager viewExtension = new ImagesManager(gameData, isCover, plugin);
            string title = string.Format(
                "{0} - {1}",
                ResourceProvider.GetString("LOCBc"),
                ResourceProvider.GetString(isCover ? "LOCGameCoverImageTitle" : "LOCGameBackgroundTitle"));

            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(title, viewExtension);
            _ = windowExtension.ShowDialog();
        }
    }
}
