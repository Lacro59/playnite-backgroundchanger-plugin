using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Models
{
    public class SteamGridDbSearchResultData
    {
        public bool success { get; set; }
        public List<SteamGridDbSearchResult> data { get; set; }
    }

    public class SteamGridDbSearchResult
    {
        public string name { get; set; }
        public long release_date { get; set; }
        public bool verified { get; set; }
        public int id { get; set; }
        public List<string> types { get; set; }
    }
}
