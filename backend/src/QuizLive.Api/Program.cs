using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using QuizLive.Api.Data;
using QuizLive.Api.Domain;
using QuizLive.Api.Hubs;
using QuizLive.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var jwtSecret = cfg["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt__Secret deve essere lungo almeno 32 caratteri (JWT_SECRET nel file .env).");

var mediaPath = Path.GetFullPath(cfg["Media:Path"] ?? "media");
Directory.CreateDirectory(mediaPath);

// ---------------------------------------------------------------- servizi
builder.Services.AddDbContext<QuizDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Default")));

builder.Services.AddSingleton<ConnectionTracker>();
builder.Services.AddSingleton<IQuizBroadcaster, HubBroadcaster>();
builder.Services.AddSingleton<GameEngine>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();

void ConfigureJson(JsonSerializerOptions o)
{
    o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.Converters.Add(new JsonStringEnumConverter());          // enum come stringhe
}

builder.Services.AddControllers().AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions));

builder.Services.AddSignalR(o =>
{
    o.KeepAliveInterval = TimeSpan.FromSeconds(15);
    o.ClientTimeoutInterval = TimeSpan.FromSeconds(45);
    o.HandshakeTimeout = TimeSpan.FromSeconds(15);
}).AddJsonProtocol(o => ConfigureJson(o.PayloadSerializerOptions));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenService.Key(jwtSecret),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = TokenService.RoleClaim,
            NameClaimType = TokenService.NameClaim
        };
        // SignalR (WebSocket) non può inviare header: il token arriva in query string
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hub"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // il reverse proxy (nginx) sta nella rete Docker: ci fidiamo di lui
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 105L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 105L * 1024 * 1024);

// NESSUNA policy CORS: stessa origine tramite nginx.

var app = builder.Build();

// ---------------------------------------------------------------- migrazioni + seed
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            // Con migrazioni presenti (dotnet ef migrations add Initial) si applicano; altrimenti lo schema viene creato direttamente.
            if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
            else await db.Database.EnsureCreatedAsync();
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning("Database non pronto (tentativo {Attempt}/10): {Message}", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    await DbSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>(), cfg, logger);
}

// ---------------------------------------------------------------- pipeline
app.UseForwardedHeaders();        // prima del rate limiter: serve l'IP reale del client
app.UseRateLimiter();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaPath),
    RequestPath = "/media",
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=604800"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<QuizHub>("/hub/quiz");

app.MapGet("/healthz", async (QuizDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));

app.Run();

public partial class Program { }
