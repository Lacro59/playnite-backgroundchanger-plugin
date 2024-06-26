﻿using BackgroundChanger.Services;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using Playnite.SDK.Data;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace BackgroundChanger.Models
{
    public class ItemImage
    {
        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;


        public string Name { get; set; }
        public string FolderName { get; set; }
        public bool IsDefault { get; set; }
        public bool IsCover { get; set; }
        public bool IsFavorite { get; set; }

        [DontSerialize]
        public bool Exist => File.Exists(FullPath);

        [DontSerialize]
        public string ImageSize
        {
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
        public string FullPath => FolderName.IsNullOrEmpty()
                    ? Name
                    : Path.Combine(
                        PluginDatabase.Paths.PluginUserDataPath,
                        "Images",
                        FolderName,
                        Name
                    );

        [DontSerialize]
        public bool IsVideo => !FullPath.IsNullOrEmpty() && Path.GetExtension(FullPath).ToLower().Contains("mp4");

        [DontSerialize]
        public bool IsConvertable => !FullPath.IsNullOrEmpty() && Path.GetExtension(FullPath).ToLower().Contains("webp");
    }
}
