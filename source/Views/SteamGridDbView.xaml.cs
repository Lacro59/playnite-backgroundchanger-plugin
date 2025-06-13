using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPluginsControls.PlayniteControls;
using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Logique d'interaction pour SteamGridDbView.xaml
    /// </summary>
    public partial class SteamGridDbView : UserControl
    {
        private BackgroundChanger Plugin { get; }
        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        private SteamGridDbApi SteamGridDbApi { get; set; } = new SteamGridDbApi();
        private SteamGridDbType SteamGridDbType { get; set; }

        public List<SteamGridDbResult> SteamGridDbResults { get; set; }

        private SteamGridDbResultData DataSearch { get; set; } = null;
        private List<SteamGridDbResult> DataSearchFiltered { get; set; } = null;


        public SteamGridDbView(string name, SteamGridDbType steamGridDbType, BackgroundChanger plugin)
        {
            InitializeComponent();

            Plugin = plugin;
            SteamGridDbType = steamGridDbType;

            SearchElement.Text = name;
            SearchData(name);


            if (SteamGridDbType == SteamGridDbType.heroes)
            {
                PART_ComboDimensions.ItemsSource = PluginDatabase.PluginSettings.Settings.SgHeroesFilters.CheckDimensions;
                PART_ComboStyles.ItemsSource = PluginDatabase.PluginSettings.Settings.SgHeroesFilters.CheckStyles;
                PART_ComboTypes.ItemsSource = PluginDatabase.PluginSettings.Settings.SgHeroesFilters.CheckTypes;
                PART_ComboTags.ItemsSource = PluginDatabase.PluginSettings.Settings.SgHeroesFilters.CheckTags;

                PART_ButtonSortByDate_Asc.IsChecked = PluginDatabase.PluginSettings.Settings.SgHeroesFilters.SortByDateAsc;
                PART_ButtonSortByDate_Desc.IsChecked = !PluginDatabase.PluginSettings.Settings.SgHeroesFilters.SortByDateAsc;
            }
            else
            {
                PART_ComboDimensions.ItemsSource = PluginDatabase.PluginSettings.Settings.SgGridsFilters.CheckDimensions;
                PART_ComboStyles.ItemsSource = PluginDatabase.PluginSettings.Settings.SgGridsFilters.CheckStyles;
                PART_ComboTypes.ItemsSource = PluginDatabase.PluginSettings.Settings.SgGridsFilters.CheckTypes;
                PART_ComboTags.ItemsSource = PluginDatabase.PluginSettings.Settings.SgGridsFilters.CheckTags;

                PART_ButtonSortByDate_Asc.IsChecked = PluginDatabase.PluginSettings.Settings.SgGridsFilters.SortByDateAsc;
                PART_ButtonSortByDate_Desc.IsChecked = !PluginDatabase.PluginSettings.Settings.SgGridsFilters.SortByDateAsc;
            }

            Combox_Changed();
        }

        private void Combox_Changed()
        {
            ComboBox_SelectionChanged(PART_ComboDimensions, null);
            ComboBox_SelectionChanged(PART_ComboStyles, null);
            ComboBox_SelectionChanged(PART_ComboTags, null);
            ComboBox_SelectionChanged(PART_ComboTypes, null);
        }


        private void SearchData(string name)
        {
            PART_SearchList.ItemsSource = null;
            PART_ElementList.ItemsSource = null;

            ButtonSelect.IsEnabled = false;

            SteamGridDbSearchResultData dataSearch = null;
            if (API.Instance.Dialogs.ActivateGlobalProgress((_) =>
            {
                try
                {
                    dataSearch = SteamGridDbApi.SearchGame(name);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "BackgroundChanger");
                }
            }, new GlobalProgressOptions("LOCDownloadingLabel")).Result == true)
            {
                if (dataSearch != null)
                {
                    PART_SearchList.ItemsSource = dataSearch.Data;
                }
            }
        }

        private void SearchDataElements(int id)
        {
            PART_ElementList.ItemsSource = null;
            ButtonSelect.IsEnabled = false;

            DataSearch = null;
            if (API.Instance.Dialogs.ActivateGlobalProgress((_) =>
            {
                try
                {
                    SteamGridDbResultData steamGridDbResultData = null;
                    steamGridDbResultData = SteamGridDbApi.SearchElement(id, SteamGridDbType);
                    DataSearch = new SteamGridDbResultData
                    {
                        Data = new List<SteamGridDbResult>()
                    };

                    int page = 0;
                    while (steamGridDbResultData?.Data?.Count > 0)
                    {
                        DataSearch.Data.AddRange(steamGridDbResultData.Data);
                        page++;
                        steamGridDbResultData = SteamGridDbApi.SearchElement(id, SteamGridDbType, page);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "BackgroundChanger");
                }
            }, new GlobalProgressOptions("LOCDownloadingLabel")).Result == true)
            {
                ButtonSelectAll.IsEnabled = DataSearch?.Data?.Count > 0;
                if (DataSearch != null)
                {
                    ApplyFilter(null, null);
                }
            }
        }


        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }

        private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
        {
            PART_ElementList.SelectAll();
            ((Window)Parent).Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }


        private void PART_BtSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchData(SearchElement.Text);
        }


        private void SearchElement_KeyUp(object sender, KeyEventArgs e)
        {
            if (((SearchBox)sender).Text.Length > 1)
            {
                PART_BtSearch.IsEnabled = true;

                if (e.Key == Key.Enter)
                {
                    PART_BtSearch_Click(null, null);
                }
            }
            else
            {
                PART_BtSearch.IsEnabled = false;
            }
        }


        private void PART_SearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (PART_SearchList?.Items?.Count > 0)
                {
                    int id = ((SteamGridDbSearchResult)PART_SearchList.SelectedItem).Id;
                    SearchDataElements(id);
                }
            }
            catch { }
        }


        private void PART_ElementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                SteamGridDbResults = PART_ElementList.SelectedItems.Cast<SteamGridDbResult>().ToList();
                if (SteamGridDbResults != null)
                {
                    ButtonSelect.IsEnabled = true;
                }
            }
            catch
            {
                ButtonSelect.IsEnabled = false;
            }
        }


        private void ApplyFilter(object sender, RoutedEventArgs e)
        {
            PART_ComboDimensions.Text = string.Empty;
            PART_ComboStyles.Text = string.Empty;
            PART_ComboTypes.Text = string.Empty;


            DataSearchFiltered = null;
            PART_TotalFound.Content = "0";

            if (DataSearch != null)
            {
                DataSearchFiltered = Serialization.GetClone(DataSearch.Data);


                List<CheckData> ListDimensions = PART_ComboDimensions.ItemsSource as List<CheckData>;
                DataSearchFiltered = DataSearchFiltered.Where(x => ListDimensions.Any(y => (x.Width + "x" + x.Height) == y.Data && y.IsChecked)).ToList();

                List<CheckData> ListStyles = PART_ComboStyles.ItemsSource as List<CheckData>;
                DataSearchFiltered = DataSearchFiltered.Where(x => ListStyles.Any(y => x.Style == y.Data && y.IsChecked)).ToList();

                List<CheckData> ListTags = PART_ComboTags.ItemsSource as List<CheckData>;
                bool humor = ListTags.Find(x => x.Name == "Humor").IsChecked;
                bool nsfw = ListTags.Find(x => x.Name == "Adult Content").IsChecked;
                bool epilepsy = ListTags.Find(x => x.Name == "Epilepsy").IsChecked;
                bool untagged = ListTags.Find(x => x.Name == "Untagged").IsChecked;
                DataSearchFiltered = DataSearchFiltered.Where(x => (humor && x.Humor) || (nsfw && x.Nsfw) || (epilepsy && x.Epilepsy) || (untagged && x.Untagged)).ToList();

                List<CheckData> ListTypes = PART_ComboTypes.ItemsSource as List<CheckData>;
                if (ListTypes[0].IsChecked && !ListTypes[1].IsChecked)
                {
                    DataSearchFiltered = DataSearchFiltered.Where(x => ListTypes.Any(y => x.Mime != "image/webp" && y.IsChecked)).ToList();
                }
                else if (!ListTypes[0].IsChecked && ListTypes[1].IsChecked)
                {
                    DataSearchFiltered = DataSearchFiltered.Where(x => ListTypes.Any(y => x.Mime == "image/webp" && y.IsChecked)).ToList();
                }


                PART_ButtonSort_Click(null, null);
            }

            PART_ElementList.ItemsSource = null;
            PART_ElementList.ItemsSource = DataSearchFiltered;

            if (DataSearchFiltered != null)
            {
                PART_TotalFound.Content = DataSearchFiltered.Count;
            }


            ComboBox_SelectionChanged(PART_ComboDimensions, null);
            ComboBox_SelectionChanged(PART_ComboStyles, null);
            ComboBox_SelectionChanged(PART_ComboTags, null);
            ComboBox_SelectionChanged(PART_ComboTypes, null);
        }

        private void PART_ButtonSort_Click(object sender, RoutedEventArgs e)
        {
            // Exit if there is no data to sort
            if (DataSearchFiltered == null)
            {
                return;
            }

            // Handle toggle button exclusivity (only one can be checked at a time)
            if (sender is ToggleButton btn)
            {
                // List all sort buttons
                var sortButtons = new[]
                {
                    PART_ButtonSortByDate_Asc,
                    PART_ButtonSortByDate_Desc,
                    //PART_ButtonSortByScore_Asc,
                    //PART_ButtonSortByScore_Desc
                };

                // Uncheck all other buttons if the current one is checked
                if (btn.IsChecked == true)
                {
                    foreach (var b in sortButtons)
                    {
                        if (b != btn)
                        {
                            b.IsChecked = false;
                        }
                    }
                }
            }

            // Sort the filtered data according to the selected sort button
            if (PART_ButtonSortByDate_Asc.IsChecked == true)
            {
                DataSearchFiltered.Sort((x, y) => x.Id.CompareTo(y.Id));
            }
            else if (PART_ButtonSortByDate_Desc.IsChecked == true)
            {
                DataSearchFiltered.Sort((x, y) => y.Id.CompareTo(x.Id));
            }
            //else if (PART_ButtonSortByScore_Asc.IsChecked == true)
            //{
            //    DataSearchFiltered.Sort((x, y) => x.Score.CompareTo(y.Score));
            //}
            //else if (PART_ButtonSortByScore_Desc.IsChecked == true)
            //{
            //    DataSearchFiltered.Sort((x, y) => y.Score.CompareTo(x.Score));
            //}

            // Refresh the item source to update the UI
            PART_ElementList.ItemsSource = null;
            PART_ElementList.ItemsSource = DataSearchFiltered;
        }


        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string data = ((ComboBox)sender).Name == "PART_ComboDimensions"
                ? string.Join(", ", ((List<CheckData>)((ComboBox)sender).ItemsSource).Where(x => x.IsChecked).Select(x => x.Name.Split('-')[2].Trim()))
                : string.Join(", ", ((List<CheckData>)((ComboBox)sender).ItemsSource).Where(x => x.IsChecked).Select(x => x.Name));

            ((ComboBox)sender).Text = data;
        }


        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            if (SteamGridDbType == SteamGridDbType.heroes)
            {
                PART_ComboDimensions.ItemsSource = new List<CheckData>
                {
                    new CheckData { Name = "Steam - 96:31 - 1920x620", Data = "1920x620" },
                    new CheckData { Name = "Steam - 96:31 - 3840x1240", Data = "3840x1240" },
                    new CheckData { Name = "Galaxy 2.0 - 32:13 - 1600x650", Data = "1600x650" }
                };

                PART_ComboStyles.ItemsSource = new List<CheckData>
                {
                    new CheckData { Name = "Alternate", Data = "alternate" },
                    new CheckData { Name = "Material", Data = "material" },
                    new CheckData { Name = "Blurred", Data = "blurred" }
                };

                PART_ComboTypes.ItemsSource = new List<CheckData>
                {
                    new CheckData { Name = "Static", Data = "static" },
                    new CheckData { Name = "Animated", Data = "animated" }
                };

                PART_ComboTags.ItemsSource = new List<CheckData>
                {
                    new CheckData { Name = "Humor", Data = "Humor" },
                    new CheckData { Name = "Adult Content", Data = "Adult Content", IsChecked = false },
                    new CheckData { Name = "Epilepsy", Data = "Epilepsy" },
                    new CheckData { Name = "Untagged", Data = "Untagged" }
                };
            }
            else
            {
                PART_ComboDimensions.ItemsSource = new List<CheckData>
                {
                    new CheckData { Name = "Steam Vertical - 2:3 - 600x900", Data = "600x900" },
                    new CheckData { Name = "Steam Horizontal - 92:43 - 920x430", Data = "920x430" },
                    new CheckData { Name = "Steam Horizontal - 92:43 - 460x215", Data = "460x215" },
                    new CheckData { Name = "Square - 1:1 - 1024x1024", Data = "1024x1024" },
                    new CheckData { Name = "Square - 1:1 - 512x512", Data = "512x512" },
                    new CheckData { Name = "Galaxy 2.0 - 22:31 - 660x930", Data = "660x930" },
                    new CheckData { Name = "Galaxy 2.0 - 22:31 - 342x482", Data = "342x482" }
                };

                PART_ComboStyles.ItemsSource = new List<CheckData>
                {
                    new CheckData { Name = "Alternate", Data = "alternate" },
                    new CheckData { Name = "White Logo", Data = "white_logo" },
                    new CheckData { Name = "Material", Data = "material" },
                    new CheckData { Name = "Blurred", Data = "blurred" },
                    new CheckData { Name = "No Logo", Data = "no_logo" }
                };

                PART_ComboTypes.ItemsSource = new List<CheckData>
                {
                    new CheckData { Name = "Static", Data = "static" },
                    new CheckData { Name = "Animated", Data = "animated" }
                };

                PART_ComboTags.ItemsSource = new List<CheckData>
                {
                    new CheckData { Name = "Humor", Data = "Humor" },
                    new CheckData { Name = "Adult Content", Data = "Adult Content", IsChecked = false },
                    new CheckData { Name = "Epilepsy", Data = "Epilepsy" },
                    new CheckData { Name = "Untagged", Data = "Untagged" }
                };
            }

            Combox_Changed();
        }

        private void SavedFilter_Click(object sender, RoutedEventArgs e)
        {
            if (SteamGridDbType == SteamGridDbType.heroes)
            {
                PluginDatabase.PluginSettings.Settings.SgHeroesFilters.CheckDimensions = (List<CheckData>)PART_ComboDimensions.ItemsSource;
                PluginDatabase.PluginSettings.Settings.SgHeroesFilters.CheckStyles = (List<CheckData>)PART_ComboStyles.ItemsSource;
                PluginDatabase.PluginSettings.Settings.SgHeroesFilters.CheckTypes = (List<CheckData>)PART_ComboTypes.ItemsSource;
                PluginDatabase.PluginSettings.Settings.SgHeroesFilters.CheckTags = (List<CheckData>)PART_ComboTags.ItemsSource;

                PluginDatabase.PluginSettings.Settings.SgHeroesFilters.SortByDateAsc = (bool)PART_ButtonSortByDate_Asc.IsChecked;
            }
            else
            {
                PluginDatabase.PluginSettings.Settings.SgGridsFilters.CheckDimensions = (List<CheckData>)PART_ComboDimensions.ItemsSource;
                PluginDatabase.PluginSettings.Settings.SgGridsFilters.CheckStyles = (List<CheckData>)PART_ComboStyles.ItemsSource;
                PluginDatabase.PluginSettings.Settings.SgGridsFilters.CheckTypes = (List<CheckData>)PART_ComboTypes.ItemsSource;
                PluginDatabase.PluginSettings.Settings.SgGridsFilters.CheckTags = (List<CheckData>)PART_ComboTags.ItemsSource;

                PluginDatabase.PluginSettings.Settings.SgGridsFilters.SortByDateAsc = (bool)PART_ButtonSortByDate_Asc.IsChecked;
            }

            Plugin.SavePluginSettings(PluginDatabase.PluginSettings.Settings);
            Combox_Changed();
        }
    }
}