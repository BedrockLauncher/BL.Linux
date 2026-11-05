using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace BedrockLauncher.UI
{
    /// <summary>Image that loads a remote URL in the background, showing <see cref="Fallback"/> until (or if) it arrives.</summary>
    public class WebImage : Image
    {
        public static readonly StyledProperty<string> UrlProperty = AvaloniaProperty.Register<WebImage, string>(nameof(Url));
        public static readonly StyledProperty<string> FallbackProperty = AvaloniaProperty.Register<WebImage, string>(nameof(Fallback));

        public string Url { get => GetValue(UrlProperty); set => SetValue(UrlProperty, value); }
        public string Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }

        protected override Type StyleKeyOverride => typeof(Image);

        protected override async void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == FallbackProperty && Source == null)
                Source = ImagePathConverter.Load(Fallback);
            if (change.Property == UrlProperty)
            {
                string url = Url;
                Bitmap bitmap = await ImagePathConverter.LoadAsync(url);
                if (url == Url && bitmap != null) Source = bitmap;
                else if (Source == null) Source = ImagePathConverter.Load(Fallback);
            }
        }
    }
}
