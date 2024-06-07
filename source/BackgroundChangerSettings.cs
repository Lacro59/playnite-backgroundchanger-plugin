using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BackgroundChanger
{
    public class BackgroundChangerSettings : ObservableObject
    {
        #region Settings variables
        public bool MenuInExtensions { get; set; } = true;


        private bool enableBackgroundImage = true;
        public bool EnableBackgroundImage { get => enableBackgroundImage; set => SetValue(ref enableBackgroundImage, value); }

        public bool BackgroundImageSameSettings { get; set; } = true;

        public bool EnableBackgroundImageRandomSelect { get; set; } = false;
        public bool EnableBackgroundImageAutoChanger { get; set; } = false;
        public int BackgroundImageAutoChangerTimer { get; set; } = 10;

        private bool enableImageAnimatedBackground = false;
        public bool EnableImageAnimatedBackground { get => enableImageAnimatedBackground; set => SetValue(ref enableImageAnimatedBackground, value); }

        public double Volume { get; set; } = 0;


        private bool enableCoverImage = true;
        public bool EnableCoverImage { get => enableCoverImage; set => SetValue(ref enableCoverImage, value); }

        public bool EnableCoverImageRandomSelect { get; set; } = false;
        public bool EnableCoverImageAutoChanger { get; set; } = false;
        public int CoverImageAutoChangerTimer { get; set; } = 10;

        private bool enableImageAnimatedCover = false;
        public bool EnableImageAnimatedCover { get => enableImageAnimatedCover; set => SetValue(ref enableImageAnimatedCover, value); }


        public string SteamGridDbApiKey { get; set; } = string.Empty;


        public string ffmpegFile { get; set; } = string.Empty;
        public string webpinfoFile { get; set; } = string.Empty;


        public bool useVideoDelayBackgroundImage { get; set; } = false;
        public int videoDelayBackgroundImage { get; set; } = 5;
        public bool useVideoDelayCoverImage { get; set; } = false;
        public int videoDelayCoverImage { get; set; } = 5;
        #endregion

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed
        private bool hasData = false;
        [DontSerialize]
        public bool HasData { get => hasData; set => SetValue(ref hasData, value); }

        private bool hasDataBackground = false;
        [DontSerialize]
        public bool HasDataBackground { get => hasDataBackground; set => SetValue(ref hasDataBackground, value); }

        private bool hasDataCover = false;
        [DontSerialize]
        public bool HasDataCover { get => hasDataCover; set => SetValue(ref hasDataCover, value); }
        #endregion
    }


    public class BackgroundChangerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly BackgroundChanger Plugin;
        private BackgroundChangerSettings EditingClone { get; set; }

        private BackgroundChangerSettings settings;
        public BackgroundChangerSettings Settings { get => settings; set => SetValue(ref settings, value); }


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
}
