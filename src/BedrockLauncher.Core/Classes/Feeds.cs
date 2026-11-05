using System;
using System.Collections.Generic;

namespace BedrockLauncher.Classes.Launcher
{
    // launchercontent.mojang.com/v2/bedrockPatchNotes.json
    public class PatchNotes_Game_Item
    {
        public string fallback_image => isBeta ? Constants.PATCHNOTE_BETA_IMG : Constants.PATCHNOTE_RELEASE_IMG;
        public bool isBeta => patchNoteType == "preview" || patchNoteType == "beta";
        public string image_url => image?.url == null ? null : Constants.PATCHNOTES_IMGPREFIX_URL + image.url;
        public string title { get; set; }
        public string version { get; set; }
        public string patchNoteType { get; set; }
        public string date { get; set; }
        public PatchNotes_Game_Image image { get; set; }
        public string body { get; set; }
        public string id { get; set; }
        public string contentPath { get; set; }
    }

    public class PatchNotes_Game_Image
    {
        public string url { get; set; }
        public string title { get; set; }
    }

    public class PatchNotes_Game_Root
    {
        public int version { get; set; }
        public List<PatchNotes_Game_Item> entries { get; set; }
    }

    // launchercontent.mojang.com/v2/news.json
    public class News_OfficialFeed
    {
        public int version { get; set; }
        public List<News_OfficialItem> entries { get; set; }
    }

    public class News_OfficialItem
    {
        public class Dimensions
        {
            public double width { get; set; }
            public double height { get; set; }
        }

        public class ImageDefinitions
        {
            public string title { get; set; }
            public string url { get; set; }
            public Dimensions dimensions { get; set; }
        }

        public string title { get; set; }
        public string tag { get; set; }
        public string category { get; set; }
        public DateTime date { get; set; }
        public string text { get; set; }
        public ImageDefinitions playPageImage { get; set; }
        public ImageDefinitions newsPageImage { get; set; }
        public string readMoreLink { get; set; }
        public bool cardBorder { get; set; }
        public List<string> newsType { get; set; }

        public string ImageUrl => newsPageImage?.url == null ? null : Constants.RSS_LAUNCHER_IMG_PATH + newsPageImage.url;
        public string Title => title;
        public string Link => readMoreLink;
        public string Tag => category;
        public string Date => date.ToString("D");
    }
}
