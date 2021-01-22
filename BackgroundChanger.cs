using BackgroundChanger.Services;
using BackgroundChanger.Views;
using BackgroundChanger2.Controls;
using CommonPluginsShared;
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
    public class BackgroundChanger : Plugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private BackgroundChangerSettings settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("3afdd02b-db6c-4b60-8faa-2971d6dfad2a");

        public bool IsFirstLoad = true;

        public static BackgroundChangerDatabase PluginDatabase;
        public static string pluginFolder;
        public static FrameworkElement PART_ImageBackground = null;
        public static FrameworkElement PART_ImageCover= null;


        public BackgroundChanger(IPlayniteAPI api) : base(api)
        {
            settings = new BackgroundChangerSettings(this);

            // Loading plugin database 
            PluginDatabase = new BackgroundChangerDatabase(PlayniteApi, settings, this.GetPluginUserDataPath());
            PluginDatabase.InitializeDatabase();

            // Get plugin's location 
            pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Add plugin localization in application ressource.
            PluginLocalization.SetPluginLanguage(pluginFolder, api.ApplicationSettings.Language);
            // Add common in application ressource.
            Common.Load(pluginFolder);
            Common.SetEvent(PlayniteApi);

            // Check version
            if (settings.EnableCheckVersion)
            {
                CheckVersion cv = new CheckVersion();
                cv.Check("BackgroundChanger", pluginFolder, api);
            }
        }


        // To add new game menu items override GetGameMenuItems
        public override List<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var GameMenu = args.Games.First();

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            gameMenuItems.Add(new GameMenuItem
            {
                // Delete & download localizations data for the selected game
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


        public override void OnGameSelected(GameSelectionEventArgs args)
        {
            if (args.NewValue != null && args.NewValue.Count == 1)
            {
                BackgroundChangerDatabase.GameSelected = args.NewValue[0];
#if DEBUG
                logger.Debug($"BackgroundChanger [Ignored] - OnGameSelected() - {BackgroundChangerDatabase.GameSelected.Name} - {BackgroundChangerDatabase.GameSelected.Id.ToString()}");
#endif

                Task.Run(() =>
                {
                    if (IsFirstLoad)
                    {
#if DEBUG
                        logger.Debug($"BackgroundChanger - IsFirstLoad");
#endif
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                        {
                            System.Threading.SpinWait.SpinUntil(() => {
                                PART_ImageBackground = IntegrationUI.SearchElementByName("ControlRoot", true, false, 2);
                                if (settings.EnableImageAnimatedCover)
                                {
                                    PART_ImageCover = IntegrationUI.SearchElementByName("PART_ImageCover", true, false);
                                }
                                return PART_ImageBackground != null;
                            }
                            , 5000);
                        })).Wait();
                        IsFirstLoad = false;

                        if (settings.EnableImageAnimatedBackground)
                        {
                            try
                            {
                                var GridParent = PART_ImageBackground.Parent;
                                if (GridParent is Grid)
                                {
                                    Application.Current.Dispatcher.BeginInvoke((Action)delegate
                                    {
                                        int index = ((Grid)GridParent).Children.IndexOf(PART_ImageBackground);
                                        ((Grid)GridParent).Children.RemoveAt(index);

                                        PART_ImageBackground = new ImageAnimated();
                                        ((ImageAnimated)PART_ImageBackground).ImgWidth = 600;
                                        PART_ImageBackground.VerticalAlignment = VerticalAlignment.Top;
                                        PART_ImageBackground.HorizontalAlignment = HorizontalAlignment.Center;
                                        ((ImageAnimated)PART_ImageBackground).UseOpacityMask = true;
                                        RenderOptions.SetBitmapScalingMode(PART_ImageBackground, BitmapScalingMode.Fant);

                                        ((Grid)GridParent).Children.Insert(index, PART_ImageBackground);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, "BackgroundChanger");
                            }
                        }

                        if (settings.EnableImageAnimatedCover)
                        {
                            try
                            {                               
                                if (PART_ImageCover != null)
                                {
                                    double CoverHeight = PART_ImageCover.ActualHeight;

                                    var DockPanelParent = PART_ImageCover.Parent;
                                    if (DockPanelParent is DockPanel)
                                    {
                                        Application.Current.Dispatcher.BeginInvoke((Action)delegate
                                        {
                                            int index = ((DockPanel)DockPanelParent).Children.IndexOf(PART_ImageCover);
                                            ((DockPanel)DockPanelParent).Children.RemoveAt(index);

                                            PART_ImageCover = new ImageAnimated();
                                            ((ImageAnimated)PART_ImageCover).Stretch = Stretch.Uniform;
                                            ((ImageAnimated)PART_ImageCover).StretchDirection = StretchDirection.Both;
                                            PART_ImageCover.VerticalAlignment = VerticalAlignment.Bottom;
                                            DockPanel.SetDock(PART_ImageCover, Dock.Right);
                                            RenderOptions.SetBitmapScalingMode(PART_ImageCover, BitmapScalingMode.Fant);
                                            PART_ImageCover.Height = CoverHeight;
                                            ((DockPanel)DockPanelParent).Children.Insert(index, PART_ImageCover);

                                            //DockPanel.SetDock(((DockPanel)DockPanelParent).Children[1], Dock.Left);
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, "BackgroundChanger");
                            }
                        }
                    }

                    
                    if (PART_ImageBackground == null)
                    {
                        PART_ImageBackground = IntegrationUI.SearchElementByName("ControlRoot", true, false, 2);
                    }

                    if (PART_ImageBackground != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke((Action)delegate
                        {
                            BackgroundChangerUI.SetBackground(PlayniteApi, BackgroundChangerDatabase.GameSelected, PART_ImageBackground);
                        });
                            
                    }

                    if (PART_ImageCover != null)
                    {
                        if (PART_ImageCover is ImageAnimated)
                        {
                            Application.Current.Dispatcher.BeginInvoke((Action)delegate
                            {
                                ((ImageAnimated)PART_ImageCover).Source = PlayniteApi.Database.GetFullFilePath(BackgroundChangerDatabase.GameSelected.CoverImage);
                            });
                        }
                    }
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


        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted()
        {

        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped()
        {

        }


        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated()
        {

        }


        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new BackgroundChangerSettingsView();
        }
    }
}