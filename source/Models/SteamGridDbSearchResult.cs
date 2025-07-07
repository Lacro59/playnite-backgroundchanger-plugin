using Playnite.SDK.Data;
using System.Collections.Generic;

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