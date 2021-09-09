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
        public bool success { get; set; }
        public List<SteamGridDbResult> data { get; set; }
    }

    public class SteamGridDbResult : ObservableObject
    {
        private BackgroundChangerDatabase PluginDatabase = BackgroundChanger.PluginDatabase;

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

        [DontSerialize]
        public bool isVideo {
            get
            {
                if (thumb.IsNullOrEmpty())
                {
                    return false;
                }

                return thumb.Contains(".webm");
            }
        }
        [DontSerialize]
        public string thumbnail
        {
            get
            {
                if (isVideo)
                {
                    return url;
                }

                return thumb;
            }
        }
        [DontSerialize]
        public bool IsVideoConverted
        {
            get
            {
                if (!isVideo)
                {
                    return false;
                }

                if (File.Exists(PluginDatabase.PluginSettings.Settings.ffmpegFile))
                {
                    var VideoFile = Path.Combine(PluginDatabase.Paths.PluginCachePath, $"{id}.mp4");

                    if (File.Exists(VideoFile))
                    {
                        this.VideoFile = VideoFile;
                        return true;
                    }

                    Task.Run(() =>
                    {
                        var ffmpeg = $"-i {thumb} {VideoFile}";

                        var process = new Process();
                        process.StartInfo.FileName = PluginDatabase.PluginSettings.Settings.ffmpegFile;
                        process.StartInfo.Arguments = ffmpeg;
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        process.Start();
                        process.WaitForExit();

                        return true;
                    });
                }

                return false;
            }
        }

        private string _VideoFile { get; set; } = string.Empty;
        [DontSerialize]
        public string VideoFile
        {
            get => _VideoFile;
            set
            {
                _VideoFile = value;
                OnPropertyChanged();
            }
        }
    }

    public class Author
    {
        public string name { get; set; }
        public string steam64 { get; set; }
        public string avatar { get; set; }
    }
}
