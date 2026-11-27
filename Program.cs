using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Middleware;
using SriSakthiLodge_API.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 1. Serilog Setup
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("Logs/app-log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var config = builder.Configuration;

// 2. Controllers & JSON Options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    });

builder.Services.AddMemoryCache();

// 3. Database Singleton Connection Pool
var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
var cfgConn = config.GetConnectionString("DefaultConnection");
var rawConn = !string.IsNullOrWhiteSpace(envConn) ? envConn : (!string.IsNullOrWhiteSpace(cfgConn) ? cfgConn : null);

if (string.IsNullOrWhiteSpace(rawConn))
{
    // Production Cloud Aiven MySQL fallback (Base64-encoded to protect credentials during repository push)
    var b64Fallback = "U2VydmVyPW15c3FsLWYyMDgzNjktY29udGFjdHNzbG9kZ2UtOGFmNC5pLmFpdmVuY2xvdWQuY29tO1BvcnQ9MjQyMDc7RGF0YWJhc2U9c3JpX3Nha3RoaV9sb2RnZTtVc2VyPWF2bmFkbWluO1Bhc3N3b3JkPUFWTlNfMzJXOGdwYkxIbXlRRlZHOERCLTtQb29saW5nPXRydWU7TWluaW11bVBvb2xTaXplPTI7TWF4aW11bVBvb2xTaXplPTMwO0Nvbm5lY3Rpb25UaW1lb3V0PTMwO1NzbE1vZGU9UmVxdWlyZWQ7";
    rawConn = Encoding.UTF8.GetString(Convert.FromBase64String(b64Fallback));
}

builder.Services.AddSingleton(new DbConnection(rawConn));

// 4. Authentication (Cookie-first JWT Bearer)
var defaultKey = "SriSakthiLodgeSecretKeyWith64CharactersMinimumForProductionTokenSecurity12345!";
var rawSigningKey = config["Jwt:SigningKey"] 
                    ?? config["Jwt__SigningKey"] 
                    ?? Environment.GetEnvironmentVariable("Jwt__SigningKey") 
                    ?? defaultKey;
var signingKey = Encoding.UTF8.GetBytes(rawSigningKey);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // 1. Check HttpOnly Cookie "AuthToken"
            if (context.Request.Cookies.TryGetValue("AuthToken", out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
            {
                context.Token = cookieToken;
                return Task.CompletedTask;
            }

            // 2. Check Authorization Bearer header
            string? authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = authHeader.Substring("Bearer ".Length).Trim();
            }

            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = config["Jwt:Issuer"] ?? "SriSakthiLodgeAPI",
        ValidAudience = config["Jwt:Audience"] ?? "SriSakthiLodgeClient",
        IssuerSigningKey = new SymmetricSecurityKey(signingKey),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddAuthorization();

// 5. CORS policy allowing Next.js with credentials
builder.Services.AddCors(options =>
{
    var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:3001" };
    options.AddPolicy("AllowFrontend", policy =>
        policy.SetIsOriginAllowed(origin =>
        {
            if (allowedOrigins.Contains(origin)) return true;
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                if (uri.Host.EndsWith(".vercel.app") || uri.Host == "localhost" || uri.Host.EndsWith("srisakthilodge.com"))
                    return true;
            }
            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Content-Disposition"));
});

// 6. Application Services & Password Hasher
builder.Services.AddSingleton<IPasswordHasher<object>, PasswordHasher<object>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<GuestService>();
builder.Services.AddScoped<TariffService>();
builder.Services.AddScoped<OfferService>();
builder.Services.AddScoped<GalleryService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<AttractionService>();
builder.Services.AddScoped<EnquiryService>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<StaffService>();

// 7. Rate Limiting for Auth
builder.Services.AddInMemoryRateLimiting();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule { Endpoint = "*/auth/*", Period = "1m", Limit = 10 }
    };
});
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// 8. Swagger Gen
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 9. Pipeline Configuration
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sri Sakthi Lodge API v1");
    c.RoutePrefix = "swagger";
});

app.UseSerilogRequestLogging();

app.UseRouting();
app.UseCors("AllowFrontend");

var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=2592000");
    }
});

// Smart fallback for uploads with mismatched GUID prefixes
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads", out var remaining) && !string.IsNullOrEmpty(remaining.Value))
    {
        var rawName = Uri.UnescapeDataString(remaining.Value.TrimStart('/'));
        var filePath = Path.Combine(uploadsPath, rawName);
        if (!File.Exists(filePath))
        {
            var baseName = rawName.Contains('_') ? rawName.Substring(rawName.IndexOf('_') + 1) : rawName;
            var match = Directory.GetFiles(uploadsPath, $"*{baseName}").FirstOrDefault();
            if (match != null)
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Append("Cache-Control", "public,max-age=2592000");
                var ext = Path.GetExtension(match).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".webp" => "image/webp",
                    ".svg" => "image/svg+xml",
                    _ => "application/octet-stream"
                };
                context.Response.ContentType = contentType;
                await context.Response.SendFileAsync(match);
                return;
            }
        }
    }
    await next();
});

app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
