using CommonPluginsPlaynite.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Models
{
    public class BackgroundImage
    {
        public string Name { get; set; }
        public string FolderName { get; set; }
        public bool IsDefault { get; set; }

        [JsonIgnore]
        public string ImageSize {
            get
            {
                if (File.Exists(FullPath))
                {
                    ImageProperties imageProperties = Images.GetImageProperties(FullPath);
                    return imageProperties.Width + "x" + imageProperties.Height;
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        [JsonIgnore]
        public string FullPath {
            get
            {
                if (FolderName.IsNullOrEmpty())
                {
                    return Name;
                }
                else
                {
                    return Path.Combine(
                        BackgroundChanger.PluginDatabase.PluginUserDataPath,
                        "Images",
                        FolderName,
                        Name
                    );
                }
            }
        }
    }
}
