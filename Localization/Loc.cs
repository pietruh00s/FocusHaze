using System.Globalization;
using System.Resources;

namespace FocusHaze.Localization;

/// <summary>Looks up UI strings from Strings.*.resx for the selected (or system) language.</summary>
internal static class Loc
{
    /// <summary>Languages with a translation; "en" is the neutral fallback in Strings.resx.</summary>
    public static readonly string[] SupportedLanguages =
        ["en", "pl", "de", "fr", "es", "it", "pt", "nl", "uk", "ru", "tr", "ja", "ko", "zh-Hans"];

    private static readonly ResourceManager Resources = new("FocusHaze.Localization.Strings", typeof(Loc).Assembly);
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;

    public static CultureInfo Culture { get; private set; } = SystemCulture;

    /// <summary>The language override; empty means "follow Windows".</summary>
    public static string Language { get; private set; } = "";

    /// <summary>Switches the UI language. Returns true when it actually changed.</summary>
    public static bool SetLanguage(string? language)
    {
        language = SupportedLanguages.Contains(language) ? language! : "";
        if (language == Language)
            return false;

        Language = language;
        Culture = language.Length == 0 ? SystemCulture : CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        return true;
    }

    public static string Get(string key) => Resources.GetString(key, Culture) ?? key;

    public static string Format(string key, params object[] args) => string.Format(Culture, Get(key), args);

    /// <summary>The language's own name, e.g. "Polski", "Deutsch", "日本語".</summary>
    public static string NativeName(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        string name = culture.NativeName;
        return name.Length == 0 ? language : char.ToUpper(name[0], culture) + name[1..];
    }
}
