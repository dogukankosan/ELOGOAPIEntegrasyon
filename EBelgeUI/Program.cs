using EBelgeUI.Filters;
using EBelgeUI.Middleware;

namespace EBelgeUI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Host.UseWindowsService();
            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.Add<SessionAuthFilter>();
            });
            builder.Services.AddDistributedMemoryCache(); // ← ekle
            // Session
            builder.Services.AddSession(opt =>
            {
                opt.IdleTimeout = TimeSpan.FromHours(8);
                opt.Cookie.HttpOnly = true;
                opt.Cookie.IsEssential = true;
                opt.Cookie.SameSite = SameSiteMode.Lax;
            });
            // API HttpClient
            builder.Services.AddHttpClient("API", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"]!);
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Add("X-API-Key",
                    builder.Configuration["ApiSettings:ApiKey"]!);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
            WebApplication app = builder.Build();
            // ── MIDDLEWARE SIRASI ÖNEMLİ ──
            app.UseMiddleware<ExceptionMiddleware>(); // ← EN BAŞA
            app.UseRouting();
            app.UseSession();
            app.UseAuthorization();
            app.UseStatusCodePagesWithReExecute("/Dashboard/HttpError/{0}"); // ← SONRA STATUS CODE
            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Dashboard}/{action=Index}/{id?}")
                .WithStaticAssets();
            app.Run();
        }
    }
}