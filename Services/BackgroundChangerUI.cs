using BackgroundChanger.Models;
using BackgroundChanger2.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace BackgroundChanger.Services
{
    public class BackgroundChangerUI
    {
        private static BackgroundChangerDatabase PluginDatabase = BackgroundChanger.PluginDatabase;
        private static Timer BcTimer { get; set; }
        private static int Counter = 0;

        public static void SetBackground(IPlayniteAPI PlayniteApi, Game game, dynamic PART_ImageBackground)
        {
            GameBackgroundImages gameBackgroundImages = PluginDatabase.Get(game);
            string PathImage = string.Empty;

            if (BcTimer != null)
            {
                Counter = 0;
                BcTimer.Stop();
                BcTimer = null;
            }

            if (PART_ImageBackground != null)
            {
                if (gameBackgroundImages.HasData)
                {
                    if (PluginDatabase.PluginSettings.EnableAutoChanger)
                    {
                        PluginDatabase.SetCurrent(game);

                        if (PluginDatabase.PluginSettings.EnableRandomSelect)
                        {
                            Random rnd = new Random();
                            int ImgSelected = rnd.Next(0, (gameBackgroundImages.Items.Count));
                            PathImage = gameBackgroundImages.Items[ImgSelected].FullPath;
                        }
                        else
                        {
                            PathImage = PluginDatabase.GameSelectedData.Items[Counter].FullPath;
                        }

                        SetBackgroundImage(BackgroundChanger.PART_ImageBackground, PathImage);

                        BcTimer = new Timer(PluginDatabase.PluginSettings.AutoChangerTimer * 1000);
                        BcTimer.AutoReset = true;
                        BcTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                        BcTimer.Start();
                    }
                    else if (PluginDatabase.PluginSettings.EnableRandomSelect)
                    {
                        Random rnd = new Random();
                        int ImgSelected = rnd.Next(0, (gameBackgroundImages.Items.Count));
                        PathImage = gameBackgroundImages.Items[ImgSelected].FullPath;

                        SetBackgroundImage(PART_ImageBackground, PathImage);
                    }
                    else
                    {
                        SetDefaultBackgroundImage(PlayniteApi, PART_ImageBackground, game);
                    }
                }
                else
                {
                    SetDefaultBackgroundImage(PlayniteApi, PART_ImageBackground, game);
                }
            }
        }


        public static void SetDefaultBackgroundImage(IPlayniteAPI PlayniteApi, dynamic PART_ImageBackground, Game game)
        {
            if (game.BackgroundImage.IsNullOrEmpty())
            {
                SetBackgroundImage(PART_ImageBackground);
            }
            else
            {
                string PathImage = PlayniteApi.Database.GetFullFilePath(game.BackgroundImage);
                SetBackgroundImage(PART_ImageBackground, PathImage);
            }
        }


        public static void SetBackgroundImage(dynamic PART_ImageBackground, string PathImage = null)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)delegate
            {
                if (!File.Exists(PathImage))
                {
                    PathImage = null;
                }
                ((dynamic)PART_ImageBackground).Source = PathImage;
            });
        }


        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            if (PluginDatabase.PluginSettings.EnableRandomSelect)
            {
                Random rnd = new Random();
                int ImgSelected = rnd.Next(0, (PluginDatabase.GameSelectedData.Items.Count));
                while (ImgSelected == Counter)
                {
                    ImgSelected = rnd.Next(0, (PluginDatabase.GameSelectedData.Items.Count));
                }
                Counter = ImgSelected;

                string PathImage = PluginDatabase.GameSelectedData.Items[ImgSelected].FullPath;

                SetBackgroundImage(BackgroundChanger.PART_ImageBackground, PathImage);
            }
            else
            {
                Counter++;

                if (Counter == PluginDatabase.GameSelectedData.Items.Count)
                {
                    Counter = 0;
                }

                string PathImage = PluginDatabase.GameSelectedData.Items[Counter].FullPath;

                SetBackgroundImage(BackgroundChanger.PART_ImageBackground, PathImage);
            }
        }
    }
}
