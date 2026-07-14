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

        /// <summary>
        /// Gets whether a preview URL is available for list display.
        /// </summary>
        [DontSerialize]
        public bool HasDisplayableThumbnail => !Thumbnail.IsNullOrEmpty();

        private const double ThumbnailPreviewMaxWidth = 480;
        private const double ThumbnailPreviewMaxHeight = 140;

        /// <summary>
        /// Gets the preview width for list thumbnails, derived from asset dimensions.
        /// </summary>
        [DontSerialize]
        public double ThumbnailPreviewWidth
        {
            get
            {
                GetThumbnailPreviewSize(Width, Height, out double previewWidth, out double previewHeight);
                return previewWidth;
            }
        }

        /// <summary>
        /// Gets the preview height for list thumbnails, derived from asset dimensions.
        /// </summary>
        [DontSerialize]
        public double ThumbnailPreviewHeight
        {
            get
            {
                GetThumbnailPreviewSize(Width, Height, out double previewWidth, out double previewHeight);
                return previewHeight;
            }
        }

        private static void GetThumbnailPreviewSize(int width, int height, out double previewWidth, out double previewHeight)
        {
            if (width <= 0 || height <= 0)
            {
                previewWidth = 93;
                previewHeight = ThumbnailPreviewMaxHeight;
                return;
            }

            double aspect = (double)width / height;
            double boxAspect = ThumbnailPreviewMaxWidth / ThumbnailPreviewMaxHeight;

            if (aspect >= boxAspect)
            {
                previewWidth = ThumbnailPreviewMaxWidth;
                previewHeight = ThumbnailPreviewMaxWidth / aspect;
            }
            else
            {
                previewHeight = ThumbnailPreviewMaxHeight;
                previewWidth = ThumbnailPreviewMaxHeight * aspect;
            }
        }

        /// <summary>
        /// Preview URL for the SteamGridDB list. Prefers API <see cref="Thumb"/>, then a static image URL derived from <see cref="Url"/>.
        /// Animated WebP uses the first frame; video assets use <c>hero_thumb</c> when available.
        /// </summary>
        [DontSerialize]
        public string Thumbnail => ResolveThumbnailUrl();

        private string ResolveThumbnailUrl()
        {
            string fromThumb = GetDisplayableUrl(Thumb);
            if (!fromThumb.IsNullOrEmpty())
            {
                return fromThumb;
            }

            string fromUrl = GetDisplayableUrl(Url);
            if (!fromUrl.IsNullOrEmpty())
            {
                return fromUrl;
            }

            return DeriveStaticThumbUrl(Url) ?? DeriveStaticThumbUrl(Thumb);
        }

        private static string GetDisplayableUrl(string url)
        {
            if (url.IsNullOrEmpty() || IsNonDisplayableMediaUrl(url))
            {
                return null;
            }

            return url;
        }

        private static string DeriveStaticThumbUrl(string url)
        {
            if (url.IsNullOrEmpty())
            {
                return null;
            }

            if (url.IndexOf("/hero/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }

            int lastSlash = url.LastIndexOf('/');
            if (lastSlash < 0 || lastSlash >= url.Length - 1)
            {
                return null;
            }

            string fileName = url.Substring(lastSlash + 1);
            int dotIndex = fileName.LastIndexOf('.');
            if (dotIndex > 0)
            {
                fileName = fileName.Substring(0, dotIndex) + ".jpg";
            }
            else
            {
                fileName = fileName + ".jpg";
            }

            return url.Substring(0, url.IndexOf("/hero/", StringComparison.OrdinalIgnoreCase))
                + "/hero_thumb/"
                + fileName;
        }

        private static bool IsNonDisplayableMediaUrl(string url)
        {
            return url.Contains(".webm", StringComparison.OrdinalIgnoreCase)
                || url.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
                || url.Contains(".mov", StringComparison.OrdinalIgnoreCase);
        }
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
