﻿using BackgroundChanger.Controls;
using BackgroundChanger.Services;
using BackgroundChanger.Views;
using CommonPluginsShared;
using CommonPluginsShared.PlayniteExtended;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace BackgroundChanger
{
    public class BackgroundChanger : PluginExtended<BackgroundChangerSettingsViewModel, BackgroundChangerDatabase>
    {
        public override Guid Id { get; } = Guid.Parse("3afdd02b-db6c-4b60-8faa-2971d6dfad2a");

        public static FrameworkElement PART_ImageBackground = null;


        public BackgroundChanger(IPlayniteAPI api) : base(api)
        {
            PlayniteApi.Database.Games.ItemUpdated += Games_ItemUpdated;


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
        }


        #region Custom event
        private void Games_ItemUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            //e.UpdatedItems.ForEach(x => PluginDatabase.Get(x.NewData));
        }
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
        public override List<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var GameMenu = args.Games.First();

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            if (PluginSettings.Settings.EnableBackgroundImage)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    // Manage game background
                    MenuSection = resources.GetString("LOCBc"),
                    Description = resources.GetString("LOCBcManageBackground"),
                    Action = (gameMenuItem) =>
                    {
                        var ViewExtension = new ImagesManager(PlayniteApi, PluginDatabase.Get(GameMenu), false);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCBc"), ViewExtension);
                        windowExtension.ShowDialog();
                    }
                });
            }

            if (PluginSettings.Settings.EnableCoverImage)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    // Manage game cover
                    MenuSection = resources.GetString("LOCBc"),
                    Description = resources.GetString("LOCBcManageCover"),
                    Action = (gameMenuItem) =>
                    {
                        var ViewExtension = new ImagesManager(PlayniteApi, PluginDatabase.Get(GameMenu), true);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCBc"), ViewExtension);
                        windowExtension.ShowDialog();
                    }
                });
            }

#if DEBUG
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = resources.GetString("LOCBc"),
                Description = "-"
            });
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = resources.GetString("LOCBc"),
                Description = "Test",
                Action = (mainMenuItem) =>
                {

                }
            });
#endif

            return gameMenuItems;
        }

        // To add new main menu items override GetMainMenuItems
        public override List<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return null;
        }
        #endregion


        #region Game Event
        public override void OnGameSelected(GameSelectionEventArgs args)
        {
            try
            {
                if (args.NewValue != null && args.NewValue.Count == 1)
                {
                    PluginDatabase.GameContext = args.NewValue[0];
                    PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }

        // Add code to be executed when game is finished installing.
        public override void OnGameInstalled(Game game)
        {

        }

        // Add code to be executed when game is started running.
        public override void OnGameStarted(Game game)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStarting(Game game)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStopped(Game game, long elapsedSeconds)
        {

        }

        // Add code to be executed when game is uninstalled.
        public override void OnGameUninstalled(Game game)
        {

        }
        #endregion


        #region Application event
        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted()
        {

        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped()
        {

        }
        #endregion


        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated()
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