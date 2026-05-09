using System.Text.RegularExpressions;

namespace EBelgeAPI.Services;

public static class SensitiveDataMasker
{
    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        // access_token, access-token
        (new Regex(@"(access[_-]token["":\s]+)[^\s,""&}]+", RegexOptions.IgnoreCase), "$1***MASKED***"),
        // Bearer token
        (new Regex(@"(Bearer\s+)[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase), "$1***MASKED***"),
        // password
        (new Regex(@"(password["":\s]+)[^\s,""&}]+", RegexOptions.IgnoreCase), "$1***MASKED***"),
        // client_secret
        (new Regex(@"(client[_]secret["":\s]+)[^\s,""&}]+", RegexOptions.IgnoreCase), "$1***MASKED***"),
        // refresh_token
        (new Regex(@"(refresh[_]token["":\s]+)[^\s,""&}]+", RegexOptions.IgnoreCase), "$1***MASKED***"),
        // acces_token (Logo bazen yanlış yazıyor)
        (new Regex(@"(acces[_]token["":\s]+)[^\s,""&}]+", RegexOptions.IgnoreCase), "$1***MASKED***"),
    ];
    public static string? Mask(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        string? result = input;
        foreach (var (pattern, replacement) in Rules)
            result = pattern.Replace(result, replacement);
        return result;
    }
}