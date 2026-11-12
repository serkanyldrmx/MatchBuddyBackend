using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth;
using MatcBuddy.Business.Auth;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.Entity;
using Microsoft.Extensions.Options;

namespace MatcBuddy.Api.Services.Auth
{
    /// <summary>Auth:Google / Auth:Facebook (gizli değerler user-secrets / ortam değişkeni). docs/auth-api.md §Kurulum.</summary>
    public sealed class ExternalAuthOptions
    {
        public const string SectionName = "Auth";
        public GoogleOptions Google { get; set; } = new();
        public FacebookOptions Facebook { get; set; } = new();

        public sealed class GoogleOptions
        {
            public string? ClientId { get; set; }
            public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
        }

        public sealed class FacebookOptions
        {
            public string? AppId { get; set; }
            public string? AppSecret { get; set; }
            public string GraphVersion { get; set; } = "v21.0";
            public bool IsConfigured => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret);
        }
    }

    /// <summary>Google Identity Services id_token (JWT) doğrulaması: imza, süre, iss, aud = Auth:Google:ClientId.</summary>
    public sealed class GoogleTokenValidator : IExternalTokenValidator
    {
        private readonly ExternalAuthOptions.GoogleOptions _o;
        private readonly ILogger<GoogleTokenValidator> _logger;

        public GoogleTokenValidator(IOptions<ExternalAuthOptions> options, ILogger<GoogleTokenValidator> logger)
        {
            _o = options.Value.Google;
            _logger = logger;
        }

        public string Provider => ExternalProviders.Google;
        public bool IsConfigured => _o.IsConfigured;

        public async Task<OperationResult<ExternalIdentity>> ValidateAsync(string token, CancellationToken ct = default)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(token, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _o.ClientId! },
                });
                return OperationResult<ExternalIdentity>.Ok(new ExternalIdentity(
                    ExternalProviders.Google, payload.Subject, payload.Email, payload.EmailVerified,
                    payload.Name, payload.GivenName, payload.FamilyName, payload.Picture));
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogInformation("Google id_token geçersiz: {Reason}", ex.Message);
                return OperationResult<ExternalIdentity>.Fail(OperationStatus.Unauthorized, ErrorCodes.Auth.ExternalTokenInvalid);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Google sertifikaları alınamadı");
                return OperationResult<ExternalIdentity>.BadRequest(ErrorCodes.Auth.ProviderUnavailable);
            }
        }
    }

    /// <summary>
    /// Facebook: Graph debug_token (uygulama erişim anahtarı = AppId|AppSecret) ile token'ın bu uygulamaya ait ve geçerli
    /// olduğu doğrulanır, sonra /me?fields=id,name,email,first_name,last_name,picture (appsecret_proof ile) okunur.
    /// E-posta yalnızca auth.facebookEmailTrusted = true ise "doğrulanmış" sayılır (hesap bağlama için).
    /// </summary>
    public sealed class FacebookTokenValidator : IExternalTokenValidator
    {
        public const string HttpClientName = "facebook";
        private readonly ExternalAuthOptions.FacebookOptions _o;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<FacebookTokenValidator> _logger;

        public FacebookTokenValidator(IOptions<ExternalAuthOptions> options, IHttpClientFactory http, ILogger<FacebookTokenValidator> logger)
        {
            _o = options.Value.Facebook;
            _http = http;
            _logger = logger;
        }

        public string Provider => ExternalProviders.Facebook;
        public bool IsConfigured => _o.IsConfigured;

        public async Task<OperationResult<ExternalIdentity>> ValidateAsync(string token, CancellationToken ct = default)
        {
            var client = _http.CreateClient(HttpClientName);
            var graph = $"https://graph.facebook.com/{_o.GraphVersion}";
            try
            {
                var appToken = Uri.EscapeDataString($"{_o.AppId}|{_o.AppSecret}");
                using var debug = await client.GetAsync($"{graph}/debug_token?input_token={Uri.EscapeDataString(token)}&access_token={appToken}", ct);
                if ((int)debug.StatusCode >= 500) return OperationResult<ExternalIdentity>.BadRequest(ErrorCodes.Auth.ProviderUnavailable);
                using var debugDoc = JsonDocument.Parse(await debug.Content.ReadAsStringAsync(ct));
                if (!debugDoc.RootElement.TryGetProperty("data", out var data)
                    || !data.TryGetProperty("is_valid", out var valid) || !valid.GetBoolean()
                    || !data.TryGetProperty("app_id", out var appId) || appId.GetString() != _o.AppId
                    || !data.TryGetProperty("user_id", out var userIdEl))
                    return Invalid("debug_token geçersiz");
                var userId = userIdEl.GetString();

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_o.AppSecret!));
                var proof = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
                using var me = await client.GetAsync(
                    $"{graph}/me?fields=id,name,email,first_name,last_name,picture.type(large)&access_token={Uri.EscapeDataString(token)}&appsecret_proof={proof}", ct);
                if ((int)me.StatusCode >= 500) return OperationResult<ExternalIdentity>.BadRequest(ErrorCodes.Auth.ProviderUnavailable);
                if (!me.IsSuccessStatusCode) return Invalid($"/me HTTP {(int)me.StatusCode}");
                using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync(ct));
                var root = meDoc.RootElement;
                var id = Str(root, "id");
                if (string.IsNullOrEmpty(id) || id != userId) return Invalid("kullanıcı kimliği eşleşmedi");
                string? picture = null;
                if (root.TryGetProperty("picture", out var pic) && pic.TryGetProperty("data", out var pd)
                    && !(pd.TryGetProperty("is_silhouette", out var sil) && sil.ValueKind == JsonValueKind.True))
                    picture = Str(pd, "url");

                return OperationResult<ExternalIdentity>.Ok(new ExternalIdentity(
                    ExternalProviders.Facebook, id, Str(root, "email"), P.Bool(ParameterKeys.Auth.FacebookEmailTrusted),
                    Str(root, "name"), Str(root, "first_name"), Str(root, "last_name"), picture));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Facebook Graph API'ye ulaşılamadı");
                return OperationResult<ExternalIdentity>.BadRequest(ErrorCodes.Auth.ProviderUnavailable);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Facebook yanıtı okunamadı");
                return OperationResult<ExternalIdentity>.BadRequest(ErrorCodes.Auth.ProviderUnavailable);
            }
        }

        private OperationResult<ExternalIdentity> Invalid(string reason)
        {
            _logger.LogInformation("Facebook token geçersiz: {Reason}", reason);
            return OperationResult<ExternalIdentity>.Fail(OperationStatus.Unauthorized, ErrorCodes.Auth.ExternalTokenInvalid);
        }

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    /// <summary>Sosyal girişte yeni hesabın profil fotoğrafını medya hattından geçirir (en iyi çaba; hata girişi engellemez).</summary>
    public sealed class ExternalAvatarImporter
    {
        public const string HttpClientName = "avatar-import";
        private static readonly string[] AllowedHostSuffixes =
            { "googleusercontent.com", "fbcdn.net", "fbsbx.com", "facebook.com" };
        private const int MaxBytes = 5 * 1024 * 1024; // indirme tavanı; asıl sınır medya hattındaki media.maxUploadMb

        private readonly IHttpClientFactory _http;
        private readonly IMediaService _media;
        private readonly MatcBuddy.DataAccess.Abstract.IAuthDal _dal;
        private readonly ILogger<ExternalAvatarImporter> _logger;

        public ExternalAvatarImporter(IHttpClientFactory http, IMediaService media, MatcBuddy.DataAccess.Abstract.IAuthDal dal,
            ILogger<ExternalAvatarImporter> logger)
        {
            _http = http;
            _media = media;
            _dal = dal;
            _logger = logger;
        }

        public static bool IsAllowed(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
            && AllowedHostSuffixes.Any(s => u.Host == s || u.Host.EndsWith("." + s, StringComparison.OrdinalIgnoreCase));

        /// <summary>Başarılıysa yeni URL.</summary>
        public async Task<string?> ImportAsync(int playerId, string? url, CancellationToken ct)
        {
            if (!IsAllowed(url)) return null;
            try
            {
                var client = _http.CreateClient(HttpClientName);
                using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!res.IsSuccessStatusCode || res.Content.Headers.ContentLength > MaxBytes) return null;
                await using var stream = await res.Content.ReadAsStreamAsync(ct);
                using var ms = new MemoryStream();
                var buffer = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    ms.Write(buffer, 0, read);
                    if (ms.Length > MaxBytes) return null;
                }
                var upload = await _media.UploadBytesAsync(ms.ToArray(), MediaKind.Avatar, playerId, ct);
                if (!upload.Success) return null;
                _media.MarkAttached(upload.Data.MediaId, playerId);
                _dal.SetAvatar(playerId, upload.Data.Url, upload.Data.MediaId);
                return upload.Data.Url;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Sosyal giriş profil fotoğrafı içe aktarılamadı (oyuncu {PlayerId})", playerId);
                return null;
            }
        }
    }
}
