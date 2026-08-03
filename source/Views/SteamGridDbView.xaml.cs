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
using SteamGridFilters = BackgroundChanger.SteamGridFilters;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Interaction logic for SteamGridDbView.xaml.
    /// </summary>
    public partial class SteamGridDbView : UserControl
    {
        private BackgroundChanger Plugin { get; }
        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        private SteamGridDbApi SteamGridDbApi { get; set; } = new SteamGridDbApi();
        private BackgroundChangerDatabase.PluginMediaKind MediaKind { get; }
        private SteamGridFilterSlot FilterSlot { get; }
        private SteamGridDbType SteamGridDbType { get; }

        public List<SteamGridDbResult> SteamGridDbResults { get; set; }

        private SteamGridDbResultData DataSearch { get; set; } = null;
        private List<SteamGridDbResult> DataSearchFiltered { get; set; } = null;
        private int? SelectedGameId { get; set; }


        /// <summary>
        /// Opens the SteamGridDB browser for the given game name and media context.
        /// </summary>
        /// <param name="name">Game name used for the initial search.</param>
        /// <param name="mediaKind">Background, cover, or icon — drives API type and persisted filters.</param>
        /// <param name="plugin">Plugin instance for settings persistence.</param>
        public SteamGridDbView(string name, BackgroundChangerDatabase.PluginMediaKind mediaKind, BackgroundChanger plugin)
        {
            InitializeComponent();

            Plugin = plugin;
            MediaKind = mediaKind;
            FilterSlot = SteamGridFilterHelper.ResolveFilterSlot(mediaKind);
            SteamGridDbType = SteamGridFilterHelper.ResolveApiType(mediaKind);

            SearchElement.Text = name;
            LoadFilters();
            SearchData(name);
        }

        private void LoadFilters()
        {
            SteamGridFilters savedFilters = SteamGridFilterHelper.GetFilters(PluginDatabase.PluginSettings, FilterSlot);
            SteamGridFilters normalizedFilters = SteamGridFilterHelper.NormalizeFilters(savedFilters, SteamGridDbType);

            PART_FilterDimensions.ItemsSource = normalizedFilters.CheckDimensions;
            PART_FilterStyles.ItemsSource = normalizedFilters.CheckStyles;
            PART_FilterTypes.ItemsSource = normalizedFilters.CheckTypes;
            PART_FilterTags.ItemsSource = normalizedFilters.CheckTags;
            PART_FilterMimes.ItemsSource = normalizedFilters.CheckMimes;

            PART_ButtonSortByDate_Asc.IsChecked = normalizedFilters.SortByDateAsc;
            PART_ButtonSortByDate_Desc.IsChecked = !normalizedFilters.SortByDateAsc;

            ApplyContextualFilterPanels();
            RefreshFilterSummaries();

            Common.LogDebug(false, string.Format(
                "[SteamGridDbView] LoadFilters mediaKind={0} slot={1} apiType={2} active={3}",
                MediaKind,
                FilterSlot,
                SteamGridDbType,
                SteamGridFilterHelper.BuildActiveFiltersDebugSummary(normalizedFilters)));
        }

        private void ApplyContextualFilterPanels()
        {
            bool isIconContext = SteamGridDbType == SteamGridDbType.icons;
            PART_MimesFilterRow.Visibility = isIconContext ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshFilterSummaries()
        {
            PART_FilterDimensions.RefreshSummary();
            PART_FilterStyles.RefreshSummary();
            PART_FilterTypes.RefreshSummary();
            PART_FilterTags.RefreshSummary();
            if (SteamGridDbType == SteamGridDbType.icons)
            {
                PART_FilterMimes.RefreshSummary();
            }
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
            SelectedGameId = id;
            RefetchDataElements(id, "gameSelected");
        }

        private void RefetchDataElements(int id, string reason)
        {
            PART_ElementList.ItemsSource = null;
            ButtonSelect.IsEnabled = false;

            DataSearch = null;
            SteamGridFilters activeFilters = CollectActiveFiltersFromUi();

            Common.LogDebug(false, string.Format(
                "[SteamGridDbView] Refetch reason={0} gameId={1} mediaKind={2} slot={3} apiType={4} active={5} url={6}",
                reason,
                id,
                MediaKind,
                FilterSlot,
                SteamGridDbType,
                SteamGridFilterHelper.BuildActiveFiltersDebugSummary(activeFilters),
                SteamGridDbApi.BuildSearchUrl(id, SteamGridDbType, activeFilters, 0)));

            int pagesFetched = 0;
            if (API.Instance.Dialogs.ActivateGlobalProgress((_) =>
            {
                try
                {
                    SteamGridDbResultData steamGridDbResultData = SteamGridDbApi.SearchElement(id, SteamGridDbType, activeFilters);
                    DataSearch = new SteamGridDbResultData
                    {
                        Data = new List<SteamGridDbResult>()
                    };

                    int page = 0;
                    while (steamGridDbResultData?.Data?.Count > 0)
                    {
                        DataSearch.Data.AddRange(steamGridDbResultData.Data);
                        pagesFetched++;
                        page++;
                        steamGridDbResultData = SteamGridDbApi.SearchElement(id, SteamGridDbType, activeFilters, page);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "BackgroundChanger");
                }
            }, new GlobalProgressOptions("LOCDownloadingLabel")).Result == true)
            {
                int fetchedCount = DataSearch?.Data?.Count ?? 0;
                Common.LogDebug(false, string.Format(
                    "[SteamGridDbView] Refetch done reason={0} gameId={1} pages={2} fetched={3}",
                    reason,
                    id,
                    pagesFetched,
                    fetchedCount));

                ButtonSelectAll.IsEnabled = fetchedCount > 0;
                if (DataSearch != null)
                {
                    ApplyDisplayFilter();
                }
            }
        }

        private SteamGridFilters CollectActiveFiltersFromUi()
        {
            return new SteamGridFilters
            {
                CheckDimensions = PART_FilterDimensions.ItemsSource,
                CheckStyles = PART_FilterStyles.ItemsSource,
                CheckTypes = PART_FilterTypes.ItemsSource,
                CheckTags = PART_FilterTags.ItemsSource,
                CheckMimes = SteamGridDbType == SteamGridDbType.icons
                    ? PART_FilterMimes.ItemsSource
                    : null,
                SortByDateAsc = PART_ButtonSortByDate_Asc.IsChecked == true
            };
        }


        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            SteamGridDbResults = PART_ElementList.SelectedItems.Cast<SteamGridDbResult>().ToList();
            ((Window)Parent).Close();
        }

        private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
        {
            PART_ElementList.SelectAll();
            SteamGridDbResults = PART_ElementList.SelectedItems.Cast<SteamGridDbResult>().ToList();
            ((Window)Parent).Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SteamGridDbResults = null;
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
                    SteamGridDbSearchResult selected = PART_SearchList.SelectedItem as SteamGridDbSearchResult;
                    if (selected == null)
                    {
                        return;
                    }

                    SearchDataElements(selected.Id);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "BackgroundChanger");
            }
        }


        private void PART_ElementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ButtonSelect.IsEnabled = PART_ElementList.SelectedItems.Count > 0;
            }
            catch
            {
                ButtonSelect.IsEnabled = false;
            }
        }


        private void ApplyFilter(object sender, RoutedEventArgs e)
        {
            if (sender != null && SelectedGameId.HasValue)
            {
                RefetchDataElements(SelectedGameId.Value, "filterChanged");
                return;
            }

            ApplyDisplayFilter();
        }

        /// <summary>
        /// Client-side pass after server fetch: tag OR-filter, static/animated edge cases, sort.
        /// </summary>
        private void ApplyDisplayFilter()
        {
            DataSearchFiltered = null;
            PART_TotalFound.Content = "0";

            if (DataSearch != null)
            {
                DataSearchFiltered = Serialization.GetClone(DataSearch.Data);

                List<CheckData> listTags = PART_FilterTags.ItemsSource;
                bool humor = IsFilterChecked(listTags, "Humor");
                bool nsfw = IsFilterChecked(listTags, "Adult Content");
                bool epilepsy = IsFilterChecked(listTags, "Epilepsy");
                bool untagged = IsFilterChecked(listTags, "Untagged");
                DataSearchFiltered = DataSearchFiltered
                    .Where(x => (humor && x.Humor) || (nsfw && x.Nsfw) || (epilepsy && x.Epilepsy) || (untagged && x.Untagged))
                    .ToList();

                List<CheckData> listTypes = PART_FilterTypes.ItemsSource;
                bool staticChecked = IsFilterChecked(listTypes, "static");
                bool animatedChecked = IsFilterChecked(listTypes, "animated");
                if (staticChecked && !animatedChecked)
                {
                    DataSearchFiltered = DataSearchFiltered.Where(x => !x.IsAnimated).ToList();
                }
                else if (!staticChecked && animatedChecked)
                {
                    DataSearchFiltered = DataSearchFiltered.Where(x => x.IsAnimated).ToList();
                }

                PART_ButtonSort_Click(null, null);
            }

            PART_ElementList.ItemsSource = null;
            PART_ElementList.ItemsSource = DataSearchFiltered;

            if (DataSearchFiltered != null)
            {
                PART_TotalFound.Content = DataSearchFiltered.Count;
                Common.LogDebug(false, string.Format(
                    "[SteamGridDbView] DisplayFilter fetched={0} displayed={1}",
                    DataSearch?.Data?.Count ?? 0,
                    DataSearchFiltered.Count));
                LogThumbnailStats(DataSearchFiltered);
            }

            RefreshFilterSummaries();
        }

        private static void LogThumbnailStats(List<SteamGridDbResult> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            int missingThumb = items.Count(x => x.Thumb.IsNullOrEmpty());
            int videoItems = items.Count(x => x.IsVideo);
            int displayableThumbs = items.Count(x => x.HasDisplayableThumbnail);

            Common.LogDebug(false, string.Format(
                "[SteamGridDbView] PART_ElementList: {0} items, {1} missing thumb, {2} video, {3} displayable previews",
                items.Count,
                missingThumb,
                videoItems,
                displayableThumbs));
        }

        private static bool IsFilterChecked(List<CheckData> items, string data)
        {
            return items?.Any(x => x.Data == data && x.IsChecked) == true;
        }

        private void PART_ButtonSort_Click(object sender, RoutedEventArgs e)
        {
            if (DataSearchFiltered == null)
            {
                return;
            }

            if (sender is ToggleButton btn)
            {
                ToggleButton[] sortButtons =
                {
                    PART_ButtonSortByDate_Asc,
                    PART_ButtonSortByDate_Desc
                };

                if (btn.IsChecked == true)
                {
                    foreach (ToggleButton button in sortButtons)
                    {
                        if (button != btn)
                        {
                            button.IsChecked = false;
                        }
                    }
                }
            }

            if (PART_ButtonSortByDate_Asc.IsChecked == true)
            {
                DataSearchFiltered.Sort((x, y) => x.Id.CompareTo(y.Id));
            }
            else if (PART_ButtonSortByDate_Desc.IsChecked == true)
            {
                DataSearchFiltered.Sort((x, y) => y.Id.CompareTo(x.Id));
            }

            PART_ElementList.ItemsSource = null;
            PART_ElementList.ItemsSource = DataSearchFiltered;
        }


        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SteamGridFilters defaultFilters = SteamGridFilterHelper.CreateDefaultFilters(SteamGridDbType);

            PART_FilterDimensions.ItemsSource = defaultFilters.CheckDimensions;
            PART_FilterStyles.ItemsSource = defaultFilters.CheckStyles;
            PART_FilterTypes.ItemsSource = defaultFilters.CheckTypes;
            PART_FilterTags.ItemsSource = defaultFilters.CheckTags;
            PART_FilterMimes.ItemsSource = defaultFilters.CheckMimes;

            PART_ButtonSortByDate_Asc.IsChecked = false;
            PART_ButtonSortByDate_Desc.IsChecked = true;

            if (SelectedGameId.HasValue)
            {
                RefetchDataElements(SelectedGameId.Value, "clearFilters");
            }
            else
            {
                ApplyDisplayFilter();
            }
        }

        private void SavedFilter_Click(object sender, RoutedEventArgs e)
        {
            SteamGridFilters filters = new SteamGridFilters
            {
                CheckDimensions = Serialization.GetClone(PART_FilterDimensions.ItemsSource),
                CheckStyles = Serialization.GetClone(PART_FilterStyles.ItemsSource),
                CheckTypes = Serialization.GetClone(PART_FilterTypes.ItemsSource),
                CheckTags = Serialization.GetClone(PART_FilterTags.ItemsSource),
                CheckMimes = SteamGridDbType == SteamGridDbType.icons
                    ? Serialization.GetClone(PART_FilterMimes.ItemsSource)
                    : null,
                SortByDateAsc = PART_ButtonSortByDate_Asc.IsChecked == true
            };

            SteamGridFilterHelper.SetFilters(PluginDatabase.PluginSettings, FilterSlot, filters);
            Plugin.SavePluginSettings(PluginDatabase.PluginSettings);
            RefreshFilterSummaries();

            Common.LogDebug(false, string.Format(
                "[SteamGridDbView] SavedFilter slot={0} mediaKind={1} active={2}",
                FilterSlot,
                MediaKind,
                SteamGridFilterHelper.BuildActiveFiltersDebugSummary(filters)));
        }
    }
}
