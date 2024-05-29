using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BackgroundChanger.Services;
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
        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

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


        [DontSerialize]
        public bool IsVideo => !Thumb.IsNullOrEmpty() && Thumb.Contains(".webm", StringComparison.InvariantCultureIgnoreCase);

        [DontSerialize]
        public string Thumbnail => IsVideo ? Url : Thumb;

        [DontSerialize]
        public bool IsVideoConverted
        {
            get
            {
                if (!IsVideo)
                {
                    return false;
                }

                if (File.Exists(PluginDatabase.PluginSettings.Settings.ffmpegFile))
                {
                    string VideoFile = Path.Combine(PluginDatabase.Paths.PluginCachePath, $"{Id}.mp4");
                    if (File.Exists(VideoFile))
                    {
                        this.VideoFile = VideoFile;
                        return true;
                    }

                    _ = Task.Run(() =>
                    {
                        string ffmpeg = $"-i {Thumb} {VideoFile}";

                        Process process = new Process();
                        process.StartInfo.FileName = PluginDatabase.PluginSettings.Settings.ffmpegFile;
                        process.StartInfo.Arguments = ffmpeg;
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        _ = process.Start();
                        process.WaitForExit();

                        return true;
                    });
                }

                return false;
            }
        }

        private string videoFile = string.Empty;
        [DontSerialize]
        public string VideoFile { get => videoFile; set => SetValue(ref videoFile, value); }
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
