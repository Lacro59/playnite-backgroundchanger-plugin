using BackgroundChanger.Models;
using BackgroundChanger.Services;
using CommonPluginsControls.PlayniteControls;
using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Steam store game search and official CDN media selection (no filters).
    /// </summary>
    public partial class SteamSelectView : UserControl
    {
        private static BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        private readonly SteamOfficialMediaService _steamMediaService;
        private readonly BackgroundChangerDatabase.PluginMediaKind _mediaKind;

        private uint? _selectedAppId;

        /// <summary>
        /// Gets the candidates confirmed by the user, or <c>null</c> when cancelled.
        /// </summary>
        public List<SteamOfficialMediaCandidate> SelectedResults { get; private set; }

        /// <summary>
        /// Gets the thumbnail preview width for the current media kind.
        /// </summary>
        public double PreviewWidth { get; }

        /// <summary>
        /// Gets the thumbnail preview height for the current media kind.
        /// </summary>
        public double PreviewHeight { get; }

        /// <summary>
        /// Opens a Steam game search and media selection view for the given Playnite game name.
        /// </summary>
        /// <param name="gameName">Initial search term (Playnite game name).</param>
        /// <param name="mediaKind">Background, Cover, or Icon.</param>
        /// <param name="steamMediaService">Steam official media service.</param>
        public SteamSelectView(
            string gameName,
            BackgroundChangerDatabase.PluginMediaKind mediaKind,
            SteamOfficialMediaService steamMediaService)
        {
            if (steamMediaService == null)
            {
                throw new ArgumentNullException(nameof(steamMediaService));
            }

            _steamMediaService = steamMediaService;
            _mediaKind = mediaKind;

            if (mediaKind == BackgroundChangerDatabase.PluginMediaKind.Cover)
            {
                PreviewWidth = 140;
                PreviewHeight = 210;
            }
            else if (mediaKind == BackgroundChangerDatabase.PluginMediaKind.Icon)
            {
                PreviewWidth = 128;
                PreviewHeight = 128;
            }
            else
            {
                PreviewWidth = 320;
                PreviewHeight = 150;
            }

            InitializeComponent();
            DataContext = this;

            SearchElement.Text = gameName ?? string.Empty;
            SearchGames(gameName);
        }

        private void SearchGames(string searchTerm)
        {
            PART_SearchList.ItemsSource = null;
            ClearMediaList();

            List<GenericItemOption> searchResults = null;
            if (API.Instance.Dialogs.ActivateGlobalProgress((_) =>
            {
                try
                {
                    searchResults = _steamMediaService.SearchGames(searchTerm)?.ToList();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }, new GlobalProgressOptions("LOCDownloadingLabel")).Result != true)
            {
                return;
            }

            if (searchResults != null)
            {
                PART_SearchList.ItemsSource = searchResults;
                Common.LogDebug(
                    false,
                    string.Format(
                        "[SteamSelectView] Search term={0} mediaKind={1} games={2}",
                        searchTerm,
                        _mediaKind,
                        searchResults.Count));
            }
        }

        private void LoadMediaForSelectedGame(GenericItemOption selectedGame)
        {
            ClearMediaList();
            _selectedAppId = null;

            if (selectedGame == null)
            {
                return;
            }

            uint appId = SteamOfficialMediaService.ParseAppIdFromSearchOption(selectedGame);
            if (appId == 0)
            {
                Common.LogDebug(
                    false,
                    string.Format(
                        "[SteamSelectView] Invalid AppId from search game={0}",
                        selectedGame.Name));
                return;
            }

            _selectedAppId = appId;
            IReadOnlyList<SteamOfficialMediaCandidate> candidates = null;

            if (API.Instance.Dialogs.ActivateGlobalProgress((_) =>
            {
                try
                {
                    if (_mediaKind == BackgroundChangerDatabase.PluginMediaKind.Icon)
                    {
                        SteamOfficialMediaCandidate iconCandidate = _steamMediaService.GetIconCandidate(appId);
                        candidates = iconCandidate != null
                            ? new List<SteamOfficialMediaCandidate> { iconCandidate }
                            : new List<SteamOfficialMediaCandidate>();
                    }
                    else
                    {
                        candidates = _steamMediaService.GetAvailableCdnCandidates(appId, _mediaKind);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }, new GlobalProgressOptions("LOCDownloadingLabel")).Result != true)
            {
                return;
            }

            int count = candidates?.Count ?? 0;
            Common.LogDebug(
                false,
                string.Format(
                    "[SteamSelectView] Media probe appId={0} game={1} mediaKind={2} available={3}",
                    appId,
                    selectedGame.Name,
                    _mediaKind,
                    count));

            if (count == 0)
            {
                API.Instance.Dialogs.ShowErrorMessage(
                    API.Instance.Resources.GetString("LOCBcSteamNoAssets"),
                    PluginDatabase.PluginName);
                return;
            }

            PART_ElementList.ItemsSource = candidates;
            PART_TotalFound.Content = count.ToString();
            ButtonSelectAll.IsEnabled = count > 0;
        }

        private void ClearMediaList()
        {
            PART_ElementList.ItemsSource = null;
            PART_TotalFound.Content = "0";
            ButtonSelect.IsEnabled = false;
            ButtonSelectAll.IsEnabled = false;
        }

        private void PART_BtSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchGames(SearchElement.Text);
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
                if (PART_SearchList?.SelectedItem is GenericItemOption selectedGame)
                {
                    LoadMediaForSelectedGame(selectedGame);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void PART_ElementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ButtonSelect.IsEnabled = PART_ElementList.SelectedItems != null
                    && PART_ElementList.SelectedItems.Count > 0;
            }
            catch
            {
                ButtonSelect.IsEnabled = false;
            }
        }

        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            SelectedResults = PART_ElementList.SelectedItems
                .Cast<SteamOfficialMediaCandidate>()
                .ToList();
            Common.LogDebug(
                false,
                string.Format(
                    "[SteamSelectView] Select confirmed appId={0} count={1}",
                    _selectedAppId,
                    SelectedResults.Count));
            CloseParentWindow();
        }

        private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
        {
            PART_ElementList.SelectAll();
            SelectedResults = PART_ElementList.SelectedItems
                .Cast<SteamOfficialMediaCandidate>()
                .ToList();
            Common.LogDebug(
                false,
                string.Format(
                    "[SteamSelectView] Select all confirmed appId={0} count={1}",
                    _selectedAppId,
                    SelectedResults.Count));
            CloseParentWindow();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedResults = null;
            Common.LogDebug(false, "[SteamSelectView] Select cancelled");
            CloseParentWindow();
        }

        private void CloseParentWindow()
        {
            Window parent = Parent as Window;
            if (parent != null)
            {
                parent.Close();
            }
        }
    }
}
