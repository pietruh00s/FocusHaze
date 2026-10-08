using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinDimmer.Core;

internal sealed class AppSettings
{
    public const int MinIntensity = 5;
    public const int MaxIntensity = 90;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDimmer", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public bool Enabled { get; set; } = true;

    /// <summary>Haze opacity in percent.</summary>
    public int Intensity { get; set; } = 40;

    /// <summary>Haze color as #RRGGBB.</summary>
    public string Color { get; set; } = "#000000";

    public bool Animate { get; set; } = true;

    public int FadeDurationMs { get; set; } = 200;

    /// <summary>Dim only the display that contains the active window.</summary>
    public bool ActiveDisplayOnly { get; set; }

    /// <summary>UI language code (e.g. "de"); empty follows the Windows display language.</summary>
    public string Language { get; set; } = "";

    [JsonIgnore]
    public byte Alpha => (byte)Math.Round(Math.Clamp(Intensity, MinIntensity, MaxIntensity) * 255 / 100.0);

    /// <summary>The color as a GDI COLORREF (0x00BBGGRR).</summary>
    [JsonIgnore]
    public uint ColorRef
    {
        get
        {
            var (r, g, b) = ParseColor(Color);
            return (uint)(r | (g << 8) | (b << 16));
        }
    }

    public static (byte R, byte G, byte B) ParseColor(string hex)
    {
        string digits = hex.TrimStart('#');
        if (digits.Length == 6 && uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            return ((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return (0, 0, 0);
    }

    public static string FormatColor(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable settings fall back to defaults.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not fatal: settings stay in memory for this session.
        }
    }
}
