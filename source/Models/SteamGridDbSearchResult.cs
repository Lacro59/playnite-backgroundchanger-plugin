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

    /// <summary>
    /// Response for <c>/games/steam/{appId}</c> and similar single-game endpoints.
    /// </summary>
    public class SteamGridDbGameResponse
    {
        [SerializationPropertyName("success")]
        public bool Success { get; set; }

        [SerializationPropertyName("data")]
        public SteamGridDbGame Data { get; set; }
    }

    /// <summary>
    /// SteamGridDB game identity payload.
    /// </summary>
    public class SteamGridDbGame
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("verified")]
        public bool Verified { get; set; }
    }
}
