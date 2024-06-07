﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using BackgroundChanger.Services;
using BackgroundChanger.Views;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace BackgroundChanger.Models
{
    public class GoogleImage
    {
        [SerializationPropertyName("ow")]
        public uint Width { get; set; }

        [SerializationPropertyName("oh")]
        public uint Height { get; set; }

        [SerializationPropertyName("ou")]
        public string ImageUrl { get; set; }

        [SerializationPropertyName("tu")]
        public string ThumbUrl { get; set; }

        public string Size => $"{Width}x{Height}";
    }


    public class GoogleImageDownloader : IDisposable
    {
        private readonly IWebView webView;
        public GoogleImageDownloader()
        {
            webView = API.Instance.WebViews.CreateOffscreenView();
        }

        public void Dispose()
        {
            webView.Dispose();
        }

        public async Task<List<GoogleImage>> GetImages(string searchTerm, SafeSearchSettings safeSearch, bool transparent = false)
        {
            List<GoogleImage> images = new List<GoogleImage>();
            HtmlParser parser = new HtmlParser();
            string url = @"https://www.google.com/search?tbm=isch&client=firefox-b-d&source=lnt&q=" + searchTerm;

            if (safeSearch == SafeSearchSettings.On)
            {
                url += "&safe=on";
            }
            else if (safeSearch == SafeSearchSettings.Off)
            {
                url += "&safe=off";
            }

            if (transparent)
            {
                url += "&tbs=ic:trans";
            }

            webView.NavigateAndWait(url.ToString());
            if (webView.GetCurrentAddress().StartsWith(@"https://consent.google.com", StringComparison.OrdinalIgnoreCase))
            {
                // This rejects Google's consent form for cookies
                _ = await webView.EvaluateScriptAsync(@"document.getElementsByTagName('form')[0].submit();");
                await Task.Delay(3000);
                webView.NavigateAndWait(url.ToString());
            }

            string googleContent = await webView.GetPageSourceAsync();
            if (googleContent.Contains(".rg_meta", StringComparison.Ordinal))
            {
                IHtmlDocument document = parser.Parse(googleContent);
                foreach (AngleSharp.Dom.IElement imageElem in document.QuerySelectorAll(".rg_meta"))
                {
                    images.Add(Serialization.FromJson<GoogleImage>(imageElem.InnerHtml));
                }
            }
            else
            {
                googleContent = Regex.Replace(googleContent, @"\r\n?|\n", string.Empty);
                MatchCollection matches = Regex.Matches(googleContent, @"\[""(https:\/\/encrypted-[^,]+?)"",\d+,\d+\],\[""(http.+?)"",(\d+),(\d+)\]");
                foreach (Match match in matches)
                {
                    List<List<object>> data = Serialization.FromJson<List<List<object>>>($"[{match.Value}]");
                    images.Add(new GoogleImage
                    {
                        ThumbUrl = data[0][0].ToString(),
                        ImageUrl = data[1][0].ToString(),
                        Height = uint.Parse(data[1][1].ToString()),
                        Width = uint.Parse(data[1][2].ToString())
                    });
                }
            }

            return images;
        }
    }
}
