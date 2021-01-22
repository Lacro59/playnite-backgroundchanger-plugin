using CommonPluginsShared.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Models
{
    public class GameBackgroundImages : PluginDataBaseGame<BackgroundImage>
    {
        private List<BackgroundImage> _Items = new List<BackgroundImage>();
        public override List<BackgroundImage> Items
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
    }
}
