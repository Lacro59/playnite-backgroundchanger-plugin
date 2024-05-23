using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPluginsControls.PlayniteControls;
using CommonPluginsShared;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Logique d'interaction pour SteamGridDbView.xaml
    /// </summary>
    public partial class SteamGridDbView : UserControl
    {
        private SteamGridDbApi SteamGridDbApi { get; set; } = new SteamGridDbApi();
        private SteamGridDbType SteamGridDbType { get; set; }

        public List<SteamGridDbResult> SteamGridDbResults { get; set; }

        private SteamGridDbResultData DataSearch { get; set; } = null;
        private List<SteamGridDbResult> DataSearchFiltered { get; set; } = null;


        public SteamGridDbView(string Name, SteamGridDbType steamGridDbType)
        {
            InitializeComponent();

            this.SteamGridDbType = steamGridDbType;

            SearchElement.Text = Name;
            SearchData(Name);


            if (steamGridDbType == SteamGridDbType.heroes)
            {
                // Set Dimensions
                List<CheckData> CheckDimensions = new List<CheckData>
                {
                    new CheckData { Name="Steam - 96:31 - 1920x620", Data="1920x620" },
                    new CheckData { Name="Steam - 96:31 - 3840x1240", Data="3840x1240" },

                    new CheckData { Name="Galaxy 2.0 - 32:13 - 1600x650", Data="1600x650" }
                };
                PART_ComboDimensions.ItemsSource = CheckDimensions;


                // Set Styles
                List<CheckData> CheckStyles = new List<CheckData>
                {
                    new CheckData { Name="Alternate", Data="alternate" },
                    new CheckData { Name="Material", Data="material" },
                    new CheckData { Name="Blurred", Data="blurred" }
                };
                PART_ComboStyles.ItemsSource = CheckStyles;
            }
            else
            {
                // Set Dimensions
                List<CheckData> CheckDimensions = new List<CheckData>
                {
                    new CheckData { Name="Steam Vertical - 2:3 - 600x900", Data="600x900" },

                    new CheckData { Name="Steam Horizontal - 92:43 - 920x430", Data="920x430" },
                    new CheckData { Name="Steam Horizontal - 92:43 - 460x215", Data="460x215" },

                    new CheckData { Name="Square - 1:1 - 1024x1024", Data="1024x1024" },
                    new CheckData { Name="Square - 1:1 - 512x512", Data="512x512" },

                    new CheckData { Name="Galaxy 2.0 - 22:31 - 660x930", Data="660x930" },
                    new CheckData { Name="Galaxy 2.0 - 22:31 - 342x482", Data="342x482" },
                };
                PART_ComboDimensions.ItemsSource = CheckDimensions;


                // Set Styles
                List<CheckData> CheckStyles = new List<CheckData>
                {
                    new CheckData { Name="Alternate", Data="alternate" },
                    new CheckData { Name="White Logo", Data="white_logo" },
                    new CheckData { Name="Material", Data="material" },
                    new CheckData { Name="Blurred", Data="blurred" },
                    new CheckData { Name="No Logo", Data="no_logo" }
                };
                PART_ComboStyles.ItemsSource = CheckStyles;
            }

            // Set Types
            List<CheckData> CheckTypes = new List<CheckData>
            {
                new CheckData { Name="Static", Data="static" },
                new CheckData { Name="Animated", Data="animated" }
            };
            PART_ComboTypes.ItemsSource = CheckTypes;

            // Set Tags
            List<CheckData> CheckTags = new List<CheckData>
            {
                new CheckData { Name="Humor", Data="Humor" },
                new CheckData { Name="Adult Content", Data="Adult Content", IsChecked=false },
                new CheckData { Name="Epilepsy", Data="Epilepsy" },
                new CheckData { Name="Untagged", Data="Untagged" }
            };
            PART_ComboTags.ItemsSource = CheckTags;


            ComboBox_SelectionChanged(PART_ComboDimensions, null);
            ComboBox_SelectionChanged(PART_ComboStyles, null);
            ComboBox_SelectionChanged(PART_ComboTags, null);
            ComboBox_SelectionChanged(PART_ComboTypes, null);
        }


        private void SearchData(string Name)
        {
            PART_SearchList.ItemsSource = null;
            PART_ElementList.ItemsSource = null;
            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.IsEnabled = false;

            ButtonSelect.IsEnabled = false;

            string GameSearch = Name;
            _ = Task.Run(() =>
            {
                SteamGridDbSearchResultData DataSearch = null;
                try
                {
                    DataSearch = SteamGridDbApi.SearchGame(GameSearch);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "BackgroundChanger");
                }

                _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                {
                    if (DataSearch != null)
                    {
                        PART_SearchList.ItemsSource = DataSearch.data;
                    }

                    PART_DataLoad.Visibility = Visibility.Collapsed;
                    PART_Data.IsEnabled = true;
                });
            });
        }

        private void SearchDataElements(int Id)
        {
            PART_ElementList.ItemsSource = null;
            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.IsEnabled = false;

            ButtonSelect.IsEnabled = false;

            string GameSearch = SearchElement.Text;
            _ = Task.Run(() =>
            {
                DataSearch = null;
                try
                {
                    DataSearch = SteamGridDbApi.SearchElement(Id, SteamGridDbType);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "BackgroundChanger");
                }

                _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                {
                    ButtonSelectAll.IsEnabled = DataSearch?.data?.Count > 0;
                    if (DataSearch != null)
                    {
                        ApplyFilter(null, null);
                    }

                    PART_DataLoad.Visibility = Visibility.Collapsed;
                    PART_Data.IsEnabled = true;
                });
            });
        }


        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            ((Window)this.Parent).Close();
        }

        private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
        {
            PART_ElementList.SelectAll();
            ((Window)this.Parent).Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ((Window)this.Parent).Close();
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
                    int Id = ((SteamGridDbSearchResult)PART_SearchList.SelectedItem).id;
                    SearchDataElements(Id);
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
                DataSearchFiltered = Serialization.GetClone(DataSearch.data);


                List<CheckData> ListDimensions = PART_ComboDimensions.ItemsSource as List<CheckData>;
                DataSearchFiltered = DataSearchFiltered.Where(x => ListDimensions.Any(y => (x.width + "x" + x.height) == y.Data && y.IsChecked)).ToList();

                List<CheckData> ListStyles = PART_ComboStyles.ItemsSource as List<CheckData>;
                DataSearchFiltered = DataSearchFiltered.Where(x => ListStyles.Any(y => x.style == y.Data && y.IsChecked)).ToList();

                List<CheckData> ListTags = PART_ComboTags.ItemsSource as List<CheckData>;
                bool humor = ListTags.Find(x => x.Name == "Humor").IsChecked;
                bool nsfw = ListTags.Find(x => x.Name == "Adult Content").IsChecked;
                bool epilepsy = ListTags.Find(x => x.Name == "Epilepsy").IsChecked;
                bool untagged = ListTags.Find(x => x.Name == "Untagged").IsChecked;
                DataSearchFiltered = DataSearchFiltered.Where(x => (humor && x.humor) || (nsfw && x.nsfw) || (epilepsy && x.epilepsy) || (untagged && x.untagged)).ToList();

                List<CheckData> ListTypes = PART_ComboTypes.ItemsSource as List<CheckData>;
                if (ListTypes[0].IsChecked && !ListTypes[1].IsChecked)
                {
                    DataSearchFiltered = DataSearchFiltered.Where(x => ListTypes.Any(y => x.mime != "image/webp" && y.IsChecked)).ToList();
                }
                else if (!ListTypes[0].IsChecked && ListTypes[1].IsChecked)
                {
                    DataSearchFiltered = DataSearchFiltered.Where(x => ListTypes.Any(y => x.mime == "image/webp" && y.IsChecked)).ToList();
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
            if (DataSearchFiltered != null)
            {
                if (sender != null)
                {
                    string BtName = ((ToggleButton)sender).Name;
                    bool IsChecked = (bool)((ToggleButton)sender).IsChecked;

                    if (BtName == "PART_ButtonSortByDate_Asc")
                    {
                        if (IsChecked)
                        {
                            PART_ButtonSortByDate_Desc.IsChecked = false;

                            PART_ButtonSortByScore_Asc.IsChecked = false;
                            PART_ButtonSortByScore_Desc.IsChecked = false;
                        }
                    }

                    if (BtName == "PART_ButtonSortByDate_Desc")
                    {
                        if (IsChecked)
                        {
                            PART_ButtonSortByDate_Asc.IsChecked = false;

                            PART_ButtonSortByScore_Asc.IsChecked = false;
                            PART_ButtonSortByScore_Desc.IsChecked = false;
                        }
                    }


                    if (BtName == "PART_ButtonSortByScore_Asc")
                    {
                        if (IsChecked)
                        {
                            PART_ButtonSortByDate_Asc.IsChecked = false;
                            PART_ButtonSortByDate_Desc.IsChecked = false;

                            PART_ButtonSortByScore_Desc.IsChecked = false;
                        }
                    }

                    if (BtName == "PART_ButtonSortByScore_Desc")
                    {
                        if (IsChecked)
                        {
                            PART_ButtonSortByDate_Asc.IsChecked = false;
                            PART_ButtonSortByDate_Desc.IsChecked = false;

                            PART_ButtonSortByScore_Asc.IsChecked = false;
                        }
                    }
                }


                if ((bool)PART_ButtonSortByDate_Asc.IsChecked)
                {
                    DataSearchFiltered.Sort((x, y) => x.id.CompareTo(y.id));
                }

                if ((bool)PART_ButtonSortByDate_Desc.IsChecked)
                {
                    DataSearchFiltered.Sort((x, y) => y.id.CompareTo(x.id));
                }


                if ((bool)PART_ButtonSortByScore_Asc.IsChecked)
                {
                    DataSearchFiltered.Sort((x, y) => x.score.CompareTo(y.score));
                }

                if ((bool)PART_ButtonSortByScore_Desc.IsChecked)
                {
                    DataSearchFiltered.Sort((x, y) => y.score.CompareTo(x.score));
                }


                PART_ElementList.ItemsSource = null;
                PART_ElementList.ItemsSource = DataSearchFiltered;
            }
        }


        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string data = ((ComboBox)sender).Name == "PART_ComboDimensions"
                ? string.Join(", ", ((List<CheckData>)((ComboBox)sender).ItemsSource).Where(x => x.IsChecked).Select(x => x.Name.Split('-')[2].Trim()))
                : string.Join(", ", ((List<CheckData>)((ComboBox)sender).ItemsSource).Where(x => x.IsChecked).Select(x => x.Name));

            ((ComboBox)sender).Text = data;
        }
    }


    public class CheckData
    {
        public string Name { get; set; }
        public string Data { get; set; }
        public bool IsChecked { get; set; } = true;
    }
}
