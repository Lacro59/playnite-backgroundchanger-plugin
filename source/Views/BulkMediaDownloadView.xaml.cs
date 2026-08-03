using BackgroundChanger.Models;
using CommonPluginsShared;
using Playnite.SDK;
using System;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanger.Views
{
    /// <summary>
    /// Dialog for configuring bulk Steam / SteamGridDB media download options.
    /// </summary>
    public partial class BulkMediaDownloadView : UserControl
    {
        private const string LogPrefix = "[BulkMediaDownload]";

        private readonly BackgroundChanger _plugin;

        /// <summary>
        /// Gets the working options edited in this dialog.
        /// </summary>
        public BulkMediaDownloadOptions Options { get; }

        /// <summary>
        /// Gets whether the user confirmed download (as opposed to cancel).
        /// </summary>
        public bool Confirmed { get; private set; }

        /// <summary>
        /// Initializes a new instance with a working copy of bulk options.
        /// </summary>
        /// <param name="options">Options to edit; cloned caller-side before opening when persisting separately.</param>
        /// <param name="plugin">Plugin instance used to save settings.</param>
        public BulkMediaDownloadView(BulkMediaDownloadOptions options, BackgroundChanger plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            Options = options ?? BulkMediaDownloadOptions.CreateDefaults();
            Options.EnsureInitialized();

            InitializeComponent();
            InitializePickModeItems();
            LoadUiFromOptions();
            RefreshDownloadEnabled();
            RefreshSgdbFiltersVisibility();
            RefreshQuantityEnabled();
            RefreshGameFilterDependentControls();
        }

        /// <summary>
        /// Shows the bulk options dialog and returns the confirmed options, or null when cancelled.
        /// </summary>
        /// <param name="plugin">Plugin instance.</param>
        /// <returns>Confirmed options, or <c>null</c> if the user cancelled.</returns>
        public static BulkMediaDownloadOptions ShowDialog(BackgroundChanger plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            BackgroundChangerSettings live = ResolveLiveSettings(plugin);
            BulkMediaDownloadOptions working = live.BulkMediaDownload.Clone();
            Common.LogDebug(true, string.Format(
                "{0} Open dialog from persisted options OnlyMissing={1} PickMode={2} Cover={3} Icon={4} Qty={5} BG dims checked={6}",
                LogPrefix,
                working.OnlyMissing,
                working.PickMode,
                working.EnableCover,
                working.EnableIcon,
                working.Quantity,
                CountChecked(working.BackgroundFilters?.CheckDimensions)));

            BulkMediaDownloadView view = new BulkMediaDownloadView(working, plugin);
            Window window = PlayniteUiHelper.CreateExtensionWindow(
                ResourceProvider.GetString("LOCBcBulkDownloadTitle"),
                view);
            window.ShowDialog();

            return view.Confirmed ? view.Options : null;
        }

        /// <summary>
        /// Loads bulk options from disk into the live settings instance (source of truth after restart
        /// and after plugin-settings <c>CancelEdit</c> which can restore a stale in-memory tree).
        /// </summary>
        private static BackgroundChangerSettings ResolveLiveSettings(BackgroundChanger plugin)
        {
            BackgroundChangerSettings live = BackgroundChanger.PluginDatabase.PluginSettings;

            BackgroundChangerSettings fromDisk = plugin.LoadPluginSettings<BackgroundChangerSettings>();
            if (fromDisk?.BulkMediaDownload != null)
            {
                live.BulkMediaDownload = fromDisk.BulkMediaDownload;
            }
            else if (live.BulkMediaDownload == null)
            {
                live.BulkMediaDownload = BulkMediaDownloadOptions.CreateDefaults();
            }

            live.BulkMediaDownload.EnsureInitialized();
            return live;
        }

        private static int CountChecked(System.Collections.Generic.List<CheckData> items)
        {
            if (items == null)
            {
                return 0;
            }

            int count = 0;
            foreach (CheckData item in items)
            {
                if (item != null && item.IsChecked)
                {
                    count++;
                }
            }

            return count;
        }

        private void InitializePickModeItems()
        {
            PART_PickMode.Items.Clear();
            PART_PickMode.Items.Add(CreatePickModeItem(BulkMediaPickMode.Random, "LOCBcBulkPickRandom"));
            PART_PickMode.Items.Add(CreatePickModeItem(BulkMediaPickMode.BestScore, "LOCBcBulkPickBestScore"));
            PART_PickMode.Items.Add(CreatePickModeItem(BulkMediaPickMode.BestUpvotes, "LOCBcBulkPickBestVotes"));
            PART_PickMode.Items.Add(CreatePickModeItem(BulkMediaPickMode.NewestById, "LOCBcBulkPickNewest"));
        }

        private static ComboBoxItem CreatePickModeItem(BulkMediaPickMode mode, string locKey)
        {
            return new ComboBoxItem
            {
                Content = ResourceProvider.GetString(locKey),
                Tag = mode
            };
        }

        private void LoadUiFromOptions()
        {
            PART_GameSourceAll.IsChecked = Options.GameSource == BulkGameSource.All;
            PART_GameSourceFiltered.IsChecked = Options.GameSource == BulkGameSource.Filtered;
            PART_GameSourceSelected.IsChecked = Options.GameSource == BulkGameSource.Selected;
            if (PART_GameSourceAll.IsChecked != true
                && PART_GameSourceFiltered.IsChecked != true
                && PART_GameSourceSelected.IsChecked != true)
            {
                PART_GameSourceAll.IsChecked = true;
            }

            PART_InstallInstalled.IsChecked = Options.InstallFilter == BulkGameInstallFilter.Installed;
            PART_InstallNotInstalled.IsChecked = Options.InstallFilter == BulkGameInstallFilter.NotInstalled;
            PART_InstallFavorite.IsChecked = Options.InstallFilter == BulkGameInstallFilter.Favorite;

            PART_TimeOldData.IsChecked = Options.TimeFilter == BulkGameTimeFilter.OldData;
            PART_TimeRecentlyPlayed.IsChecked = Options.TimeFilter == BulkGameTimeFilter.RecentlyPlayed;
            PART_TimeRecentlyAdded.IsChecked = Options.TimeFilter == BulkGameTimeFilter.RecentlyAdded;
            PART_GameFilterMonths.LongValue = Options.GameFilterMonths;

            PART_OnlyGamesWithoutPluginData.IsChecked = Options.OnlyGamesWithoutPluginData;
            PART_EnableSteam.IsChecked = Options.EnableSteam;
            PART_EnableSteamGridDb.IsChecked = Options.EnableSteamGridDb;
            PART_EnableBackground.IsChecked = Options.EnableBackground;
            PART_EnableCover.IsChecked = Options.EnableCover;
            PART_EnableIcon.IsChecked = Options.EnableIcon;
            PART_OnlyMissing.IsChecked = Options.OnlyMissing;
            PART_DownloadAll.IsChecked = Options.DownloadAll;
            PART_Quantity.LongValue = Options.Quantity;
            PART_FuzzyThreshold.LongValue = Options.FuzzyMatchThreshold;
            PART_PromptOnLowFuzzy.IsChecked = Options.PromptOnLowFuzzyMatch;
            PART_AllowAnimated.IsChecked = Options.AllowAnimated;
            SelectPickMode(Options.PickMode);
            BindFilterBoxes();
        }

        private void SelectPickMode(BulkMediaPickMode mode)
        {
            foreach (object item in PART_PickMode.Items)
            {
                ComboBoxItem comboItem = item as ComboBoxItem;
                if (comboItem?.Tag is BulkMediaPickMode itemMode && itemMode == mode)
                {
                    PART_PickMode.SelectedItem = comboItem;
                    return;
                }
            }

            if (PART_PickMode.Items.Count > 0)
            {
                PART_PickMode.SelectedIndex = 0;
            }
        }

        private void BindFilterBoxes()
        {
            PART_BgDimensions.ItemsSource = Options.BackgroundFilters?.CheckDimensions;
            PART_BgStyles.ItemsSource = Options.BackgroundFilters?.CheckStyles;
            PART_BgTypes.ItemsSource = Options.BackgroundFilters?.CheckTypes;
            PART_BgTags.ItemsSource = Options.BackgroundFilters?.CheckTags;

            PART_CoverDimensions.ItemsSource = Options.CoverFilters?.CheckDimensions;
            PART_CoverStyles.ItemsSource = Options.CoverFilters?.CheckStyles;
            PART_CoverTypes.ItemsSource = Options.CoverFilters?.CheckTypes;
            PART_CoverTags.ItemsSource = Options.CoverFilters?.CheckTags;

            PART_IconDimensions.ItemsSource = Options.IconFilters?.CheckDimensions;
            PART_IconStyles.ItemsSource = Options.IconFilters?.CheckStyles;
            PART_IconTypes.ItemsSource = Options.IconFilters?.CheckTypes;
            PART_IconTags.ItemsSource = Options.IconFilters?.CheckTags;
            PART_IconMimes.ItemsSource = Options.IconFilters?.CheckMimes;
        }

        private void CollectUiIntoOptions()
        {
            if (PART_GameSourceFiltered.IsChecked == true)
            {
                Options.GameSource = BulkGameSource.Filtered;
            }
            else if (PART_GameSourceSelected.IsChecked == true)
            {
                Options.GameSource = BulkGameSource.Selected;
            }
            else
            {
                Options.GameSource = BulkGameSource.All;
            }

            if (PART_InstallInstalled.IsChecked == true)
            {
                Options.InstallFilter = BulkGameInstallFilter.Installed;
            }
            else if (PART_InstallNotInstalled.IsChecked == true)
            {
                Options.InstallFilter = BulkGameInstallFilter.NotInstalled;
            }
            else if (PART_InstallFavorite.IsChecked == true)
            {
                Options.InstallFilter = BulkGameInstallFilter.Favorite;
            }
            else
            {
                Options.InstallFilter = BulkGameInstallFilter.None;
            }

            if (PART_TimeOldData.IsChecked == true)
            {
                Options.TimeFilter = BulkGameTimeFilter.OldData;
            }
            else if (PART_TimeRecentlyPlayed.IsChecked == true)
            {
                Options.TimeFilter = BulkGameTimeFilter.RecentlyPlayed;
            }
            else if (PART_TimeRecentlyAdded.IsChecked == true)
            {
                Options.TimeFilter = BulkGameTimeFilter.RecentlyAdded;
            }
            else
            {
                Options.TimeFilter = BulkGameTimeFilter.None;
            }

            Options.GameFilterMonths = (int)PART_GameFilterMonths.LongValue;
            Options.OnlyGamesWithoutPluginData = PART_OnlyGamesWithoutPluginData.IsChecked == true;
            Options.EnableSteam = PART_EnableSteam.IsChecked == true;
            Options.EnableSteamGridDb = PART_EnableSteamGridDb.IsChecked == true;
            Options.EnableBackground = PART_EnableBackground.IsChecked == true;
            Options.EnableCover = PART_EnableCover.IsChecked == true;
            Options.EnableIcon = PART_EnableIcon.IsChecked == true;
            Options.OnlyMissing = PART_OnlyMissing.IsChecked == true;
            Options.DownloadAll = PART_DownloadAll.IsChecked == true;
            Options.Quantity = (int)PART_Quantity.LongValue;
            Options.FuzzyMatchThreshold = (int)PART_FuzzyThreshold.LongValue;
            Options.PromptOnLowFuzzyMatch = PART_PromptOnLowFuzzy.IsChecked == true;
            Options.AllowAnimated = PART_AllowAnimated.IsChecked == true;

            ComboBoxItem selectedPick = PART_PickMode.SelectedItem as ComboBoxItem;
            if (selectedPick?.Tag is BulkMediaPickMode pickMode)
            {
                Options.PickMode = pickMode;
            }

            Options.EnsureInitialized();
        }

        private void RefreshGameFilterDependentControls()
        {
            bool useMonthFilter = PART_TimeOldData.IsChecked == true
                || PART_TimeRecentlyPlayed.IsChecked == true
                || PART_TimeRecentlyAdded.IsChecked == true;
            PART_MonthSelect.IsEnabled = useMonthFilter;
            PART_OnlyGamesWithoutPluginData.IsEnabled = PART_TimeOldData.IsChecked != true;
        }

        private void GameFilterChanged(object sender, RoutedEventArgs e)
        {
            RefreshGameFilterDependentControls();
        }

        /// <summary>
        /// Writes the current working options into live plugin settings and persists to disk.
        /// </summary>
        /// <returns><c>true</c> when options were valid and saved; otherwise <c>false</c>.</returns>
        private bool TryPersistOptions()
        {
            CollectUiIntoOptions();

            if (!Options.HasAnySource || !Options.HasAnyMediaKind)
            {
                API.Instance.Dialogs.ShowMessage(
                    ResourceProvider.GetString("LOCBcBulkOptionsInvalid"),
                    ResourceProvider.GetString("LOCBcBulkDownloadTitle"));
                return false;
            }

            BackgroundChangerSettings settings = BackgroundChanger.PluginDatabase.PluginSettings;
            settings.BulkMediaDownload = Options.Clone();
            _plugin.SavePluginSettings(settings);

            BackgroundChangerSettingsViewModel viewModel = _plugin.GetSettings(false) as BackgroundChangerSettingsViewModel;
            viewModel?.SyncEditingCloneBulkMediaDownload();

            Common.LogDebug(true, string.Format(
                "{0} Persisted options OnlyMissing={1} PickMode={2} Cover={3} Icon={4} Qty={5}",
                LogPrefix,
                settings.BulkMediaDownload.OnlyMissing,
                settings.BulkMediaDownload.PickMode,
                settings.BulkMediaDownload.EnableCover,
                settings.BulkMediaDownload.EnableIcon,
                settings.BulkMediaDownload.Quantity));

            return true;
        }

        private void RefreshDownloadEnabled()
        {
            bool hasSource = PART_EnableSteam.IsChecked == true || PART_EnableSteamGridDb.IsChecked == true;
            bool hasKind = PART_EnableBackground.IsChecked == true
                || PART_EnableCover.IsChecked == true
                || PART_EnableIcon.IsChecked == true;
            PART_BtDownload.IsEnabled = hasSource && hasKind;
        }

        private void RefreshSgdbFiltersVisibility()
        {
            PART_SgdbFiltersSection.Visibility = PART_EnableSteamGridDb.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RefreshQuantityEnabled()
        {
            PART_Quantity.IsEnabled = PART_DownloadAll.IsChecked != true;
        }

        private void OptionsChanged(object sender, RoutedEventArgs e)
        {
            RefreshDownloadEnabled();
        }

        private void SteamGridDbSourceChanged(object sender, RoutedEventArgs e)
        {
            RefreshDownloadEnabled();
            RefreshSgdbFiltersVisibility();
        }

        private void DownloadAllChanged(object sender, RoutedEventArgs e)
        {
            RefreshQuantityEnabled();
        }

        private void AllowAnimatedChanged(object sender, RoutedEventArgs e)
        {
            Options.AllowAnimated = PART_AllowAnimated.IsChecked == true;
            Options.EnsureInitialized();
            BindFilterBoxes();
        }

        private void PART_BtSaveOptions_Click(object sender, RoutedEventArgs e)
        {
            if (!TryPersistOptions())
            {
                return;
            }

            API.Instance.Dialogs.ShowMessage(
                ResourceProvider.GetString("LOCBcBulkOptionsSaved"),
                ResourceProvider.GetString("LOCBcBulkDownloadTitle"));
        }

        private void PART_BtDownload_Click(object sender, RoutedEventArgs e)
        {
            // Persist last-used options so they survive restart (same path as Save options).
            if (!TryPersistOptions())
            {
                return;
            }

            Confirmed = true;
            CloseParentWindow();
        }

        private void PART_BtCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            CloseParentWindow();
        }

        private void CloseParentWindow()
        {
            Window parent = Window.GetWindow(this);
            parent?.Close();
        }
    }
}
