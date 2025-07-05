using CommonPluginsShared.Collections;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BackgroundChanger.Models
{
    public class GameBackgroundImages : PluginDataBaseGame<ItemImage>
    {
        [DontSerialize]
        public bool HasDataBackground => Items?.Where(x => !x.IsCover && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsBackground => Items?.Where(x => !x.IsCover && x.Exist)?.ToList() ?? new List<ItemImage>();


        [DontSerialize]
        public bool HasDataCover => Items?.Where(x => x.IsCover && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsCover => Items?.Where(x => x.IsCover && x.Exist)?.ToList() ?? new List<ItemImage>();


        private ItemImage _backgroundImageOnStart;
        [DontSerialize]
        public ItemImage BackgroundImageOnStart
        {
            get
            {
                if (_backgroundImageOnStart == null)
                {
                    if (ItemsBackground.Count == 0) return null;
                    Random rnd = new Random();
                    int counter = rnd.Next(0, ItemsBackground.Count);
                    _backgroundImageOnStart = ItemsBackground[counter];
                }
                return _backgroundImageOnStart;
            }
        }

        private ItemImage _coverImageOnStart;
        [DontSerialize]
        public ItemImage CoverImageOnStart
        {
            get
            {
                if (_coverImageOnStart == null)
                {
                    if (ItemsCover.Count == 0) return null;
                    Random rnd = new Random();
                    int counter = rnd.Next(0, ItemsCover.Count);
                    _coverImageOnStart = ItemsCover[counter];
                }
                return _coverImageOnStart;
            }
        }
    }
}