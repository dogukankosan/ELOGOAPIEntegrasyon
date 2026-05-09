namespace EBelgeUI.Models;
public class ApiLogDto
{
    public int Id { get; set; }
    public string? Level { get; set; }
    public string? Source { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public string? Message { get; set; }
    public string? Detail { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public int? Duration { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class ApiLogFilterViewModel
{
    public string? Level { get; set; }
    public string? Source { get; set; }
    public string? Username { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class ApiLogIndexViewModel
{
    public ApiLogFilterViewModel Filter { get; set; } = new();
    public List<ApiLogDto> Logs { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<string> Sources { get; set; } = new();
    public List<string> Usernames { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
public class ApiLogApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ApiLogDto>? Data { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
public class ApiLogSourceResponse
{
    public bool Success { get; set; }
    public List<string>? Data { get; set; }
}