using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

namespace MatcBuddy.Api.Infrastructure
{
    /// <summary>Ters vekil ayarları (Proxy bölümü). Varsayılan: yalnızca loopback güvenilir.</summary>
    public sealed class ProxyOptions
    {
        public const string SectionName = "Proxy";
        public string[] KnownProxies { get; set; } = Array.Empty<string>();
        /// <summary>CIDR, ör. "10.0.0.0/8".</summary>
        public string[] KnownNetworks { get; set; } = Array.Empty<string>();
        public int ForwardLimit { get; set; } = 1;
    }

    public static class PlatformSetup
    {
        // ------------------------------------------------------------------ Forwarded headers

        public static void ConfigureForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
        {
            var proxy = configuration.GetSection(ProxyOptions.SectionName).Get<ProxyOptions>() ?? new ProxyOptions();
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.ForwardLimit = proxy.ForwardLimit <= 0 ? null : proxy.ForwardLimit;
                o.KnownProxies.Clear();
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Add(IPAddress.Loopback);
                o.KnownProxies.Add(IPAddress.IPv6Loopback);
                foreach (var p in proxy.KnownProxies)
                {
                    if (IPAddress.TryParse(p, out var ip)) o.KnownProxies.Add(ip);
                    else throw new InvalidOperationException($"Proxy:KnownProxies içinde geçersiz IP: {p}");
                }
                foreach (var n in proxy.KnownNetworks)
                {
                    if (System.Net.IPNetwork.TryParse(n, out var net)) o.KnownIPNetworks.Add(net);
                    else throw new InvalidOperationException($"Proxy:KnownNetworks içinde geçersiz CIDR: {n}");
                }
            });
        }

        // ------------------------------------------------------------------ Localization

        public static RequestLocalizationOptions LocalizationOptions(IConfiguration configuration)
        {
            var cultures = configuration.GetSection("Localization:SupportedCultures").Get<string[]>() is { Length: > 0 } c
                ? c : new[] { "tr", "en" };
            var defaultCulture = configuration["Localization:DefaultCulture"] ?? "tr";
            Msg.SupportedLanguages = cultures.Select(x => x.ToLowerInvariant()).ToArray();
            Msg.DefaultCulture = CultureInfo.GetCultureInfo(defaultCulture);
            CultureInfo.DefaultThreadCurrentCulture = Msg.DefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = Msg.DefaultCulture;

            var options = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(defaultCulture),
                SupportedCultures = cultures.Select(CultureInfo.GetCultureInfo).ToList(),
                SupportedUICultures = cultures.Select(CultureInfo.GetCultureInfo).ToList(),
                FallBackToParentCultures = true,
                FallBackToParentUICultures = true,
                ApplyCurrentCultureToResponseHeaders = true,
            };
            // Sıra: ?culture=en (test/hata ayıklama), sonra Accept-Language. Çerez kullanılmaz.
            options.RequestCultureProviders = new List<IRequestCultureProvider>
            {
                new QueryStringRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider(),
            };
            return options;
        }

        // ------------------------------------------------------------------ Logging

        public static void ConfigureSerilog(this WebApplicationBuilder builder)
        {
            var filePath = builder.Configuration["Logging:File:Path"] ?? "logs/matcbuddy-.log";
            var retained = builder.Configuration.GetValue("Logging:File:RetainedFileCountLimit", 14);
            if (!Path.IsPathRooted(filePath)) filePath = Path.Combine(builder.Environment.ContentRootPath, filePath);
            builder.Services.AddSerilog((services, lc) => lc
                .ReadFrom.Configuration(builder.Configuration)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(filePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: retained,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}"));
        }

        /// <summary>Yapılandırılmış istek logu: yöntem, yol, durum, süre, kullanıcı, traceId, istemci IP.</summary>
        public static IApplicationBuilder UseStructuredRequestLogging(this IApplicationBuilder app) =>
            app.UseSerilogRequestLogging(o =>
            {
                o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0} ms (user {UserId}, ip {ClientIp}, trace {TraceId})";
                o.GetLevel = (ctx, _, ex) => ex != null || ctx.Response.StatusCode >= 500 ? LogEventLevel.Error
                    : ctx.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                    : LogEventLevel.Information;
                o.EnrichDiagnosticContext = (diag, ctx) =>
                {
                    diag.Set("UserId", ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "-");
                    diag.Set("TraceId", ApiErrors.TraceId(ctx));
                    diag.Set("ClientIp", ctx.Connection.RemoteIpAddress?.ToString() ?? "-");
                };
            });

        // ------------------------------------------------------------------ Güvenlik başlıkları + trace

        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
            app.Use(async (ctx, next) =>
            {
                ctx.Response.OnStarting(() =>
                {
                    var h = ctx.Response.Headers;
                    h["X-Content-Type-Options"] = "nosniff";
                    h["Referrer-Policy"] = "no-referrer";
                    h["X-Frame-Options"] = "DENY";
                    if (!h.ContainsKey("Content-Security-Policy"))
                        h["Content-Security-Policy"] = "frame-ancestors 'none'";
                    h[ApiErrors.TraceHeader] = ApiErrors.TraceId(ctx);
                    return Task.CompletedTask;
                });
                await next();
            });
    }

    /// <summary>/health: veritabanı bağlantısı.</summary>
    public sealed class DatabaseHealthCheck : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                await using var db = new MatcBuddy.DataAccess.MatcBuddyContext();
                return await db.Database.CanConnectAsync(ct)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("Database unreachable.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database unreachable.", ex);
            }
        }
    }
}
