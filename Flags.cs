using System.Collections.Generic;

namespace CS2RegionPicker;

public static class Flags
{
    private static readonly Dictionary<string, string> Runtime =
        new(StringComparer.OrdinalIgnoreCase);

    public static void SetCountry(string code, string country)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(country)) return;
        Runtime[code.ToLowerInvariant()] = country.ToUpperInvariant();
    }

    public static string? Country(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        code = code.ToLowerInvariant();

        for (int len = code.Length; len >= 2; len--)
        {
            if (Runtime.TryGetValue(code.Substring(0, len), out string? c))
                return c;
        }
        return null;
    }
}
