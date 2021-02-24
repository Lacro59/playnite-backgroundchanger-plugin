using BackgroundChanger.Services;
using BackgroundChanger.Views;
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

        public bool IsFirstLoad = true;
        public static FrameworkElement PART_ImageBackground = null;


        public BackgroundChanger(IPlayniteAPI api) : base(api, true)
        {
            PlayniteApi.Database.Games.ItemUpdated += Games_ItemUpdated;
        }


        private void Games_ItemUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            SetImage();
        }

        private void SetImage()
        {
            if (PART_ImageBackground == null)
            {
                PART_ImageBackground = IntegrationUI.SearchElementByName("ControlRoot", true, false, 2);
            }

            if (PART_ImageBackground != null)
            {
                Application.Current.Dispatcher.BeginInvoke((Action)delegate
                {
                    BackgroundChangerUI.SetBackground(PlayniteApi, PluginDatabase.GameContext, PART_ImageBackground);
                });
            }
        }


        #region Menus
        // To add new game menu items override GetGameMenuItems
        public override List<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var GameMenu = args.Games.First();

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            gameMenuItems.Add(new GameMenuItem
            {
                // Manage game background
                MenuSection = resources.GetString("LOCBc"),
                Description = resources.GetString("LOCBcManageBackground"),
                Action = (gameMenuItem) =>
                {
                    var ViewExtension = new BackgroudImagesManager(PlayniteApi, PluginDatabase.Get(GameMenu));
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCBc"), ViewExtension);
                    windowExtension.ShowDialog();
                }
            });


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
            if (args.NewValue != null && args.NewValue.Count == 1)
            {
                PluginDatabase.GameContext = args.NewValue[0];

                Task.Run(() =>
                {
                    if (IsFirstLoad)
                    {
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                        {
                            System.Threading.SpinWait.SpinUntil(() => {
                                PART_ImageBackground = IntegrationUI.SearchElementByName("ControlRoot", true, false, 2);
                                return PART_ImageBackground != null;
                            }
                            , 5000);
                        })).Wait();
                        IsFirstLoad = false;
                    }

                    SetImage();
                });
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