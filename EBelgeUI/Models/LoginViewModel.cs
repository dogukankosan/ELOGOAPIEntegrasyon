namespace EBelgeUI.Models;
public class LoginViewModel
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public bool RememberMe { get; set; }
    public string? ErrorMessage { get; set; }
}
public class LoginResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public LoginData? Data { get; set; }
}
public class LoginData
{
    public string Token { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}