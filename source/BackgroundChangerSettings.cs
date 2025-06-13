using BackgroundChanger.Models;
using CommonPluginsShared.Plugins;
using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BackgroundChanger
{
    public class BackgroundChangerSettings : PluginSettings
    {
        #region Settings variables

        private bool _enableBackgroundImage = true;
        public bool EnableBackgroundImage { get => _enableBackgroundImage; set => SetValue(ref _enableBackgroundImage, value); }

        public bool BackgroundImageSameSettings { get; set; } = true;

        public bool EnableBackgroundImageRandomSelect { get; set; } = false;
        public bool EnableBackgroundImageRandomOnStart { get; set; } = true;
        public bool EnableBackgroundImageRandomOnSelect { get; set; } = false;
        public bool EnableBackgroundImageAutoChanger { get; set; } = false;
        public int BackgroundImageAutoChangerTimer { get; set; } = 10;

        private bool _enableImageAnimatedBackground = false;
        public bool EnableImageAnimatedBackground { get => _enableImageAnimatedBackground; set => SetValue(ref _enableImageAnimatedBackground, value); }

        public double Volume { get; set; } = 0;


        private bool _enableCoverImage = true;
        public bool EnableCoverImage { get => _enableCoverImage; set => SetValue(ref _enableCoverImage, value); }

        public bool EnableCoverImageRandomSelect { get; set; } = false;
        public bool EnableCoverImageRandomOnStart { get; set; } = true;
        public bool EnableCoverImageRandomOnSelect { get; set; } = false;
        public bool EnableCoverImageAutoChanger { get; set; } = false;
        public int CoverImageAutoChangerTimer { get; set; } = 10;

        private bool _enableImageAnimatedCover = false;
        public bool EnableImageAnimatedCover { get => _enableImageAnimatedCover; set => SetValue(ref _enableImageAnimatedCover, value); }


        public string SteamGridDbApiKey { get; set; } = string.Empty;


        public string ffmpegFile { get; set; } = string.Empty;
        public string webpinfoFile { get; set; } = string.Empty;


        public bool useVideoDelayBackgroundImage { get; set; } = false;
        public int videoDelayBackgroundImage { get; set; } = 5;
        public bool useVideoDelayCoverImage { get; set; } = false;
        public int videoDelayCoverImage { get; set; } = 5;

        #endregion


        public SteamGridFilters SgGridsFilters = new SteamGridFilters
        {
            CheckDimensions = new List<CheckData>
            {
                new CheckData { Name="Steam Vertical - 2:3 - 600x900", Data="600x900" },
                new CheckData { Name="Steam Horizontal - 92:43 - 920x430", Data="920x430" },
                new CheckData { Name="Steam Horizontal - 92:43 - 460x215", Data="460x215" },
                new CheckData { Name="Square - 1:1 - 1024x1024", Data="1024x1024" },
                new CheckData { Name="Square - 1:1 - 512x512", Data="512x512" },
                new CheckData { Name="Galaxy 2.0 - 22:31 - 660x930", Data="660x930" },
                new CheckData { Name="Galaxy 2.0 - 22:31 - 342x482", Data="342x482" },
            },
            CheckStyles = new List<CheckData>
            {
                new CheckData { Name="Alternate", Data="alternate" },
                new CheckData { Name="White Logo", Data="white_logo" },
                new CheckData { Name="Material", Data="material" },
                new CheckData { Name="Blurred", Data="blurred" },
                new CheckData { Name="No Logo", Data="no_logo" }
            },
            CheckTypes = new List<CheckData>
            {
                new CheckData { Name="Static", Data="static" },
                new CheckData { Name="Animated", Data="animated" }
            },
            CheckTags = new List<CheckData>
            {
                new CheckData { Name="Humor", Data="Humor" },
                new CheckData { Name="Adult Content", Data="Adult Content", IsChecked=false },
                new CheckData { Name="Epilepsy", Data="Epilepsy" },
                new CheckData { Name="Untagged", Data="Untagged" }
            }
        };

        public SteamGridFilters SgHeroesFilters = new SteamGridFilters
        {
            CheckDimensions = new List<CheckData>
            {
                new CheckData { Name="Steam - 96:31 - 1920x620", Data="1920x620" },
                new CheckData { Name="Steam - 96:31 - 3840x1240", Data="3840x1240" },
                new CheckData { Name="Galaxy 2.0 - 32:13 - 1600x650", Data="1600x650" }
            },
            CheckStyles = new List<CheckData>
            {
                new CheckData { Name="Alternate", Data="alternate" },
                new CheckData { Name="Material", Data="material" },
                new CheckData { Name="Blurred", Data="blurred" }
            },
            CheckTypes = new List<CheckData>
            {
                new CheckData { Name="Static", Data="static" },
                new CheckData { Name="Animated", Data="animated" }
            },
            CheckTags = new List<CheckData>
            {
                new CheckData { Name="Humor", Data="Humor" },
                new CheckData { Name="Adult Content", Data="Adult Content", IsChecked=false },
                new CheckData { Name="Epilepsy", Data="Epilepsy" },
                new CheckData { Name="Untagged", Data="Untagged" }
            }
        };


        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed

        private bool _hasDataBackground = false;
        [DontSerialize]
        public bool HasDataBackground { get => _hasDataBackground; set => SetValue(ref _hasDataBackground, value); }

        private bool _hasDataCover = false;
        [DontSerialize]
        public bool HasDataCover { get => _hasDataCover; set => SetValue(ref _hasDataCover, value); }

        #endregion
    }


    public class BackgroundChangerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly BackgroundChanger Plugin;
        private BackgroundChangerSettings EditingClone { get; set; }

        private BackgroundChangerSettings _settings;
        public BackgroundChangerSettings Settings { get => _settings; set => SetValue(ref _settings, value); }


        public BackgroundChangerSettingsViewModel(BackgroundChanger plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            BackgroundChangerSettings savedSettings = plugin.LoadPluginSettings<BackgroundChangerSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            Settings = savedSettings ?? new BackgroundChangerSettings();
        }

        // Code executed when settings view is opened and user starts editing values.
        public void BeginEdit()
        {
            EditingClone = Serialization.GetClone(Settings);
        }

        // Code executed when user decides to cancel any changes made since BeginEdit was called.
        // This method should revert any changes made to Option1 and Option2.
        public void CancelEdit()
        {
            Settings = EditingClone;
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
            Plugin.SavePluginSettings(Settings);
            BackgroundChanger.PluginDatabase.PluginSettings = this;
            this.OnPropertyChanged();
        }

        // Code execute when user decides to confirm changes made since BeginEdit was called.
        // Executed before EndEdit is called and EndEdit is not called if false is returned.
        // List of errors is presented to user if verification fails.
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }


    public class SteamGridFilters
    {
        public List<CheckData> CheckDimensions { get; set; }
        public List<CheckData> CheckStyles { get; set; }
        public List<CheckData> CheckTypes { get; set; }
        public List<CheckData> CheckTags { get; set; }

        public bool SortByDateAsc { get; set; }
    }
}