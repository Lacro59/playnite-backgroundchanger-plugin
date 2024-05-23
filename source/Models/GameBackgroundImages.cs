using CommonPluginsShared.Collections;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Models
{
    public class GameBackgroundImages : PluginDataBaseGame<ItemImage>
    {
        private List<ItemImage> items = new List<ItemImage>();
        public override List<ItemImage> Items { get => items; set => SetValue(ref items, value); }


        [DontSerialize]
        public bool HasDataBackground => Items?.Where(x => !x.IsCover)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsBackground => Items?.Where(x => !x.IsCover)?.ToList();


        [DontSerialize]
        public bool HasDataCover => Items?.Where(x => x.IsCover)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsCover => Items?.Where(x => x.IsCover)?.ToList();
    }
}
