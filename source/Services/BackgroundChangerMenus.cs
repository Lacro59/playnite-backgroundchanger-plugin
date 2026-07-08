using BackgroundChanger.Models;
using CommonPlayniteShared.Commands;
using CommonPluginsShared.Collections;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Collections.Generic;
using System.IO;

namespace BackgroundChanger.Services
{
    public class BackgroundChangerMenus : PluginMenus
    {
        private readonly BackgroundChangerSettings _settings;
        private readonly BackgroundChangerDatabase _database;
        private readonly BackgroundChangerWindows _windows;
        private readonly BackgroundChanger _plugin;

        public BackgroundChangerMenus(
            BackgroundChangerSettings settings,
            BackgroundChangerDatabase database,
            BackgroundChangerWindows windows,
            BackgroundChanger plugin)
            : base(settings, database)
        {
            _settings = settings;
            _database = database;
            _windows = windows;
            _plugin = plugin;
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();
            if (args?.Games == null || args.Games.Count == 0)
            {
                return gameMenuItems;
            }

            Game gameMenu = args.Games[0];
            GameBackgroundImages data = _database.Get(gameMenu, true);

            if (_settings.EnableBackgroundImage)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    MenuSection = ResourceProvider.GetString("LOCBc"),
                    Description = ResourceProvider.GetString("LOCBcManageBackground"),
                    Action = (gameMenuItem) => _windows.ShowImagesManagerWindow(gameMenu, false, _plugin)
                });
            }

            if (_settings.EnableCoverImage)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    MenuSection = ResourceProvider.GetString("LOCBc"),
                    Description = ResourceProvider.GetString("LOCBcManageCover"),
                    Action = (gameMenuItem) => _windows.ShowImagesManagerWindow(gameMenu, true, _plugin)
                });
            }

            if (data != null && (data.HasDataBackground || data.HasDataCover))
            {
                if (gameMenuItems.Count > 0)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = ResourceProvider.GetString("LOCBc"),
                        Description = "-"
                    });
                }

                gameMenuItems.Add(new GameMenuItem
                {
                    MenuSection = ResourceProvider.GetString("LOCBc"),
                    Icon = "openFolderIcon",
                    Description = ResourceProvider.GetString("LOCOpenMetadataFolder"),
                    Action = (gameMenuItem) =>
                    {
                        string path = Path.Combine(_database.Paths.PluginUserDataPath, "Images", gameMenu.Id.ToString());
                        GlobalCommands.NavigateDirectoryCommand.Execute(path);
                    }
                });
            }

#if DEBUG
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = ResourceProvider.GetString("LOCBc"),
                Description = "-"
            });
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = ResourceProvider.GetString("LOCBc"),
                Description = "Test",
                Action = (mainMenuItem) =>
                {
                }
            });
#endif

            return gameMenuItems;
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return null;
        }
    }
}
