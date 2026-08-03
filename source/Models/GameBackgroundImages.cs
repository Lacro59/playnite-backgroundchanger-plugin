using CommonPluginsShared.Collections;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BackgroundChanger.Models
{
    public class GameBackgroundImages : PluginGameCollection<ItemImage>
    {
        /// <summary>
        /// Invalidates cached <see cref="PluginGameEntry.HasData"/> after in-memory item list mutations
        /// (e.g. ephemeral Playnite default mirrors from <see cref="BackgroundChangerDatabase.Get"/>).
        /// </summary>
        public void InvalidateItemCache()
        {
            RefreshCachedValues();
        }

        [DontSerialize]
        public bool HasDataBackground => Items?.Where(x => x.IsBackgroundMedia && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsBackground => Items?.Where(x => x.IsBackgroundMedia && x.Exist)?.ToList() ?? new List<ItemImage>();


        [DontSerialize]
        public bool HasDataCover => Items?.Where(x => x.IsCover && !x.IsIcon && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsCover => Items?.Where(x => x.IsCover && !x.IsIcon && x.Exist)?.ToList() ?? new List<ItemImage>();


        [DontSerialize]
        public bool HasDataIcon => Items?.Where(x => x.IsIcon && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsIcon => Items?.Where(x => x.IsIcon && x.Exist)?.ToList() ?? new List<ItemImage>();


        private ItemImage backgroundImageOnStart;
        [DontSerialize]
        public ItemImage BackgroundImageOnStart
        {
            get
            {
                if (backgroundImageOnStart == null)
                {
                    List<ItemImage> items = ItemsBackground.Where(x => !x.IsVideo).ToList();
                    if (items.Count == 0)
                    {
                        items = ItemsBackground;
                    }

                    if (items.Count == 0)
                    {
                        return null;
                    }

                    Random rnd = new Random();
                    int counter = rnd.Next(0, items.Count);
                    backgroundImageOnStart = items[counter];
                }
                return backgroundImageOnStart;
            }
        }

        private ItemImage coverImageOnStart;
        [DontSerialize]
        public ItemImage CoverImageOnStart
        {
            get
            {
                if (coverImageOnStart == null)
                {
                    List<ItemImage> items = ItemsCover.Where(x => !x.IsVideo).ToList();
                    if (items.Count == 0)
                    {
                        items = ItemsCover;
                    }

                    if (items.Count == 0)
                    {
                        return null;
                    }

                    Random rnd = new Random();
                    int counter = rnd.Next(0, items.Count);
                    coverImageOnStart = items[counter];
                }
                return coverImageOnStart;
            }
        }

        private ItemImage iconImageOnStart;
        [DontSerialize]
        public ItemImage IconImageOnStart
        {
            get
            {
                if (iconImageOnStart == null)
                {
                    List<ItemImage> items = ItemsIcon.Where(x => !x.IsVideo).ToList();
                    if (items.Count == 0)
                    {
                        items = ItemsIcon;
                    }

                    if (items.Count == 0)
                    {
                        return null;
                    }

                    Random rnd = new Random();
                    int counter = rnd.Next(0, items.Count);
                    iconImageOnStart = items[counter];
                }
                return iconImageOnStart;
            }
        }
    }
}
