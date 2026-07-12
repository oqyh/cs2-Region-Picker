using System.Collections.Generic;

namespace CS2RegionPicker;

public static class Flags
{
    private static readonly Dictionary<string, string> Countries = new()
    {
        ["ams"] = "NL",
        ["atl"] = "US",
        ["bom"] = "IN",
        ["maa"] = "IN",
        ["can"] = "CN",
        ["ctu"] = "CN",
        ["pek"] = "CN",
        ["pwg"] = "CN",
        ["sha"] = "CN",
        ["sha2"] = "CN",
        ["shb"] = "CN",
        ["tsn"] = "CN",
        ["cdg"] = "FR",
        ["par"] = "FR",
        ["dfw"] = "US",
        ["den"] = "US",
        ["dxb"] = "AE",
        ["eat"] = "US",
        ["fra"] = "DE",
        ["gru"] = "BR",
        ["hkg"] = "HK",
        ["iad"] = "US",
        ["ist"] = "TR",
        ["jhb"] = "ZA",
        ["lax"] = "US",
        ["lhr"] = "GB",
        ["lim"] = "PE",
        ["lux"] = "LU",
        ["mad"] = "ES",
        ["man"] = "GB",
        ["mwh"] = "US",
        ["okc"] = "US",
        ["ord"] = "US",
        ["scl"] = "CL",
        ["sea"] = "US",
        ["seo"] = "KR",
        ["sgp"] = "SG",
        ["sto"] = "SE",
        ["syd"] = "AU",
        ["tyo"] = "JP",
        ["vie"] = "AT",
        ["waw"] = "PL",
        ["bog"] = "CO",
        ["eze"] = "AR",
        ["hel"] = "FI",
        ["mba"] = "KE",
        ["tlv"] = "IL",
        ["yyz"] = "CA",
        ["yul"] = "CA",
        ["bne"] = "AU",
        ["mel"] = "AU",
        ["per"] = "AU",
        ["akl"] = "NZ",
        ["dub"] = "IE",
        ["osl"] = "NO",
        ["cph"] = "DK",
        ["mil"] = "IT",
        ["zrh"] = "CH",
        ["prg"] = "CZ",
        ["bud"] = "HU",
        ["buh"] = "RO",
        ["sof"] = "BG",
        ["kie"] = "UA",
        ["mos"] = "RU",
        ["led"] = "RU",
        ["ekb"] = "RU",
        ["jkt"] = "ID",
        ["kul"] = "MY",
        ["bkk"] = "TH",
        ["mnl"] = "PH",
        ["tpe"] = "TW",
        ["cai"] = "EG",
        ["lag"] = "NG",
        ["cpt"] = "ZA",
        ["mex"] = "MX",
        ["pan"] = "PA",
        ["canu"] = "CN",
        ["cbr"] = "AU",
        ["jnb"] = "ZA",
        ["pvg"] = "CN",
        ["pwj"] = "CN",
        ["pwu"] = "CN",
        ["pww"] = "CN",
        ["pwz"] = "CN",
        ["tgd"] = "ME",
    };

    public static string? Country(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        code = code.ToLowerInvariant();

        for (int len = code.Length; len >= 2; len--)
        {
            if (Countries.TryGetValue(code.Substring(0, len), out string? c))
                return c;
        }
        return null;
    }
}
