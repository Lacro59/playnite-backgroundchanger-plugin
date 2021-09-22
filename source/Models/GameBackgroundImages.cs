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
        private List<ItemImage> _Items = new List<ItemImage>();
        public override List<ItemImage> Items
        {
            get
            {
                return _Items;
            }

            set
            {
                _Items = value;
                OnPropertyChanged();
            }
        }


        [DontSerialize]
        public bool HasDataBackground
        {
            get
            {
                return Items.Where(x => !x.IsCover).Count() > 0;
            }
        }

        [DontSerialize]
        public List<ItemImage> ItemsBackground
        {
            get
            {
                return Items.Where(x => !x.IsCover).ToList();
            }
        }


        [DontSerialize]
        public bool HasDataCover
        {
            get
            {
                return Items.Where(x => x.IsCover).Count() > 0;
            }
        }

        [DontSerialize]
        public List<ItemImage> ItemsCover
        {
            get
            {
                return Items.Where(x => x.IsCover).ToList();
            }
        }
    }
}
