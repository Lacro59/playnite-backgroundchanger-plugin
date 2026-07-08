using CommonPluginsShared.Collections;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BackgroundChanger.Models
{
    public class GameBackgroundImages : PluginGameCollection<ItemImage>
    {
        [DontSerialize]
        public bool HasDataBackground => Items?.Where(x => !x.IsCover && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsBackground => Items?.Where(x => !x.IsCover && x.Exist)?.ToList() ?? new List<ItemImage>();


        [DontSerialize]
        public bool HasDataCover => Items?.Where(x => x.IsCover && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsCover => Items?.Where(x => x.IsCover && x.Exist)?.ToList() ?? new List<ItemImage>();


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
    }
}