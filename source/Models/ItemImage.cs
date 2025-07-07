using BackgroundChanger.Services;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using Playnite.SDK.Data;
using System;
using System.IO;

namespace BackgroundChanger.Models
{
    /// <summary>
    /// Represents an image or video item used for background customization.
    /// Provides metadata such as file size, resolution, and type (video, convertable).
    /// </summary>
    public class ItemImage
    {
        /// <summary>
        /// Gets the instance of the plugin's database.
        /// </summary>
        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        /// <summary>
        /// Gets or sets the file name of the image.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the folder name containing the image.
        /// </summary>
        public string FolderName { get; set; }

        /// <summary>
        /// Indicates whether this image is the default background.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Indicates whether this image is a cover.
        /// </summary>
        public bool IsCover { get; set; }

        /// <summary>
        /// Indicates whether this image is marked as favorite.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Checks if the image or video file exists on disk.
        /// </summary>
        [DontSerialize]
        public bool Exist => File.Exists(FullPath);

        /// <summary>
        /// Gets the resolution of the image in the format "width x height".
        /// Returns an empty string for videos or if the file doesn't exist.
        /// </summary>
        [DontSerialize]
        public string ImageSize
        {
            get
            {
                if (File.Exists(FullPath))
                {
                    if (Path.GetExtension(FullPath).IsEqual(".mp4"))
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

        /// <summary>
        /// Gets the human-readable file size (e.g., KB, MB).
        /// Returns an empty string if the file doesn't exist.
        /// </summary>
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

        /// <summary>
        /// Gets the full absolute path to the image or video file.
        /// </summary>
        [DontSerialize]
        public string FullPath => FolderName.IsNullOrEmpty()
            ? Name
            : Path.Combine(
                PluginDatabase.Paths.PluginUserDataPath,
                "Images",
                FolderName,
                Name
            );

        /// <summary>
        /// Indicates whether the item is a video (determined by .mp4 extension).
        /// </summary>
        [DontSerialize]
        public bool IsVideo => !FullPath.IsNullOrEmpty() && Path.GetExtension(FullPath).IsEqual(".mp4");

        /// <summary>
        /// Indicates whether the image is in WebP format and thus eligible for conversion.
        /// </summary>
        [DontSerialize]
        public bool IsConvertable => !FullPath.IsNullOrEmpty() && Path.GetExtension(FullPath).IsEqual(".webp");
    }
}