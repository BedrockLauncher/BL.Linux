using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace BedrockLauncher.UI
{
    /// <summary>
    /// Loads an image from a path the core hands out: avares:// resources (matched case-insensitively, since the
    /// upstream icon list uses lower-case names that Windows resolved without caring about case), local files and
    /// http(s) URLs (news images, loaded in the background).
    /// </summary>
    public sealed class ImagePathConverter : IValueConverter
    {
        public static readonly ImagePathConverter Instance = new();

        private static readonly ConcurrentDictionary<string, Bitmap> cache = new();
        private static Dictionary<string, Uri> assetIndex;
        private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Load(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

        public static Bitmap Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (cache.TryGetValue(path, out Bitmap cached)) return cached;

            try
            {
                Bitmap bitmap = null;
                if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                {
                    Uri uri = ResolveAsset(path);
                    if (uri != null)
                        using (Stream stream = AssetLoader.Open(uri)) bitmap = new Bitmap(stream);
                }
                else if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return null; // see LoadAsync
                }
                else if (File.Exists(path))
                {
                    using FileStream stream = File.OpenRead(path);
                    bitmap = new Bitmap(stream);
                }

                if (bitmap != null) cache[path] = bitmap;
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Image {path}: {ex.Message}");
                return null;
            }
        }

        public static async System.Threading.Tasks.Task<Bitmap> LoadAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return Load(path);
            if (cache.TryGetValue(path, out Bitmap cached)) return cached;
            try
            {
                byte[] data = await http.GetByteArrayAsync(path);
                var bitmap = new Bitmap(new MemoryStream(data));
                cache[path] = bitmap;
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Image {path}: {ex.Message}");
                return null;
            }
        }

        private static Uri ResolveAsset(string path)
        {
            var uri = new Uri(path);
            if (AssetLoader.Exists(uri)) return uri;
            assetIndex ??= AssetLoader.GetAssets(new Uri("avares://BedrockLauncher/Resources/"), null)
                .GroupBy(x => x.AbsolutePath.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First());
            return assetIndex.TryGetValue(uri.AbsolutePath.ToLowerInvariant(), out Uri found) ? found : null;
        }
    }

    public static class Conv
    {
        public static readonly IValueConverter Image = ImagePathConverter.Instance;

        /// <summary>true -> 0.25 opacity (upstream DisabledStyle).</summary>
        public static readonly IValueConverter NotEmpty = new FuncValueConverter<object, bool>(x => x switch
        {
            null => false,
            string s => s.Length > 0,
            int i => i > 0,
            _ => true
        });

        public static readonly IValueConverter IsZero = new FuncValueConverter<int, bool>(x => x == 0);

        public static readonly IValueConverter ToUpper = new FuncValueConverter<string, string>(x => x?.ToUpperInvariant());
    }
}
