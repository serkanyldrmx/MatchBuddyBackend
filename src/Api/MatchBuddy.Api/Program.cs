using System.Text;
using System.Threading.RateLimiting;
using Business;
using MatchBuddy.Api.Controllers;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services;
using MatchBuddy.Core;
using MatchBuddy.DataAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace MatchBuddy.Api
{
    public class Program
    {
        private const string CorsPolicy = "Frontend";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ---- Ayarlar ----
            // Bağlantı dizesi appsettings'ten okunur (ortam değişkeni: ConnectionStrings__MatchBuddy)
            var connectionString = builder.Configuration.GetConnectionString("MatchBuddy");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                MatchBuddyContext.ConnectionString = connectionString;
            }

            // JWT anahtarı kaynak koda/appsettings'e yazılmaz: user-secrets veya ortam değişkeni (Jwt__Key)
            builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
            // user-secrets varsayılan olarak sadece "Development" ortamında yüklenir;
            // Production dışındaki tüm ortamlarda (ör. VS'de farklı bir profil) da yüklensin.
            if (!builder.Environment.IsProduction())
            {
                builder.Configuration.AddUserSecrets<Program>(optional: true);
            }

            var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
            if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
            {
                if (!builder.Environment.IsProduction())
                {
                    // Geliştirmede anahtar yoksa geçici rastgele anahtar üret: uygulama açılır,
                    // ama her yeniden başlatmada token'lar geçersiz olur (kullanıcılar tekrar giriş yapar).
                    var tempKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
                    builder.Configuration["Jwt:Key"] = tempKey;
                    jwt.Key = tempKey;
                    Console.WriteLine(
                        $"UYARI: Jwt:Key bulunamadı (ortam: {builder.Environment.EnvironmentName}). Geçici anahtar kullanılıyor. " +
                        "Kalıcı yapmak için src/Api/MatchBuddy.Api klasöründe: dotnet user-secrets set \"Jwt:Key\" \"<en-az-32-karakter>\"");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Jwt:Key ayarı eksik veya 32 karakterden kısa (ortam: {builder.Environment.EnvironmentName}). " +
                        "Sunucuda Jwt__Key ortam değişkenini en az 32 karakterlik rastgele bir değerle tanımlayın.");
                }
            }

            // ---- Servisler ----
            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                // Swagger'da "Authorize" butonu ile token girilebilsin
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "PlayerLogin'den aldığınız token'ı girin."
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.AddBussinesRegistration();
            builder.Services.AddCoreRegistration();
            builder.Services.AddDataAccessRegistration();

            builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

            // OpenStreetMap (Nominatim / Overpass): 25 sn zaman aşımı, kimlik belirten User-Agent
            builder.Services.AddHttpClient(OsmService.HttpClientName, c =>
            {
                // Overpass sorgusu 25 sn sunucu süresi alabilir; aynalar paralel denendiği için toplam bekleme bunu aşmaz
                c.Timeout = TimeSpan.FromSeconds(40);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("MatchBuddy/1.0 (+https://github.com/serkanyldrmx/MatchBuddy)");
            });
            builder.Services.AddScoped<OsmService>();

            // Maç hatırlatmaları (Reminders:IntervalSeconds, varsayılan 600 sn)
            builder.Services.AddHostedService<MatchReminderService>();

            // Medya hattı (docs/media-api.md): Storage:Provider = Local (varsayılan) | S3
            builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
            var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
            if (storageOptions.IsS3)
                builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
            else
                builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
            builder.Services.AddSingleton<IMediaService, MediaService>();
            builder.Services.AddHostedService<MediaCleanupService>();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                    };
                });
            builder.Services.AddAuthorization();

            // CORS: sadece ayarlarda tanımlı frontend adreslerine izin ver
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                 ?? new[] { "http://localhost:3000" };
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicy, policy =>
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader());
            });

            // Giriş / kayıt uç noktaları için kaba kuvvet koruması: IP başına dakikada N istek (varsayılan 10)
            var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("auth", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authPermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }));

                // Fotoğraf yükleme: kullanıcı başına dakikada N (varsayılan 20). UseAuthentication'dan sonra çalışır.
                var uploadPermitLimit = builder.Configuration.GetValue("RateLimiting:UploadPermitLimit", 20);
                options.AddPolicy(MediaUploadLimits.Policy, httpContext =>
                {
                    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var key = !string.IsNullOrEmpty(userId)
                        ? "user:" + userId
                        : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = uploadPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
                });
                options.OnRejected = async (context, ct) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        Success = false,
                        Message = "Çok fazla istek. Lütfen biraz sonra tekrar deneyin."
                    }, ct);
                };
            });

            var app = builder.Build();

            // ---- Pipeline ----
            // Beklenmeyen hatalarda iç detayları (exception mesajı, stack trace) istemciye sızdırma
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var feature = context.Features.Get<IExceptionHandlerFeature>();
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(feature?.Error, "İşlenmeyen hata: {Path}", context.Request.Path);

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Success = false,
                        Message = "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin."
                    });
                });
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles(); // wwwroot/uploads (eski profil / tesis resimleri; geçiş tamamlanana kadar)

            // /media: yerel depoda dosyalar doğrudan (immutable cache), S3/R2'de PublicBaseUrl'e 302
            if (storageOptions.IsS3)
            {
                var publicBase = (storageOptions.S3.PublicBaseUrl ?? string.Empty).TrimEnd('/');
                app.MapGet("/media/{**key}", (string key) =>
                        StorageKeys.IsSafe(key) ? Results.Redirect($"{publicBase}/{key}") : Results.NotFound())
                    .AllowAnonymous()
                    .ExcludeFromDescription();
            }
            else
            {
                var mediaRoot = storageOptions.ResolveLocalRoot(app.Environment.ContentRootPath);
                Directory.CreateDirectory(mediaRoot);
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mediaRoot),
                    RequestPath = "/media",
                    OnPrepareResponse = ctx =>
                        ctx.Context.Response.Headers.CacheControl = StorageOptions.CacheControl,
                });
            }

            app.UseCors(CorsPolicy);

            app.UseAuthentication();
            // Rate limiter kimlik doğrulamadan sonra: "upload" politikası kullanıcıya göre bölümlenir
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
