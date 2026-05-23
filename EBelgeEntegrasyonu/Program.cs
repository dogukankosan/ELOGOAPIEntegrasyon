using System.Text;
using System.Threading.RateLimiting;
using EBelgeAPI.Data.Interfaces;
using EBelgeAPI.Data.Repositories;
using EBelgeAPI.Middleware;
using EBelgeAPI.Services;
using EBelgeAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
// ── Windows Service desteği ───────────────────────────
builder.Host.UseWindowsService();
string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
string jwtSecret = builder.Configuration["Jwt:Secret"]!;
// ── Temel servisler ───────────────────────────────────
builder.Services.AddMemoryCache();   // IMemoryCache Singleton — cache burada yaşar
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
// ── Repository'ler ────────────────────────────────────
builder.Services.AddScoped<ILogoSettingsRepository>(sp =>
    new LogoSettingsRepository(connStr, sp.GetRequiredService<IEncryptionService>()));
builder.Services.AddScoped<ILogoTokenRepository>(sp =>
    new LogoTokenRepository(connStr, sp.GetRequiredService<IEncryptionService>()));
builder.Services.AddScoped<IApiLogRepository>(_ => new ApiLogRepository(connStr));
builder.Services.AddScoped<IRevokedTokenRepository>(_ => new RevokedTokenRepository(connStr));
builder.Services.AddScoped<ICariFilterRepository>(_ => new CariFilterRepository(connStr));
builder.Services.AddScoped<ITransferRepository>(_ => new TransferRepository(connStr));
builder.Services.AddScoped<IFaturaAmbarRepository>(_ => new FaturaAmbarRepository(connStr));
builder.Services.AddScoped<IAmbarRepository>(_ => new AmbarRepository(connStr)); builder.Services.AddScoped<IDashboardRepository>(_ =>
    new DashboardRepository(connStr));
builder.Services.AddScoped<IFaturaSatisElemaniRepository>(_ =>
    new FaturaSatisElemaniRepository(connStr));
builder.Services.AddScoped<IELogoSettingsRepository>(sp =>
    new ELogoSettingsRepository(connStr, sp.GetRequiredService<IEncryptionService>()));
// ── Uygulama servisleri ───────────────────────────────
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<ILogoTokenService, LogoTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISalesInvoiceService, SalesInvoiceService>();
// ── HTTP Client'lar ───────────────────────────────────
builder.Services.AddHttpClient<ISatisElemaniService, SatisElemaniService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
builder.Services.AddHttpClient<ILogoTransferService, LogoTransferService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
builder.Services.AddHttpClient<IELogoService, ELogoService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
// ── Logo malzeme cache ────────────────────────────────
// Transient olarak kaydedildi — IMemoryCache zaten Singleton olduğu için
// cache uygulama boyunca yaşar, Scoped DI sorunları olmaz
builder.Services.AddHttpClient<ILogoItemCacheService, LogoItemCacheService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
}); builder.Services.AddHttpClient<ILotTakipService, LotTakipService>(client =>  // ← buraya
{
    client.Timeout = TimeSpan.FromMinutes(5);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
builder.Services.AddHttpClient("Logo", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
// ── JWT Authentication ────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
        opt.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(
                    "{\"success\":false,\"message\":\"Bu işlem için giriş yapmanız gerekiyor. " +
                    "Lütfen /api/auth/login ile token alın.\"}");
            },
            OnForbidden = ctx =>
            {
                ctx.Response.StatusCode = 403;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(
                    "{\"success\":false,\"message\":\"Bu işlem için yetkiniz bulunmuyor.\"}");
            }
        };
    });
builder.Services.AddAuthorization();
// ── Rate Limiter ──────────────────────────────────────
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("login", o =>
    {
        o.PermitLimit = 30;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    opt.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 10;
    });
    opt.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"success\":false,\"message\":\"Çok fazla istek gönderildi. " +
            "Lütfen bir dakika bekleyip tekrar deneyin.\"}");
    };
});
// ── Controllers + Swagger ─────────────────────────────
builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(c =>
//{
//    c.SwaggerDoc("v1", new OpenApiInfo
//    {
//        Title = "EBelge Entegrasyon API",
//        Version = "v1",
//        Description = "Logo ERP e-fatura/e-arşiv UBL indirme ve fatura sorgulama API'si."
//    });
//    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//    {
//        Name = "Authorization",
//        Type = SecuritySchemeType.ApiKey,
//        Scheme = "Bearer",
//        BearerFormat = "JWT",
//        In = ParameterLocation.Header,
//        Description = "Örnek: Bearer eyJhbGci..."
//    });
//    c.AddSecurityRequirement(new OpenApiSecurityRequirement
//    {
//        {
//            new OpenApiSecurityScheme
//            {
//                Reference = new OpenApiReference
//                {
//                    Type = ReferenceType.SecurityScheme,
//                    Id   = "Bearer"
//                }
//            },
//            []
//        }
//    });
//});
// ── CORS ──────────────────────────────────────────────
builder.Services.AddCors(opt =>
{
    string[] allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];
    opt.AddPolicy("UIPolicy", p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});
// ─────────────────────────────────────────────────────
WebApplication app = builder.Build();
// ─────────────────────────────────────────────────────
// ── Uygulama başlarken malzeme cache'ini ısıt ─────────
// Scope içinde çağrıldığı için Scoped bağımlılıklar sorunsuz çalışır
using (var scope = app.Services.CreateScope())
{
    var itemCache = scope.ServiceProvider.GetRequiredService<ILogoItemCacheService>();
    var startLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await itemCache.RefreshAsync();
        startLogger.LogInformation("Logo malzeme cache başarıyla yüklendi.");
    }
    catch (Exception ex)
    {
        startLogger.LogWarning(ex,
            "Uygulama başlangıcında malzeme cache yüklenemedi. " +
            "Transfer sırasında lazy load yapılacak.");
    }
}
// ── Middleware pipeline ───────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<TokenRevocationMiddleware>();
//app.UseSwagger();
//app.UseSwaggerUI(c =>
  //  c.SwaggerEndpoint("/swagger/v1/swagger.json", "EBelge API v1"));
// app.UseHttpsRedirection(); // ← HTTP'de çalışıyoruz, kapalı
app.UseCors("UIPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();