using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Models
{
    public class SteamGridDbSearchResultData
    {
        [SerializationPropertyName("success")]
        public bool Success { get; set; }
        [SerializationPropertyName("data")]
        public List<SteamGridDbSearchResult> Data { get; set; }
    }

    public class SteamGridDbSearchResult
    {
        [SerializationPropertyName("name")]
        public string Name { get; set; }
        [SerializationPropertyName("release_date")]
        public long ReleaseDate { get; set; }
        [SerializationPropertyName("verified")]
        public bool Verified { get; set; }
        [SerializationPropertyName("id")]
        public int Id { get; set; }
        [SerializationPropertyName("types")]
        public List<string> Types { get; set; }
    }
}
