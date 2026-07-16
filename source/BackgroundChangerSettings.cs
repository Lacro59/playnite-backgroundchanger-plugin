using BackgroundChanger.Models;
using BackgroundChanger.Views;
using CommonPluginsShared.Interfaces;
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

        private bool enableBackgroundImage = true;
        public bool EnableBackgroundImage { get => enableBackgroundImage; set => SetValue(ref enableBackgroundImage, value); }

        private bool backgroundImageSameSettings = true;
        public bool BackgroundImageSameSettings { get => backgroundImageSameSettings; set => SetValue(ref backgroundImageSameSettings, value); }

        private bool enableBackgroundImageRandomSelect = false;
        public bool EnableBackgroundImageRandomSelect { get => enableBackgroundImageRandomSelect; set => SetValue(ref enableBackgroundImageRandomSelect, value); }

        private bool enableBackgroundImageRandomOnStart = true;
        public bool EnableBackgroundImageRandomOnStart { get => enableBackgroundImageRandomOnStart; set => SetValue(ref enableBackgroundImageRandomOnStart, value); }

        private bool enableBackgroundImageRandomOnSelect = false;
        public bool EnableBackgroundImageRandomOnSelect { get => enableBackgroundImageRandomOnSelect; set => SetValue(ref enableBackgroundImageRandomOnSelect, value); }

        private bool enableBackgroundImageAutoChanger = false;
        public bool EnableBackgroundImageAutoChanger { get => enableBackgroundImageAutoChanger; set => SetValue(ref enableBackgroundImageAutoChanger, value); }

        private int backgroundImageAutoChangerTimer = 10;
        public int BackgroundImageAutoChangerTimer { get => backgroundImageAutoChangerTimer; set => SetValue(ref backgroundImageAutoChangerTimer, value); }

        private double volume = 0;
        public double Volume { get => volume; set => SetValue(ref volume, value); }


        private bool enableCoverImage = true;
        public bool EnableCoverImage { get => enableCoverImage; set => SetValue(ref enableCoverImage, value); }

        private bool enableCoverImageRandomSelect = false;
        public bool EnableCoverImageRandomSelect { get => enableCoverImageRandomSelect; set => SetValue(ref enableCoverImageRandomSelect, value); }

        private bool enableCoverImageRandomOnStart = true;
        public bool EnableCoverImageRandomOnStart { get => enableCoverImageRandomOnStart; set => SetValue(ref enableCoverImageRandomOnStart, value); }

        private bool enableCoverImageRandomOnSelect = false;
        public bool EnableCoverImageRandomOnSelect { get => enableCoverImageRandomOnSelect; set => SetValue(ref enableCoverImageRandomOnSelect, value); }

        private bool enableCoverImageAutoChanger = false;
        public bool EnableCoverImageAutoChanger { get => enableCoverImageAutoChanger; set => SetValue(ref enableCoverImageAutoChanger, value); }

        private int coverImageAutoChangerTimer = 10;
        public int CoverImageAutoChangerTimer { get => coverImageAutoChangerTimer; set => SetValue(ref coverImageAutoChangerTimer, value); }


        private bool enableIconImage = false;
        public bool EnableIconImage { get => enableIconImage; set => SetValue(ref enableIconImage, value); }

        private bool enableIconImageRandomSelect = false;
        public bool EnableIconImageRandomSelect { get => enableIconImageRandomSelect; set => SetValue(ref enableIconImageRandomSelect, value); }

        private bool enableIconImageRandomOnStart = true;
        public bool EnableIconImageRandomOnStart { get => enableIconImageRandomOnStart; set => SetValue(ref enableIconImageRandomOnStart, value); }

        private bool enableIconImageRandomOnSelect = false;
        public bool EnableIconImageRandomOnSelect { get => enableIconImageRandomOnSelect; set => SetValue(ref enableIconImageRandomOnSelect, value); }

        private bool enableIconImageAutoChanger = false;
        public bool EnableIconImageAutoChanger { get => enableIconImageAutoChanger; set => SetValue(ref enableIconImageAutoChanger, value); }

        private int iconImageAutoChangerTimer = 10;
        public int IconImageAutoChangerTimer { get => iconImageAutoChangerTimer; set => SetValue(ref iconImageAutoChangerTimer, value); }


        public string SteamGridDbApiKey { get; set; } = string.Empty;


        public string ffmpegFile { get; set; } = string.Empty;
        public string ffprobeFile { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets per-format animated media conversion parameters.
        /// </summary>
        public MediaConversionSettings MediaConversion { get; set; } = new MediaConversionSettings();


        private bool _useVideoDelayBackgroundImage = false;
        public bool useVideoDelayBackgroundImage { get => _useVideoDelayBackgroundImage; set => SetValue(ref _useVideoDelayBackgroundImage, value); }

        private int _videoDelayBackgroundImage = 5;
        public int videoDelayBackgroundImage { get => _videoDelayBackgroundImage; set => SetValue(ref _videoDelayBackgroundImage, value); }

        private bool _useVideoDelayCoverImage = false;
        public bool useVideoDelayCoverImage { get => _useVideoDelayCoverImage; set => SetValue(ref _useVideoDelayCoverImage, value); }

        private int _videoDelayCoverImage = 5;
        public int videoDelayCoverImage { get => _videoDelayCoverImage; set => SetValue(ref _videoDelayCoverImage, value); }

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

        private bool hasDataBackground = false;
        [DontSerialize]
        public bool HasDataBackground { get => hasDataBackground; set => SetValue(ref hasDataBackground, value); }

        private bool hasDataCover = false;
        [DontSerialize]
        public bool HasDataCover { get => hasDataCover; set => SetValue(ref hasDataCover, value); }

        private bool hasDataIcon = false;
        [DontSerialize]
        public bool HasDataIcon { get => hasDataIcon; set => SetValue(ref hasDataIcon, value); }

        private bool backgroundIsVideo = false;
        [DontSerialize]
        public bool BackgroundIsVideo { get => backgroundIsVideo; set => SetValue(ref backgroundIsVideo, value); }

        private bool coverIsVideo = false;
        [DontSerialize]
        public bool CoverIsVideo { get => coverIsVideo; set => SetValue(ref coverIsVideo, value); }

        #endregion
    }


    public class BackgroundChangerSettingsViewModel : PluginSettingsViewModel, IPluginSettingsViewModel
    {
        private readonly BackgroundChanger Plugin;
        private BackgroundChangerSettings EditingClone { get; set; }

        private BackgroundChangerSettings settings;
        public BackgroundChangerSettings Settings { get => settings; }

        IPluginSettings IPluginSettingsViewModel.Settings => Settings;


        public BackgroundChangerSettingsViewModel(BackgroundChanger plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            BackgroundChangerSettings savedSettings = plugin.LoadPluginSettings<BackgroundChangerSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            settings = savedSettings ?? new BackgroundChangerSettings();

            if (settings.MediaConversion == null)
            {
                settings.MediaConversion = new MediaConversionSettings();
            }
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
            CopySettingsValues(EditingClone, Settings);
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
            Settings.EnableBackgroundImageRandomOnSelect = BackgroundChangerSettingsView.BackgroundOnSelect;
            Settings.EnableBackgroundImageRandomOnStart = BackgroundChangerSettingsView.BackgroundOnStart;

            Settings.EnableCoverImageRandomOnSelect = BackgroundChangerSettingsView.CoverOnSelect;
            Settings.EnableCoverImageRandomOnStart = BackgroundChangerSettingsView.CoverOnStart;

            if (Settings.EnableIconImageAutoChanger)
            {
                Settings.EnableIconImageRandomOnStart = false;
            }
            else if (Settings.EnableIconImageRandomSelect)
            {
                Settings.EnableIconImageRandomOnStart = true;
            }

            Settings.EnableIconImageRandomOnSelect = false;

            Plugin.SavePluginSettings(Settings);
        }

        // Code execute when user decides to confirm changes made since BeginEdit was called.
        // Executed before EndEdit is called and EndEdit is not called if false is returned.
        // List of errors is presented to user if verification fails.
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (Settings?.MediaConversion == null)
            {
                return true;
            }

            MediaConversionSettings conversion = Settings.MediaConversion;

            if (!MediaConversionSettings.IsValidDefaultCrf(conversion.Defaults?.Crf ?? MediaConversionDefaults.DefaultCrf))
            {
                errors.Add(ResourceProvider.GetString("LOCBcMediaConversionCrfInvalid"));
            }

            ValidateFormatCrf(conversion.AnimatedWebp, errors);
            ValidateFormatCrf(conversion.Webm, errors);
            ValidateFormatCrf(conversion.Apng, errors);
            ValidateFormatCrf(conversion.Gif, errors);

            ValidateFormatFramerate(conversion.AnimatedWebp, errors);
            ValidateFormatFramerate(conversion.Apng, errors);
            ValidateFormatFramerate(conversion.Gif, errors);

            return errors.Count == 0;
        }

        private static void ValidateFormatCrf(MediaConversionFormatSettings formatSettings, List<string> errors)
        {
            if (formatSettings != null && !MediaConversionSettings.IsValidCrf(formatSettings.Crf))
            {
                errors.Add(ResourceProvider.GetString("LOCBcMediaConversionCrfInvalid"));
            }
        }

        private static void ValidateFormatFramerate(MediaConversionFormatSettings formatSettings, List<string> errors)
        {
            if (formatSettings != null
                && !formatSettings.UseAutoFramerate
                && !MediaConversionSettings.IsValidFramerate(formatSettings.FixedFramerate))
            {
                errors.Add(ResourceProvider.GetString("LOCBcMediaConversionFramerateInvalid"));
            }
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