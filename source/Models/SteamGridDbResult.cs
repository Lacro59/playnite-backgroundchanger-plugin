using System;
using System.Collections.Generic;
using Playnite.SDK.Data;

namespace BackgroundChanger.Models
{
    public class SteamGridDbResultData
    {
        [SerializationPropertyName("success")]
        public bool Success { get; set; }

        [SerializationPropertyName("data")]
        public List<SteamGridDbResult> Data { get; set; }
    }


    public class SteamGridDbResult : ObservableObject
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("score")]
        public int Score { get; set; }

        [SerializationPropertyName("style")]
        public string Style { get; set; }

        [SerializationPropertyName("width")]
        public int Width { get; set; }

        [SerializationPropertyName("height")]
        public int Height { get; set; }

        [SerializationPropertyName("nsfw")]
        public bool Nsfw { get; set; }

        [SerializationPropertyName("humor")]
        public bool Humor { get; set; }

        [SerializationPropertyName("notes")]
        public string Notes { get; set; }

        [SerializationPropertyName("mime")]
        public string Mime { get; set; }

        [SerializationPropertyName("language")]
        public string Language { get; set; }

        [SerializationPropertyName("url")]
        public string Url { get; set; }

        [SerializationPropertyName("thumb")]
        public string Thumb { get; set; }

        [SerializationPropertyName("lock")]
        public bool IsLock { get; set; }

        [SerializationPropertyName("epilepsy")]
        public bool Epilepsy { get; set; }

        [SerializationPropertyName("upvotes")]
        public int Upvotes { get; set; }

        [SerializationPropertyName("downvotes")]
        public int Downvotes { get; set; }

        [SerializationPropertyName("author")]
        public Author Author { get; set; }

        [DontSerialize]
        public bool Untagged => !Nsfw && !Humor && !Epilepsy;

        /// <summary>
        /// SteamGridDB animated heroes are often served as WebM; conversion to MP4 happens at import via <see cref="Services.MediaConversionService"/>.
        /// </summary>
        [DontSerialize]
        public bool IsVideo
        {
            get
            {
                if (!Mime.IsNullOrEmpty() && Mime.StartsWith("video/", StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }

                return !Thumb.IsNullOrEmpty() && Thumb.Contains(".webm", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Animated SteamGridDB assets: animated WebP grids or WebM heroes.
        /// </summary>
        [DontSerialize]
        public bool IsAnimated => Mime == "image/webp" || IsVideo;

        [DontSerialize]
        public string Thumbnail => IsVideo ? Url : Thumb;
    }


    public class Author
    {
        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("steam64")]
        public string Steam64 { get; set; }

        [SerializationPropertyName("avatar")]
        public string Avatar { get; set; }
    }
}
