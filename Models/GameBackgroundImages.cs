using CommonPluginsShared.Collections;
using Newtonsoft.Json;
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


        [JsonIgnore]
        public bool HasDataBackground
        {
            get
            {
                return Items.Where(x => !x.IsCover).Count() > 0;
            }
        }

        [JsonIgnore]
        public List<ItemImage> ItemsBackground
        {
            get
            {
                return Items.Where(x => !x.IsCover).ToList().GetClone();
            }
        }


        [JsonIgnore]
        public bool HasDataCover
        {
            get
            {
                return Items.Where(x => x.IsCover).Count() > 0;
            }
        }

        [JsonIgnore]
        public List<ItemImage> ItemsCover
        {
            get
            {
                return Items.Where(x => x.IsCover).ToList().GetClone();
            }
        }
    }
}
