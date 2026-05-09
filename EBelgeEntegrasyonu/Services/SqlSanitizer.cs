namespace EBelgeAPI.Services;

public static class SqlSanitizer
{
    // Tehlikeli SQL keyword'leri
    private static readonly string[] DangerousKeywords =
    [
        "--", ";--", ";", "/*", "*/", "xp_", "exec ", "execute ",
        "insert ", "update ", "delete ", "drop ", "create ", "alter ",
        "truncate ", "union ", "select ", "bulk ", "waitfor ", "cast(",
        "convert(", "char(", "nchar(", "varchar(", "declare ", "set ",
        "fetch ", "kill ", "open ", "cursor ", "sys.", "sysobjects",
        "syscolumns", "information_schema"
    ];
    /// <summary>
    /// String filtreyi validate eder. Tehlikeli içerik varsa exception fırlatır.
    /// </summary>
    public static string Sanitize(string? input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        string? lower = input.ToLowerInvariant();
        foreach (string? keyword in DangerousKeywords)
        {
            if (lower.Contains(keyword))
                throw new InvalidOperationException(
                    $"Geçersiz karakter veya ifade tespit edildi: '{fieldName}' alanı güvenli değil.");
        }
        // Tek tırnak escape
        return input.Trim().Replace("'", "''");
    }
    /// <summary>
    /// Sadece harf, rakam, nokta, tire, alt çizgi içeren alanlar için (cari kodu gibi)
    /// </summary>
    public static string SanitizeCode(string? input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        foreach (char c in input)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_' && c != ' ')
                throw new InvalidOperationException(
                    $"'{fieldName}' alanında geçersiz karakter tespit edildi: '{c}'");
        }
        return input.Trim().Replace("'", "''");
    }
    /// <summary>
    /// Sadece rakam içeren alanlar için (VKN, TCKN gibi)
    /// </summary>
    public static string SanitizeNumeric(string? input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        foreach (char c in input)
        {
            if (!char.IsDigit(c))
                throw new InvalidOperationException(
                    $"'{fieldName}' alanı sadece rakam içerebilir.");
        }
        return input.Trim();
    }
}