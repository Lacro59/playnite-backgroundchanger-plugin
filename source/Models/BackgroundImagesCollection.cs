using CommonPluginsShared.Collections;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Models
{
    public class BackgroundImagesCollection : PluginItemCollection<GameBackgroundImages>
    {
        public BackgroundImagesCollection(string path, GameDatabaseCollection type = GameDatabaseCollection.Uknown) : base(path, type)
        {
        }
    }
}
