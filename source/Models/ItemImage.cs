using BackgroundChanger.Services;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Models
{
    public class ItemImage
    {
        private BackgroundChangerDatabase PluginDatabase = BackgroundChanger.PluginDatabase;


        public string Name { get; set; }
        public string FolderName { get; set; }
        public bool IsDefault { get; set; }
        public bool IsCover { get; set; }
        public bool IsFavorite { get; set; }

        [DontSerialize]
        public string ImageSize {
            get
            {
                if (File.Exists(FullPath))
                {
                    if (Path.GetExtension(FullPath).ToLower().Contains("mp4"))
                    {
                        return string.Empty;
                    }
                    else
                    {
                        ImageProperties imageProperties = Images.GetImageProperties(FullPath);
                        return imageProperties.Width + "x" + imageProperties.Height;
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        [DontSerialize]
        public string ImageWeight
        {
            get
            {
                if (File.Exists(FullPath))
                {
                    FileInfo fi = new FileInfo(FullPath);
                    return Tools.SizeSuffix(fi.Length);
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        
        [DontSerialize]
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
                        PluginDatabase.Paths.PluginUserDataPath,
                        "Images",
                        FolderName,
                        Name
                    );
                }
            }
        }

        [DontSerialize]
        public bool IsVideo
        {
            get
            {
                if (FullPath.IsNullOrEmpty())
                {
                    return false;
                }

                return Path.GetExtension(FullPath).ToLower().Contains("mp4");
            }
        }

        [DontSerialize]
        public bool IsConvertable
        {
            get
            {
                if (FullPath.IsNullOrEmpty())
                {
                    return false;
                }

                return Path.GetExtension(FullPath).ToLower().Contains("webp");
            }
        }
    }
}
