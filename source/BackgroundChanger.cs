﻿using BackgroundChanger.Controls;
using BackgroundChanger.Models;
using BackgroundChanger.Services;
using BackgroundChanger.Views;
using CommonPlayniteShared.Commands;
using CommonPluginsShared;
using CommonPluginsShared.PlayniteExtended;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanger
{
    public class BackgroundChanger : PluginExtended<BackgroundChangerSettingsViewModel, BackgroundChangerDatabase>
    {
        public override Guid Id => Guid.Parse("3afdd02b-db6c-4b60-8faa-2971d6dfad2a");

        public static FrameworkElement PART_ImageBackground = null;

        public BackgroundChanger(IPlayniteAPI api) : base(api)
        {
            // Custom elements integration
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string> { "PluginBackgroundImage", "PluginCoverImage" },
                SourceName = "BackgroundChanger"
            });

            // Settings integration
            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = "BackgroundChanger",
                SettingsRoot = $"{nameof(PluginSettings)}.{nameof(PluginSettings.Settings)}"
            });

            var iconResourcesToAdd = new Dictionary<string, string>
            {
                { "openFolderIcon", "\xEC5B" }
            };
            Common.AddTextIcoFontResource(iconResourcesToAdd);
        }

        #region Custom event

        #endregion

        #region Theme integration

        // List custom controls
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Name == "PluginBackgroundImage")
            {
                return new PluginBackgroundImage();
            }

            if (args.Name == "PluginCoverImage")
            {
                return new PluginCoverImage();
            }

            return null;
        }

        #endregion

        #region Menus

        // To add new game menu items override GetGameMenuItems
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Game gameMenu = args.Games.First();
            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            GameBackgroundImages data = PluginDatabase.Get(gameMenu, true);

            if (PluginSettings.Settings.EnableBackgroundImage)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    // Manage game background
                    MenuSection = ResourceProvider.GetString("LOCBc"),
                    Description = ResourceProvider.GetString("LOCBcManageBackground"),
                    Action = (gameMenuItem) =>
                    {
                        ImagesManager viewExtension = new ImagesManager(PluginDatabase.Get(gameMenu), false, this);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCBc") + " - " + ResourceProvider.GetString("LOCGameBackgroundTitle"), viewExtension);
                        _ = windowExtension.ShowDialog();
                    }
                });
            }

            if (PluginSettings.Settings.EnableCoverImage)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    // Manage game cover
                    MenuSection = ResourceProvider.GetString("LOCBc"),
                    Description = ResourceProvider.GetString("LOCBcManageCover"),
                    Action = (gameMenuItem) =>
                    {
                        ImagesManager viewExtension = new ImagesManager(PluginDatabase.Get(gameMenu), true, this);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCBc") + " - " + ResourceProvider.GetString("LOCGameCoverImageTitle"), viewExtension);
                        _ = windowExtension.ShowDialog();
                    }
                });
            }

            if (data.HasDataBackground || data.HasDataCover)
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
                        string path = Path.Combine(PluginDatabase.Paths.PluginUserDataPath, "Images", gameMenu.Id.ToString());
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

        // To add new main menu items override GetMainMenuItems
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return null;
        }

        #endregion

        #region Game Event

        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {
            try
            {
                if (args?.NewValue != null && args.NewValue.Count == 1)
                {
                    PluginDatabase.GameContext = args.NewValue[0];
                    PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        // Add code to be executed when game is finished installing.
        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
        }

        // Add code to be executed when game is uninstalled.
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
        }

        // Add code to be executed when game is started running.
        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
        }

        #endregion

        #region Application event

        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
        }

        #endregion

        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {

        }

        #region Settings

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return PluginSettings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new BackgroundChangerSettingsView();
        }

        #endregion
    }
}