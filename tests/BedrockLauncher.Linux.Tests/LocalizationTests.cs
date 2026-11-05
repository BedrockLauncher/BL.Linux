using BedrockLauncher.Localization.Language;
using Xunit;

namespace BedrockLauncher.Linux.Tests
{
    public class LocalizationTests
    {
        [Fact]
        public void Upstream_language_files_load_with_fallback_and_linux_strings()
        {
            LanguageManager.SetLanguage("it-IT");
            Assert.Equal("it-IT", LanguageManager.CurrentLocale);
            Assert.NotEqual("ToolbarButtons_NewsButton_Text", LanguageManager.GetResource("ToolbarButtons_NewsButton_Text"));
            Assert.StartsWith("Minecraft {0} è una build GDK", LanguageManager.GetResource("Linux_GdkFilesRequired"));

            LanguageManager.SetLanguage("en-US");
            Assert.Equal("News", LanguageManager.GetResource("ToolbarButtons_NewsButton_Text"));
            Assert.Contains("GDK", LanguageManager.GetResource("Linux_GdkRuntimeMissing"));
            Assert.True(LanguageManager.GetLanguages().Count >= 14);
        }

        [Fact]
        public void Unknown_language_falls_back_to_english()
        {
            LanguageManager.SetLanguage("xx-XX");
            Assert.Equal("en-US", LanguageManager.CurrentLocale);
            Assert.Equal("News", LanguageManager.GetResource("ToolbarButtons_NewsButton_Text"));
        }
    }
}
