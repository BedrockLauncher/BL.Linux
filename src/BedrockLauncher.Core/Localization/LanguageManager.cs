using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace BedrockLauncher.Localization.Language
{
    public class LanguageDefinition
    {
        public string Locale { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsExternal { get; set; }

        public override string ToString() => Name ?? Locale;
    }

    /// <summary>
    /// The upstream launcher keeps its strings in WPF resource dictionaries (lang.&lt;locale&gt;.xaml, maintained on
    /// Crowdin). The same files are read here as plain key/value tables so they stay shareable with upstream; the UI
    /// layer copies the active table into its own resources whenever <see cref="LanguageChanged"/> fires.
    /// </summary>
    public static class LanguageManager
    {
        public const string DefaultLocale = "en-US";

        private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        private static Dictionary<string, string> fallback;
        private static Dictionary<string, string> current = new Dictionary<string, string>();

        public static string CurrentLocale { get; private set; } = DefaultLocale;

        /// <summary>Raised after the active table changed; the argument is the merged (fallback + language) table.</summary>
        public static event Action<IReadOnlyDictionary<string, string>> LanguageChanged;

        public static IReadOnlyDictionary<string, string> Strings => current;

        /// <summary>Locales offered in the settings (same list as upstream's AvailableLanguages).</summary>
        public static readonly string[] AvailableLocales =
        {
            "en-US", "en-UD", "es-ES", "fr-FR", "fi-FI", "ms-MY", "pl-PL", "pt-BR", "pt-PT", "ru-RU", "tr-TR", "uk-UA", "zh-CN", "zh-TW", "it-IT", "de-DE"
        };

        public static void Init(string locale = null) => SetLanguage(string.IsNullOrWhiteSpace(locale) ? DefaultLocale : locale);

        public static List<LanguageDefinition> GetLanguages(string externalFolder = null)
        {
            var result = new List<LanguageDefinition>();
            foreach (string locale in AvailableLocales)
            {
                var table = LoadInternal(locale);
                if (table == null) continue;
                result.Add(new LanguageDefinition { Locale = locale, Name = table.TryGetValue("LanguageString", out var n) ? n : locale });
            }

            if (!string.IsNullOrEmpty(externalFolder) && Directory.Exists(externalFolder))
            {
                foreach (string file in Directory.EnumerateFiles(externalFolder, "lang.*.xaml"))
                {
                    var table = TryParse(File.ReadAllText(file));
                    if (table == null || table.Count == 0) continue;
                    string locale = System.IO.Path.GetFileNameWithoutExtension(file).Substring(5);
                    result.Add(new LanguageDefinition
                    {
                        Locale = locale,
                        Path = file,
                        IsExternal = true,
                        Name = table.TryGetValue("LanguageString", out var n) ? n : locale
                    });
                }
            }
            return result;
        }

        public static void SetLanguage(string locale, string externalPath = null)
        {
            fallback ??= LoadInternal(DefaultLocale) ?? new Dictionary<string, string>();

            Dictionary<string, string> table = null;
            if (!string.IsNullOrEmpty(externalPath) && File.Exists(externalPath))
                table = TryParse(File.ReadAllText(externalPath));
            table ??= LoadInternal(locale);
            if (table == null)
            {
                Trace.WriteLine($"Language {locale} not found; using {DefaultLocale}.");
                locale = DefaultLocale;
                table = fallback;
            }

            var merged = new Dictionary<string, string>(fallback);
            foreach (var pair in LinuxStrings.Tables[DefaultLocale]) merged[pair.Key] = pair.Value;
            foreach (var pair in table) merged[pair.Key] = pair.Value;
            if (LinuxStrings.Tables.TryGetValue(locale, out var linux))
                foreach (var pair in linux) merged[pair.Key] = pair.Value;

            current = merged;
            CurrentLocale = locale;
            LanguageChanged?.Invoke(current);
        }

        public static string GetResource(string key)
        {
            if (current.TryGetValue(key, out string value)) return value;
            fallback ??= LoadInternal(DefaultLocale) ?? new Dictionary<string, string>();
            if (fallback.TryGetValue(key, out value)) return value;
            return LinuxStrings.Tables[DefaultLocale].TryGetValue(key, out value) ? value : key;
        }

        public static bool TryGetResource(string key, out string value)
        {
            if (current.TryGetValue(key, out value)) return true;
            value = string.Empty;
            return false;
        }

        private static Dictionary<string, string> LoadInternal(string locale)
        {
            Assembly assembly = typeof(LanguageManager).Assembly;
            using Stream stream = assembly.GetManifestResourceStream($"lang.lang.{locale}.xaml");
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            return TryParse(reader.ReadToEnd());
        }

        internal static Dictionary<string, string> TryParse(string xaml)
        {
            try
            {
                XDocument doc = XDocument.Parse(xaml);
                var table = new Dictionary<string, string>();
                foreach (XElement element in doc.Root.Descendants())
                {
                    string key = (string)element.Attribute(XamlNs + "Key");
                    if (key == null) continue;
                    table[key] = element.Value;
                }
                return table;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Could not parse language file: {ex.Message}");
                return null;
            }
        }
    }
}
