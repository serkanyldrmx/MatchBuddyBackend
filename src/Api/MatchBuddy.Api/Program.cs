using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Business;
using MatchBuddy.Api.Controllers;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services;
using MatchBuddy.Api.Services.Auth;
using MatchBuddy.Api.Services.Messaging;
using MatchBuddy.Core;
using MatchBuddy.DataAccess;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

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

            // Loglama: Serilog (konsol + günlük dönen dosya, Logging:File:Path)
            builder.ConfigureSerilog();

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
            builder.Services.AddLocalization();
            builder.Services.AddSingleton<IMessageCatalog, ResxMessageCatalog>();
            var localizationOptions = PlatformSetup.LocalizationOptions(builder.Configuration);

            builder.Services
                .AddControllers(o =>
                {
                    o.Filters.Add<StandardErrorResultFilter>();
                    ModelValidation.LocalizeModelBindingMessages(o.ModelBindingMessageProvider);
                    o.ModelMetadataDetailsProviders.Add(new LocalizedValidationMetadataProvider());
                })
                .ConfigureApiBehaviorOptions(o =>
                {
                    // Model doğrulaması → VALIDATION_FAILED; gövdesiz 4xx'ler filtrede standart gövdeye çevrilir
                    o.InvalidModelStateResponseFactory = ModelValidation.InvalidModelStateResponse;
                    o.SuppressMapClientErrors = true;
                })
                .AddJsonOptions(o => o.AllowInputFormatterExceptionMessages = false);

            builder.Services.AddExceptionHandler<StandardExceptionHandler>();
            builder.Services.AddProblemDetails();
            builder.Services.ConfigureForwardedHeaders(builder.Configuration);
            builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

            // Swagger: Swagger:Enabled (varsayılan: Development'ta açık, diğer ortamlarda kapalı)
            var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", builder.Environment.IsDevelopment());
            if (swaggerEnabled)
            {
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
                        Description = "/api/Auth/Login'den aldığınız token'ı girin."
                    });
                    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
                    });
                });
            }

            builder.Services.AddBussinesRegistration();
            builder.Services.AddCoreRegistration();
            builder.Services.AddDataAccessRegistration();

            // Sistem parametreleri (docs/platform-api.md): önbellekli okuyucu + yönetim
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<EFParameterService>();
            builder.Services.AddSingleton<IParameterService>(sp => sp.GetRequiredService<EFParameterService>());
            builder.Services.AddSingleton<IParameterStore>(sp => sp.GetRequiredService<EFParameterService>());

            builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
            builder.Services.AddScoped<MatchBuddy.Api.Services.Ai.RecommendationService>();
            // Anlık mesajlaşma (docs/realtime-api.md)
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<MatchBuddy.Api.Hubs.RealtimeNotifier>();

            // ---- Kimlik doğrulama paketi (docs/auth-api.md)
            builder.Services.AddSingleton(new MatchBuddy.Business.Auth.AuthSettings
            {
                FrontendBaseUrl = builder.Configuration["Auth:FrontendBaseUrl"] ?? "http://localhost:3000",
                // Kod özetleri için gizli anahtar: Auth:CodePepper, yoksa Jwt:Key
                CodePepper = builder.Configuration["Auth:CodePepper"] is { Length: >= 16 } pepper ? pepper : jwt.Key,
            });
            builder.Services.Configure<RefreshCookieOptions>(builder.Configuration.GetSection(RefreshCookieOptions.SectionName));
            builder.Services.AddSingleton<RefreshCookie>();
            builder.Services.AddScoped<AuthSessionIssuer>();
            builder.Services.Configure<ExternalAuthOptions>(builder.Configuration.GetSection(ExternalAuthOptions.SectionName));
            builder.Services.AddSingleton<MatchBuddy.Business.Auth.IExternalTokenValidator, GoogleTokenValidator>();
            builder.Services.AddSingleton<MatchBuddy.Business.Auth.IExternalTokenValidator, FacebookTokenValidator>();
            builder.Services.AddScoped<ExternalAvatarImporter>();
            builder.Services.AddSingleton<IdentifierThrottle>();
            builder.Services.AddHttpClient(FacebookTokenValidator.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15));
            builder.Services.AddHttpClient(ExternalAvatarImporter.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10));
            builder.Services.AddHttpClient(NetgsmSmsSender.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(20));

            // E-posta / SMS: yapılandırılmamışsa outbox (App_Data/outbox/*.txt). Gönderim arka planda kuyruktan yapılır.
            builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
            builder.Services.Configure<NetgsmOptions>(builder.Configuration.GetSection(NetgsmOptions.SectionName));
            builder.Services.AddSingleton<OutboxWriter>();
            var smtpConfigured = builder.Configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>()?.IsConfigured == true;
            var netgsmConfigured = builder.Configuration.GetSection(NetgsmOptions.SectionName).Get<NetgsmOptions>()?.IsConfigured == true;
            if (smtpConfigured) builder.Services.AddSingleton<MatchBuddy.Core.Messaging.IEmailSender, SmtpEmailSender>();
            else builder.Services.AddSingleton<MatchBuddy.Core.Messaging.IEmailSender, OutboxEmailSender>();
            if (netgsmConfigured) builder.Services.AddSingleton<MatchBuddy.Core.Messaging.ISmsSender, NetgsmSmsSender>();
            else builder.Services.AddSingleton<MatchBuddy.Core.Messaging.ISmsSender, OutboxSmsSender>();
            builder.Services.AddSingleton<OutgoingMessageQueue>();
            builder.Services.AddSingleton<MatchBuddy.Core.Messaging.IOutgoingMessageQueue>(sp => sp.GetRequiredService<OutgoingMessageQueue>());
            builder.Services.AddHostedService<OutgoingMessageService>();

            // OpenStreetMap (Nominatim / Overpass): 40 sn zaman aşımı, kimlik belirten User-Agent
            builder.Services.AddHttpClient(OsmService.HttpClientName, c =>
            {
                // Overpass sorgusu sunucuda osm.overpassTimeoutSeconds sürebilir; aynalar paralel denendiği için toplam bekleme bunu aşmaz
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
                    options.MapInboundClaims = true; // ClaimTypes.NameIdentifier / Role (önceki sürümlerle aynı)
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
                    // 401 / 403 → standart hata gövdesi (AUTH_UNAUTHORIZED / AUTH_FORBIDDEN)
                    options.Events = new JwtBearerEvents
                    {
                        // SignalR (WebSocket) access token'ı başlık yerine sorgu dizesinde gönderir: yalnızca hub yolunda kabul edilir
                        OnMessageReceived = ctx =>
                        {
                            var accessToken = ctx.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken) && ctx.HttpContext.Request.Path.StartsWithSegments(MatchBuddy.Api.Hubs.ChatHub.Path))
                                ctx.Token = accessToken;
                            return Task.CompletedTask;
                        },
                        OnChallenge = async ctx =>
                        {
                            ctx.HandleResponse();
                            if (ctx.Response.HasStarted) return;
                            // Süresi dolmuş token: RFC 6750 error_description (kod yine AUTH_UNAUTHORIZED; istemci Refresh dener)
                            ctx.Response.Headers.WWWAuthenticate = ctx.AuthenticateFailure is SecurityTokenExpiredException
                                ? "Bearer error=\"invalid_token\", error_description=\"The access token expired\""
                                : "Bearer";
                            await ApiErrors.WriteAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.Auth.Unauthorized);
                        },
                        OnForbidden = ctx =>
                            ApiErrors.WriteAsync(ctx.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.Auth.Forbidden),
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
                          .AllowAnyHeader()
                          .AllowCredentials() // refresh çerezi (mb_rt) için: fetch(..., { credentials: 'include' })
                          .WithExposedHeaders(ApiErrors.TraceHeader, "Retry-After"));
            });

            // İstek sınırları: dakika başına izin sayısı sistem parametresinden (rateLimit.*) okunur.
            // Sınır değeri bölüm anahtarına eklenir: parametre değişince yeni sınır hemen geçerli olur.
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("auth", httpContext =>
                {
                    var limit = P.Int(ParameterKeys.RateLimit.AuthPerMinute);
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}:{limit}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
                });

                // Şifre sıfırlama (ForgotPassword / ResetPassword): IP başına; tanımlayıcı başına sınır IdentifierThrottle'da
                options.AddPolicy(AuthController.PasswordResetPolicy, httpContext =>
                {
                    var limit = P.Int(ParameterKeys.Auth.PasswordResetPerMinute);
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter($"pwreset:{ip}:{limit}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
                });

                // Fotoğraf yükleme: kullanıcı başına (UseAuthentication'dan sonra çalışır)
                options.AddPolicy(MediaUploadLimits.Policy, httpContext =>
                {
                    var limit = P.Int(ParameterKeys.RateLimit.UploadPerMinute);
                    var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var key = !string.IsNullOrEmpty(userId)
                        ? "user:" + userId
                        : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                    return RateLimitPartition.GetFixedWindowLimiter($"upload:{key}:{limit}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
                });
                options.OnRejected = async (context, ct) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                    else
                        context.HttpContext.Response.Headers.RetryAfter = "60";
                    await ApiErrors.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.Common.RateLimited);
                };
            });

            var app = builder.Build();

            // Parametreler: statik yardımcılar DI'daki önbellekli servisi kullanır; tanımlar tabloya senkronlanır
            Parameters.Current = app.Services.GetRequiredService<IParameterService>();
            try
            {
                var added = app.Services.GetRequiredService<IParameterStore>().SyncDefinitions();
                if (added > 0) app.Logger.LogInformation("{Count} sistem parametresi varsayılan değerle eklendi.", added);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Sistem parametreleri senkronlanamadı; kod varsayılanları kullanılacak.");
            }

            // ---- Pipeline ----
            // Ters vekil arkasında gerçek istemci IP'si / şema (Proxy:KnownProxies / KnownNetworks)
            app.UseForwardedHeaders();
            app.UseSecurityHeaders();
            app.UseStructuredRequestLogging();

            // Beklenmeyen hatalar → COMMON_INTERNAL_ERROR (iç ayrıntı yalnızca Development'ta)
            app.UseExceptionHandler();
            // Gövdesiz 4xx/5xx (bilinmeyen rota, 405 …) → standart hata gövdesi
            app.UseStatusCodePages(StatusCodeBodies.WriteAsync);

            app.UseRequestLocalization(localizationOptions);

            if (swaggerEnabled)
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            if (!app.Environment.IsDevelopment())
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

            // Sağlık kontrolü: anonim, istek sınırı yok
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = async (ctx, report) =>
                {
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsJsonAsync(new
                    {
                        status = report.Status.ToString(),
                        totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
                        checks = report.Entries.ToDictionary(e => e.Key, e => new
                        {
                            status = e.Value.Status.ToString(),
                            durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                        }),
                    });
                },
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
                },
            }).AllowAnonymous().DisableRateLimiting();

            app.MapControllers();
            app.MapHub<MatchBuddy.Api.Hubs.ChatHub>(MatchBuddy.Api.Hubs.ChatHub.Path);

            try
            {
                app.Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
