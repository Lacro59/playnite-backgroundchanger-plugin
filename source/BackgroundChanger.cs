using BackgroundChanger.Controls;
using BackgroundChanger.Models;
using BackgroundChanger.Services;
using BackgroundChanger.Views;
using CommonPluginsShared;
using CommonPluginsShared.PlayniteExtended;
using CommonPluginsShared.Plugins;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanger
{
    public class BackgroundChanger : PluginExtended<BackgroundChangerSettingsViewModel, BackgroundChangerDatabase>
    {
        public override Guid Id => Guid.Parse("3afdd02b-db6c-4b60-8faa-2971d6dfad2a");

        public static FrameworkElement PART_ImageBackground = null;
        private readonly BackgroundChangerMenus menus;
        private readonly MediaConversionStartupService _mediaConversionStartupService = new MediaConversionStartupService();

        public BackgroundChanger(IPlayniteAPI api) : base(api, nameof(BackgroundChanger))
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
                SettingsRoot = $"{nameof(PluginSettingsViewModel)}.{nameof(BackgroundChangerSettingsViewModel.Settings)}"
            });

            var iconResourcesToAdd = new Dictionary<string, string>
            {
                { "openFolderIcon", "\xEC5B" }
            };
            Common.AddTextIcoFontResource(iconResourcesToAdd);

            BackgroundChangerWindows pluginWindows = new BackgroundChangerWindows(nameof(BackgroundChanger), PluginDatabase);
            PluginDatabase.PluginWindows = pluginWindows;
            menus = new BackgroundChangerMenus(PluginSettingsViewModel.Settings, PluginDatabase, pluginWindows, this);
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
            return menus.GetGameMenuItems(args);
        }

        // To add new main menu items override GetMainMenuItems
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return menus.GetMainMenuItems(args);
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

        private const int StartupBatchDatabaseWaitMs = 120000;
        private const int StartupBatchDatabasePollMs = 200;

        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            Task.Run(() =>
            {
                try
                {
                    int elapsed = 0;
                    while (!PluginDatabase.IsDatabaseReady() && elapsed < StartupBatchDatabaseWaitMs)
                    {
                        Thread.Sleep(StartupBatchDatabasePollMs);
                        elapsed += StartupBatchDatabasePollMs;
                    }

                    if (!PluginDatabase.IsDatabaseReady())
                    {
                        Common.LogDebug(
                            false,
                            "[MediaConversionStartup] Database not ready after timeout, startup batch skipped.");
                        return;
                    }

                    _mediaConversionStartupService.RunStartupBatch(PluginDatabase, Id);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            });
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
            return PluginSettingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new BackgroundChangerSettingsView();
        }

        #endregion
    }
}