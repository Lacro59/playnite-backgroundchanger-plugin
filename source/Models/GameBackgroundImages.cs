using CommonPluginsShared.Collections;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.Linq;

namespace BackgroundChanger.Models
{
    public class GameBackgroundImages : PluginDataBaseGame<ItemImage>
    {
        [DontSerialize]
        public bool HasDataBackground => Items?.Where(x => !x.IsCover && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsBackground => Items?.Where(x => !x.IsCover && x.Exist)?.ToList();


        [DontSerialize]
        public bool HasDataCover => Items?.Where(x => x.IsCover && x.Exist)?.Count() > 0;

        [DontSerialize]
        public List<ItemImage> ItemsCover => Items?.Where(x => x.IsCover && x.Exist)?.ToList();
    }
}