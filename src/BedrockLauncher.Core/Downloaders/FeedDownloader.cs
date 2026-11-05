using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BedrockLauncher.Classes.Launcher;
using Newtonsoft.Json;

namespace BedrockLauncher.Downloaders
{
    public static class SharedHttpClient
    {
        public static readonly HttpClient Instance = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BedrockLauncher-Linux/0.1");
            return client;
        }
    }

    /// <summary>Port of upstream ChangelogDownloader: the game's patch notes, filtered by release / beta.</summary>
    public class ChangelogDownloader : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            UiThread.Run(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));

        public bool ShowBetas { get; set; } = true;
        public bool ShowReleases { get; set; } = true;
        public bool IsRefreshable { get; set; } = true;
        public ObservableCollection<PatchNotes_Game_Item> PatchNotes { get; } = new();

        private List<PatchNotes_Game_Item> all = new();

        public async Task UpdateList()
        {
            if (!IsRefreshable) return;
            IsRefreshable = false;
            try
            {
                string json = await SharedHttpClient.Instance.GetStringAsync(Constants.PATCHNOTES_MAIN_URL);
                all = JsonConvert.DeserializeObject<PatchNotes_Game_Root>(json)?.entries ?? new List<PatchNotes_Game_Item>();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Patch notes: {ex.Message}");
                all = new List<PatchNotes_Game_Item>();
            }
            finally
            {
                IsRefreshable = true;
            }
            ApplyFilter();
        }

        public void ApplyFilter() => UiThread.Run(() =>
        {
            PatchNotes.Clear();
            foreach (var item in all.Where(x => x.isBeta ? ShowBetas : ShowReleases))
                PatchNotes.Add(item);
        });
    }

    public static class NewsDownloader
    {
        public static async Task<List<News_OfficialItem>> GetOfficialFeed()
        {
            try
            {
                string json = await SharedHttpClient.Instance.GetStringAsync(Constants.RSS_LAUNCHER_URL);
                var feed = JsonConvert.DeserializeObject<News_OfficialFeed>(json);
                return (feed?.entries ?? new List<News_OfficialItem>())
                    .Where(x => x.newsType != null && x.newsType.Any(t => t.Contains("News page")))
                    .ToList();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"News: {ex.Message}");
                return new List<News_OfficialItem>();
            }
        }
    }
}

namespace BedrockLauncher.Downloaders
{
    public class RssItem
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Date { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
    }

    /// <summary>Minimal RSS 2.0 reader for the forum feed (upstream uses CodeHollow.FeedReader).</summary>
    public static class RssDownloader
    {
        public static async System.Threading.Tasks.Task<System.Collections.Generic.List<RssItem>> GetFeed(string url)
        {
            var result = new System.Collections.Generic.List<RssItem>();
            try
            {
                string xml = await SharedHttpClient.Instance.GetStringAsync(url);
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                foreach (var item in doc.Descendants().Where(x => x.Name.LocalName == "item"))
                {
                    string Get(string name) => item.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value;
                    string image = item.Elements().FirstOrDefault(x => x.Name.LocalName is "enclosure" or "content" or "thumbnail")?.Attribute("url")?.Value;
                    string description = System.Text.RegularExpressions.Regex.Replace(Get("description") ?? string.Empty, "<.*?>", string.Empty).Trim();
                    DateTime.TryParse(Get("pubDate"), out DateTime date);
                    result.Add(new RssItem
                    {
                        Title = Get("title"),
                        Link = Get("link"),
                        Date = date == default ? string.Empty : date.ToString("D"),
                        Description = description.Length > 240 ? description.Substring(0, 240) + "…" : description,
                        ImageUrl = image
                    });
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"RSS {url}: {ex.Message}");
            }
            return result;
        }
    }
}
