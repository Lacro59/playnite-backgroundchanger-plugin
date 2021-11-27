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


        private bool _EnableBackgroundImage { get; set; } = true;
        public bool EnableBackgroundImage
        {
            get => _EnableBackgroundImage;
            set
            {
                _EnableBackgroundImage = value;
                OnPropertyChanged();
            }
        }

        public bool EnableBackgroundImageRandomSelect { get; set; } = false;
        public bool EnableBackgroundImageAutoChanger { get; set; } = false;
        public int BackgroundImageAutoChangerTimer { get; set; } = 10;

        private bool _EnableImageAnimatedBackground { get; set; } = false;
        public bool EnableImageAnimatedBackground
        {
            get => _EnableImageAnimatedBackground;
            set
            {
                _EnableImageAnimatedBackground = value;
                OnPropertyChanged();
            }
        }


        private bool _EnableCoverImage { get; set; } = true;
        public bool EnableCoverImage
        {
            get => _EnableCoverImage;
            set
            {
                _EnableCoverImage = value;
                OnPropertyChanged();
            }
        }

        public bool EnableCoverImageRandomSelect { get; set; } = false;
        public bool EnableCoverImageAutoChanger { get; set; } = false;
        public int CoverImageAutoChangerTimer { get; set; } = 10;

        private bool _EnableImageAnimatedCover { get; set; } = false;
        public bool EnableImageAnimatedCover
        {
            get => _EnableImageAnimatedCover;
            set
            {
                _EnableImageAnimatedCover = value;
                OnPropertyChanged();
            }
        }


        public string SteamGridDbApiKey { get; set; } = string.Empty;


        public string ffmpegFile { get; set; } = string.Empty;
        public string webpinfoFile { get; set; } = string.Empty;
        #endregion

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed
        private bool _HasData { get; set; } = false;
        [DontSerialize]
        public bool HasData
        {
            get => _HasData;
            set
            {
                _HasData = value;
                OnPropertyChanged();
            }
        }

        private bool _HasDataBackground { get; set; } = false;
        [DontSerialize]
        public bool HasDataBackground
        {
            get => _HasDataBackground;
            set
            {
                _HasDataBackground = value;
                OnPropertyChanged();
            }
        }

        private bool _HasDataCover { get; set; } = false;
        [DontSerialize]
        public bool HasDataCover
        {
            get => _HasDataCover;
            set
            {
                _HasDataCover = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }


    public class BackgroundChangerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly BackgroundChanger Plugin;
        private BackgroundChangerSettings EditingClone { get; set; }

        private BackgroundChangerSettings _Settings;
        public BackgroundChangerSettings Settings
        {
            get => _Settings;
            set
            {
                _Settings = value;
                OnPropertyChanged();
            }
        }


        public BackgroundChangerSettingsViewModel(BackgroundChanger plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<BackgroundChangerSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new BackgroundChangerSettings();
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
