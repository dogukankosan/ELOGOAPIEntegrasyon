using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EBelgeUI.Filters;
public class SessionAuthFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        bool hasSkip = context.ActionDescriptor.EndpointMetadata
            .OfType<SkipSessionAuthAttribute>()
            .Any();
        if (hasSkip)
        {
            base.OnActionExecuting(context);
            return;
        }
        var session = context.HttpContext.Session;
        string? token = session.GetString("Token");
        // Session yoksa cookie'den hatırla
        if (string.IsNullOrEmpty(token))
        {
            string? rememberToken = context.HttpContext.Request.Cookies["RememberToken"];
            string? rememberUser = context.HttpContext.Request.Cookies["RememberUser"];
            if (!string.IsNullOrEmpty(rememberToken) && !string.IsNullOrEmpty(rememberUser))
            {
                session.SetString("Token", rememberToken);
                session.SetString("Username", rememberUser);
                session.SetString("ExpiresAt", DateTime.UtcNow.AddDays(7).ToString("o"));
                token = rememberToken;
            }
        }
        if (string.IsNullOrEmpty(token))
        {
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }
        // Token expire kontrolü
        string? expiresAtStr = session.GetString("ExpiresAt");
        if (!string.IsNullOrEmpty(expiresAtStr) &&
            DateTime.TryParse(expiresAtStr, out var expiresAt) &&
            DateTime.UtcNow >= expiresAt)
        {
            session.Clear();
            context.HttpContext.Response.Cookies.Delete("RememberToken");
            context.HttpContext.Response.Cookies.Delete("RememberUser");
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }
        base.OnActionExecuting(context);
    }
}