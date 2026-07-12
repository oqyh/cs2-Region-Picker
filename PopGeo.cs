using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CS2RegionPicker;

public static class PopGeo
{

    public static readonly Dictionary<string, (double Lat, double Lon)> Coords = new()
    {
        ["ams"]  = (52.37, 4.90),
        ["atl"]  = (33.75, -84.39),
        ["bom"]  = (19.08, 72.88),
        ["maa"]  = (13.08, 80.27),
        ["canu"] = (23.13, 113.26),
        ["can"]  = (23.13, 113.26),
        ["cbr"]  = (-35.28, 149.13),
        ["ctu"]  = (30.57, 104.07),
        ["dfw"]  = (32.78, -96.80),
        ["dxb"]  = (25.20, 55.27),
        ["eze"]  = (-34.61, -58.38),
        ["fra"]  = (50.11, 8.68),
        ["gru"]  = (-23.55, -46.63),
        ["hkg"]  = (22.32, 114.17),
        ["iad"]  = (38.90, -77.04),
        ["jnb"]  = (-26.20, 28.05),
        ["lax"]  = (34.05, -118.24),
        ["lhr"]  = (51.51, -0.13),
        ["lim"]  = (-12.05, -77.04),
        ["lux"]  = (49.61, 6.13),
        ["mad"]  = (40.42, -3.70),
        ["man"]  = (14.60, 120.98),
        ["okc"]  = (35.47, -97.52),
        ["ord"]  = (41.88, -87.63),
        ["par"]  = (48.86, 2.35),
        ["pek"]  = (39.90, 116.41),
        ["pvg"]  = (31.23, 121.47),
        ["pwj"]  = (30.59, 114.30),
        ["pww"]  = (30.59, 114.30),
        ["pwu"]  = (38.04, 114.51),
        ["pwz"]  = (30.27, 120.15),
        ["scl"]  = (-33.45, -70.67),
        ["sea"]  = (47.61, -122.33),
        ["seo"]  = (37.57, 126.98),
        ["sgp"]  = (1.35, 103.82),
        ["sha"]  = (31.23, 121.47),
        ["shb"]  = (31.23, 121.47),
        ["sto"]  = (59.33, 18.07),
        ["syd"]  = (-33.87, 151.21),
        ["tgd"]  = (23.13, 113.26),
        ["tsn"]  = (39.08, 117.20),
        ["tyo"]  = (35.68, 139.69),
        ["vie"]  = (48.21, 16.37),
        ["waw"]  = (52.23, 21.01),
    };

    public static (double Lat, double Lon)? Lookup(string code)
    {
        code = code.ToLowerInvariant();
        for (int len = code.Length; len >= 2; len--)
        {
            if (Coords.TryGetValue(code.Substring(0, len), out var c))
            {
                return c;
            }
        }
        return null;
    }

    static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<(double Lat, double Lon)?> ResolveAsync(
        string code, System.Collections.Generic.List<string> ips,
        System.Collections.Generic.Dictionary<string, string> cache)
    {

        var built = Lookup(code);
        if (built != null) return built;

        if (cache.TryGetValue(code, out string? cached) && TryParse(cached, out var cc))
            return cc;

        foreach (string ip in ips)
        {
            var geo = await GeolocateIpAsync(ip);
            if (geo != null)
            {
                cache[code] = geo.Value.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + "," + geo.Value.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return geo;
            }
        }
        return null;
    }

    static async Task<(double Lat, double Lon)?> GeolocateIpAsync(string ip)
    {

        try
        {
            string json = await Http.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,lat,lon");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var st) && st.GetString() == "success" &&
                root.TryGetProperty("lat", out var la) && root.TryGetProperty("lon", out var lo))
                return (la.GetDouble(), lo.GetDouble());
        }
        catch { }

        try
        {
            string json = await Http.GetStringAsync($"https://ipapi.co/{ip}/json/");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("latitude", out var la) && root.TryGetProperty("longitude", out var lo) &&
                la.ValueKind == System.Text.Json.JsonValueKind.Number)
                return (la.GetDouble(), lo.GetDouble());
        }
        catch { }

        return null;
    }

    static bool TryParse(string s, out (double Lat, double Lon) c)
    {
        c = default;
        var parts = s.Split(',');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon))
        {
            c = (lat, lon);
            return true;
        }
        return false;
    }
}
