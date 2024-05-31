using BackgroundChanger.Models;
using CommonPluginsShared;
using Playnite.SDK.Data;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BackgroundChanger.Services
{
    public enum SteamGridDbType
    {
        grids, heroes
    }


    public class SteamGridDbApi
    {
        private BackgroundChangerDatabase PluginDatabase => BackgroundChanger.PluginDatabase;

        private string ApiKey { get; set; }
        private static string UrlBase => @"https://www.steamgriddb.com";
        private static string UrlSearch => UrlBase + "/api/v2/search/autocomplete/{0}";
        private static string UrlSearchGrids => UrlBase + "/api/v2/grids/game/{0}?dimensions=600x900,920x430,460x215,1024x1024,342x482&mimes=image/png,image/webp,image/jpeg&types=static,animated&styles=alternate,blurred,material,white_logo,no_logo&nsfw=any&humor=any&page={1}";
        private static string UrlSearchHeroes => UrlBase + "/api/v2/heroes/game/{0}?dimensions=1920x620,3840x1240,1600x650&mimes=image/png,image/webp,image/jpeg&types=static,animated&styles=alternate,blurred,material&nsfw=any&humor=any&page={1}";
        private static string UrlGame => UrlBase + "/game/{0}";


        public SteamGridDbApi()
        {
            ApiKey = PluginDatabase.PluginSettings.Settings.SteamGridDbApiKey;
        }


        public SteamGridDbSearchResultData SearchGame(string Name)
        {
            try
            {
                string Response = DownloadStringData(string.Format(UrlSearch, WebUtility.UrlEncode(Name))).GetAwaiter().GetResult();
                SteamGridDbSearchResultData ResultData = Serialization.FromJson<SteamGridDbSearchResultData>(Response);
                return ResultData;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return null;
        }

        public SteamGridDbResultData SearchElement(int Id, SteamGridDbType steamGridDbType, int page = 0)
        {
            try
            {
                string url = UrlSearchHeroes;
                if (steamGridDbType == SteamGridDbType.grids)
                {
                    url = UrlSearchGrids;
                }

                string response = DownloadStringData(string.Format(url, Id, page)).GetAwaiter().GetResult();
                Serialization.TryFromJson(response, out SteamGridDbResultData ResultData);
                return ResultData;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return null;
        }



        public async Task<string> DownloadStringData(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpRequestMessage request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Get
                };

                HttpResponseMessage response;
                try
                {
                    client.DefaultRequestHeaders.Add("User-Agent", Web.UserAgent);
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
                    response = await client.SendAsync(request).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Error on download {url}", true, "BackgroundChanger");
                    return string.Empty;
                }

                if (response == null)
                {
                    return string.Empty;
                }

                int statusCode = (int)response.StatusCode;

                // We want to handle redirects ourselves so that we can determine the final redirect Location (via header)
                if (statusCode >= 300 && statusCode <= 399)
                {
                    Uri redirectUri = response.Headers.Location;
                    if (!redirectUri.IsAbsoluteUri)
                    {
                        redirectUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + redirectUri);
                    }

                    Common.LogDebug(true, string.Format("DownloadStringData() redirecting to {0}", redirectUri));
                    return await DownloadStringData(redirectUri.ToString());
                }
                else
                {
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
