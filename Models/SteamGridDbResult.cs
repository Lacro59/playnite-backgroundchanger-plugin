using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK.Data;

namespace BackgroundChanger.Models
{
    public class SteamGridDbResultData
    {
        public bool success { get; set; }
        public List<SteamGridDbResult> data { get; set; }
    }

    public class SteamGridDbResult
    {
        public int id { get; set; }
        public int score { get; set; }
        public string style { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public bool nsfw { get; set; }
        public bool humor { get; set; }
        public string notes { get; set; }
        public string mime { get; set; }
        public string language { get; set; }
        public string url { get; set; }
        public string thumb { get; set; }
        [SerializationPropertyName("lock")]
        public bool isLock { get; set; }
        public bool epilepsy { get; set; }
        public int upvotes { get; set; }
        public int downvotes { get; set; }
        public Author author { get; set; }
    }

    public class Author
    {
        public string name { get; set; }
        public string steam64 { get; set; }
        public string avatar { get; set; }
    }
}
