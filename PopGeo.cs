using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CS2RegionPicker;

public static class PopGeo
{
    public readonly record struct Geo(double Lat, double Lon, string Country);

    static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static string? CountryFromCache(
        string code, double lat, double lon, Dictionary<string, string> cache)
    {
        code = code.ToLowerInvariant();
        if (cache.TryGetValue(code, out string? s) && TryParse(s, out Geo g) &&
            g.Country.Length > 0 &&
            Math.Abs(g.Lat - lat) < 1.5 && Math.Abs(g.Lon - lon) < 1.5)
        {
            Flags.SetCountry(code, g.Country);
            return g.Country;
        }
        return null;
    }

    public static Geo? PositionFromCache(string code, Dictionary<string, string> cache)
    {
        code = code.ToLowerInvariant();
        if (cache.TryGetValue(code, out string? s) && TryParse(s, out Geo g))
        {
            Flags.SetCountry(code, g.Country);
            return g;
        }
        return null;
    }

    static void Store(string code, double lat, double lon, string country,
                      Dictionary<string, string> cache)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        cache[code.ToLowerInvariant()] =
            lat.ToString(inv) + "," + lon.ToString(inv) + "," + country;
        Flags.SetCountry(code, country);
    }

    public static async Task<string?> ResolveCountryAsync(
        string code, double lat, double lon, List<string> ips,
        Dictionary<string, string> cache)
    {
        code = code.ToLowerInvariant();

        string? cached = CountryFromCache(code, lat, lon, cache);
        if (cached != null) return cached;

        string? cc = await ReverseGeocodeAsync(lat, lon);

        if (cc == null)
        {

            foreach (string ip in ips)
            {
                Geo? g = await GeolocateIpAsync(ip);
                if (g != null && g.Value.Country.Length > 0) { cc = g.Value.Country; break; }
            }
        }

        if (cc != null) Store(code, lat, lon, cc, cache);
        return cc;
    }

    static async Task<string?> ReverseGeocodeAsync(double lat, double lon)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            string json = await Http.GetStringAsync(
                "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude=" +
                lat.ToString(inv) + "&longitude=" + lon.ToString(inv) + "&localityLanguage=en");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("countryCode", out var c) &&
                c.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                string cc = (c.GetString() ?? "").Trim().ToUpperInvariant();
                if (cc.Length == 2) return cc;
            }
        }
        catch { }
        return null;
    }

    public static async Task<Geo?> ResolvePositionAsync(
        string code, List<string> ips, Dictionary<string, string> cache)
    {
        code = code.ToLowerInvariant();

        Geo? cached = PositionFromCache(code, cache);
        if (cached != null) return cached;

        foreach (string ip in ips)
        {
            Geo? geo = await GeolocateIpAsync(ip);
            if (geo != null)
            {
                Store(code, geo.Value.Lat, geo.Value.Lon, geo.Value.Country, cache);
                return geo;
            }
        }
        return null;
    }

    static async Task<Geo?> GeolocateIpAsync(string ip)
    {
        try
        {
            string json = await Http.GetStringAsync(
                $"http://ip-api.com/json/{ip}?fields=status,lat,lon,countryCode");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var st) && st.GetString() == "success" &&
                root.TryGetProperty("lat", out var la) && root.TryGetProperty("lon", out var lo))
            {
                string cc = root.TryGetProperty("countryCode", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.String
                    ? c.GetString() ?? "" : "";
                return new Geo(la.GetDouble(), lo.GetDouble(), cc.ToUpperInvariant());
            }
        }
        catch { }

        try
        {
            string json = await Http.GetStringAsync($"https://ipapi.co/{ip}/json/");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("latitude", out var la) && root.TryGetProperty("longitude", out var lo) &&
                la.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                string cc = root.TryGetProperty("country_code", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.String
                    ? c.GetString() ?? "" : "";
                return new Geo(la.GetDouble(), lo.GetDouble(), cc.ToUpperInvariant());
            }
        }
        catch { }

        return null;
    }

    static bool TryParse(string s, out Geo g)
    {
        g = default;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string[] parts = s.Split(',');

        if (parts.Length == 3 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Float, inv, out double lat) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out double lon))
        {
            g = new Geo(lat, lon, parts[2].Trim().ToUpperInvariant());
            return true;
        }
        return false;
    }
}
