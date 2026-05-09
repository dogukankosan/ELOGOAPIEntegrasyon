namespace EBelgeAPI.Models.Responses;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public static ApiResponse<T> Ok(T data, string? message = null)
        => new() { Success = true, Data = data, Message = message };
    public static ApiResponse<T> Fail(string message)
        => new() { Success = false, Message = message };
}
public class PagedResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<T>? Data { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling((double)TotalCount / PageSize)
        : 0;
    public static PagedResponse<T> Ok(List<T> data, int totalCount, int page, int pageSize)
        => new() { Success = true, Data = data, TotalCount = totalCount, Page = page, PageSize = pageSize };
    public static PagedResponse<T> Fail(string message)
        => new() { Success = false, Message = message };
}
public class LoginResponse
{
    public string Token { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}