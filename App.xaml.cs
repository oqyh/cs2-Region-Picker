using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace CS2RegionPicker;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    public App()
    {
        Settings = AppSettings.Load();
    }
}

public class AppSettings
{
    public bool DarkMode { get; set; } = true;
    public string Language { get; set; } = "en";

    public List<string> BlockedCodes { get; set; } = new();
    public List<string> KnownCodes { get; set; } = new();
    public bool HasSelection { get; set; } = false;
    public string AppliedFingerprint { get; set; } = "";

    public Dictionary<string, string> GeoCache { get; set; } = new();

    public double PanelX { get; set; } = -1;
    public double PanelY { get; set; } = -1;
    public double PanelW { get; set; } = 238;
    public double PanelH { get; set; } = 180;

    static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CS2RegionPicker");

    static string FilePath => Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public static class ThemeManager
{

    static readonly (string Key, string Dark, string Light)[] Palette =
    {
        ("WinBg",         "#1E2A38", "#EFF1F5"),
        ("CardBg",        "#263340", "#FFFFFF"),
        ("CardBorder",    "#3B4B5E", "#DCE0E8"),
        ("InputBg",       "#1A2532", "#E6E9EF"),
        ("InputBorder",   "#2E3B48", "#CCD0D9"),
        ("TextPrimary",   "#D8DEE9", "#4C4F69"),
        ("TextSecondary", "#81A1C1", "#6C6F85"),
        ("Accent",        "#88C0D0", "#DD7878"),
        ("AccentHover",   "#A3D4E0", "#E89E9E"),
        ("Success",       "#A3BE8C", "#40A02B"),
        ("Warning",       "#EBCB8B", "#B36B00"),
        ("Error",         "#BF616A", "#D20F39"),
        ("LogBg",         "#17212B", "#EFF1F5"),
        ("BtnSecBg",      "#2E3B48", "#E6E9EF"),
        ("BtnSecHover",   "#3B4B5E", "#DCE0E8"),
        ("ProgressTrack", "#2E3B48", "#DCE0E8"),
        ("DragBarBg",     "#1A2532", "#FFFFFF"),
        ("DropdownBg",    "#263340", "#FFFFFF"),
        ("DropdownHover", "#3B4B5E", "#E6E9EF"),
        ("ToggleTrackBg", "#2E3B48", "#CCD0D9"),
        ("ToggleOnBg",    "#4A5D75", "#B7C4D6"),
        ("ToggleKnobBg",  "#D8DEE9", "#FFFFFF"),
        ("CheckFg",       "#D8DEE9", "#4C4F69"),

        ("ScrollThumb",      "#3E4C5E", "#B9C2CE"),
        ("ScrollThumbHover", "#5A6C82", "#98A4B4"),
        ("MapSea",        "#17212B", "#DCE7F2"),
        ("MapLand",       "#2E3B48", "#B9CADD"),
        ("MapGrid",       "#3B4B5E", "#A9BAD0"),
    };

    public static void Apply(bool dark)
    {
        var r = Application.Current.Resources;
        foreach (var (key, darkHex, lightHex) in Palette)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? darkHex : lightHex));
            b.Freeze();
            r[key] = b;
        }
    }
}
