using System.Security.Cryptography;
using System.Text;
using MatcBuddy.Business.Security;
using MatcBuddy.Core.Messaging;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.Entity;
using Microsoft.Extensions.Logging;

namespace MatcBuddy.Business.Auth
{
    public sealed record ClientInfo(string? Ip, string? UserAgent);

    /// <summary>Yeni verilen refresh token (ham değer yalnızca çereze yazılır, veritabanında özeti durur).</summary>
    public sealed record IssuedSession(string RefreshToken, DateTime ExpiresAtUtc, bool Persistent);

    /// <summary>NewSession null ise: eş zamanlı yenileme tolerans penceresi — çerez değiştirilmez, yalnızca access token verilir.</summary>
    public sealed record RefreshResult(Player Player, IssuedSession? NewSession);

    public interface IAuthService
    {
        Player? GetPlayer(int playerId);

        // Oturum
        IssuedSession StartSession(int playerId, bool persistent, ClientInfo client);
        OperationResult<RefreshResult> Refresh(string? rawRefreshToken, ClientInfo client);
        /// <summary>Çerezdeki token kalıcı mı ("Beni hatırla"); bilinmiyorsa null.</summary>
        bool? IsPersistent(string? rawRefreshToken);
        void Logout(string? rawRefreshToken);
        int LogoutAll(int playerId);
        int RevokeAllSessions(int playerId, string reason);

        // Şifre
        OperationResult<bool> ForgotPassword(string? identifier, string? channel, ClientInfo client);
        OperationResult<Player> ResetPassword(string? identifier, string? code, string? newPassword);
        OperationResult<Player> ChangePassword(int playerId, string? currentPassword, string? newPassword);
        /// <summary>Profil güncellemesiyle (eski uç nokta) şifre değiştiğinde: tüm oturumlar kapanır, bilgilendirme e-postası.</summary>
        void OnPasswordChangedExternally(int playerId);

        // E-posta doğrulama
        /// <summary>Başarılıysa Data = 0; bekleme süresindeyse AUTH_CODE_RESEND_COOLDOWN ve Args[0] = kalan saniye.</summary>
        OperationResult<int> SendEmailVerification(int playerId, bool enforceCooldown, ClientInfo client);
        OperationResult<Player> VerifyEmail(int playerId, string? code);

        // KVKK
        string CurrentTermsVersion { get; }
        AppError? ValidateRegistrationConsent(string? termsVersion);
        void ApplyRegistrationConsent(Player player, bool marketingConsent);
        OperationResult<Player> AcceptTerms(int playerId, string? termsVersion, bool marketingConsent);
        bool ConsentRequired(Player player);
        List<string> MissingProfileFields(Player player);
        Dictionary<string, object?> Export(int playerId);

        // Sosyal giriş bağlantıları
        List<ExternalLogin> GetExternalLogins(int playerId);
        OperationResult<bool> UnlinkExternal(int playerId, string? provider);
    }

    /// <summary>Oturum (refresh token rotation), şifre sıfırlama, e-posta doğrulama ve KVKK onayı — docs/auth-api.md.</summary>
    public sealed class AuthManager : IAuthService
    {
        public const int CodeDigits = 6;
        private const int MaxIpLength = 45;
        private const int MaxUserAgentLength = 300;

        private readonly IAuthDal _dal;
        private readonly IOutgoingMessageQueue _outbox;
        private readonly AuthSettings _settings;
        private readonly ILogger<AuthManager> _logger;

        public AuthManager(IAuthDal dal, IOutgoingMessageQueue outbox, AuthSettings settings, ILogger<AuthManager> logger)
        {
            _dal = dal;
            _outbox = outbox;
            _settings = settings;
            _logger = logger;
        }

        public Player? GetPlayer(int playerId) => _dal.FindPlayer(playerId);

        // ================================================================== Oturum

        public static string HashToken(string raw) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

        private static string NewRawToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static TimeSpan Lifetime(bool persistent) => persistent
            ? TimeSpan.FromDays(P.Int(ParameterKeys.Auth.RefreshTokenDays))
            : TimeSpan.FromHours(P.Int(ParameterKeys.Auth.SessionRefreshTokenHours));

        private static string? Cut(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];

        private (RefreshToken Entity, IssuedSession Issued) NewToken(int playerId, Guid familyId, bool persistent, ClientInfo client)
        {
            var raw = NewRawToken();
            var now = DateTime.UtcNow;
            var entity = new RefreshToken
            {
                PlayerId = playerId,
                TokenHash = HashToken(raw),
                FamilyId = familyId,
                CreatedAt = now,
                ExpiresAt = now + Lifetime(persistent),
                Persistent = persistent,
                CreatedByIp = Cut(client.Ip, MaxIpLength),
                UserAgent = Cut(client.UserAgent, MaxUserAgentLength),
            };
            return (entity, new IssuedSession(raw, entity.ExpiresAt, persistent));
        }

        public IssuedSession StartSession(int playerId, bool persistent, ClientInfo client)
        {
            var (entity, issued) = NewToken(playerId, Guid.NewGuid(), persistent, client);
            _dal.AddRefreshToken(entity);
            return issued;
        }

        public OperationResult<RefreshResult> Refresh(string? raw, ClientInfo client)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Unauthorized<RefreshResult>(ErrorCodes.Auth.RefreshInvalid);
            var token = _dal.FindRefreshToken(HashToken(raw));
            if (token == null) return Unauthorized<RefreshResult>(ErrorCodes.Auth.RefreshInvalid);

            var now = DateTime.UtcNow;
            if (token.RevokedAt != null) return RevokedTokenPresented(token, now);
            if (token.ExpiresAt <= now) return Unauthorized<RefreshResult>(ErrorCodes.Auth.RefreshInvalid);

            var player = _dal.FindPlayer(token.PlayerId);
            if (player == null || player.IsDeleted)
            {
                _dal.RevokeFamily(token.FamilyId, RevokeReasons.AccountDeleted, now);
                return Unauthorized<RefreshResult>(ErrorCodes.Auth.RefreshInvalid);
            }

            var (replacement, issued) = NewToken(player.PlayerId, token.FamilyId, token.Persistent, client);
            if (!_dal.TryRotate(token.RefreshTokenId, replacement, now))
            {
                // Aynı token başka bir istekte az önce döndürüldü (iki sekme aynı anda yeniledi)
                var current = _dal.GetRefreshToken(token.RefreshTokenId);
                return current == null
                    ? Unauthorized<RefreshResult>(ErrorCodes.Auth.RefreshInvalid)
                    : RevokedTokenPresented(current, now);
            }
            return OperationResult<RefreshResult>.Ok(new RefreshResult(player, issued));
        }

        /// <summary>
        /// İptal edilmiş token geldi. Döndürülmüş (rotated) bir token tolerans penceresi içinde gelirse eş zamanlı yenilemedir:
        /// yeni access token verilir, çerez değişmez. Pencere dışında ise token çalınmış sayılır → tüm aile iptal.
        /// </summary>
        private OperationResult<RefreshResult> RevokedTokenPresented(RefreshToken token, DateTime now)
        {
            if (token.RevokedReason != RevokeReasons.Rotated || token.ReplacedById == null)
                return Unauthorized<RefreshResult>(ErrorCodes.Auth.RefreshInvalid);

            var grace = P.Int(ParameterKeys.Auth.RefreshReuseGraceSeconds);
            if (grace > 0 && token.RevokedAt != null && now - token.RevokedAt.Value <= TimeSpan.FromSeconds(grace))
            {
                var next = _dal.GetRefreshToken(token.ReplacedById.Value);
                var player = _dal.FindPlayer(token.PlayerId);
                if (next != null && next.ExpiresAt > now && (next.RevokedAt == null || next.RevokedReason == RevokeReasons.Rotated)
                    && player != null && !player.IsDeleted)
                    return OperationResult<RefreshResult>.Ok(new RefreshResult(player, null));
            }

            var revoked = _dal.RevokeFamily(token.FamilyId, RevokeReasons.ReuseDetected, now);
            _logger.LogWarning("Refresh token yeniden kullanıldı: oyuncu {PlayerId}, aile {FamilyId}; {Count} token iptal edildi.",
                token.PlayerId, token.FamilyId, revoked);
            return Unauthorized<RefreshResult>(ErrorCodes.Auth.RefreshReused);
        }

        public bool? IsPersistent(string? raw) =>
            string.IsNullOrWhiteSpace(raw) ? null : _dal.FindRefreshToken(HashToken(raw))?.Persistent;

        public void Logout(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var token = _dal.FindRefreshToken(HashToken(raw));
            if (token == null) return;
            // Mevcut oturumun zinciri (döndürülmüş eski token gelse bile) kapanır
            _dal.RevokeFamily(token.FamilyId, RevokeReasons.Logout, DateTime.UtcNow);
        }

        public int LogoutAll(int playerId) => RevokeAllSessions(playerId, RevokeReasons.LogoutAll);

        public int RevokeAllSessions(int playerId, string reason) => _dal.RevokeAllForPlayer(playerId, reason, DateTime.UtcNow);

        private static OperationResult<T> Unauthorized<T>(string code, params object?[] args) =>
            OperationResult<T>.Fail(OperationStatus.Unauthorized, code, args);

        // ================================================================== Kodlar

        private string HashCode(int playerId, byte purpose, string code)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.CodePepper));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{playerId}:{purpose}:{code}"))).ToLowerInvariant();
        }

        private static string NewCode() =>
            RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeDigits)).ToString("D" + CodeDigits);

        private string IssueCode(Player p, byte purpose, byte channel, string destination, int ttlMinutes, ClientInfo client)
        {
            var code = NewCode();
            var now = DateTime.UtcNow;
            _dal.ReplaceCode(new AuthCode
            {
                PlayerId = p.PlayerId,
                Purpose = purpose,
                Channel = channel,
                CodeHash = HashCode(p.PlayerId, purpose, code),
                Destination = destination,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(ttlMinutes),
                CreatedByIp = Cut(client.Ip, MaxIpLength),
            });
            return code;
        }

        /// <summary>Kalan bekleme süresi (saniye); bekleme yoksa 0.</summary>
        private int CooldownRemaining(int playerId, byte purpose)
        {
            var cooldown = P.Int(ParameterKeys.Auth.CodeResendCooldownSeconds);
            if (cooldown <= 0) return 0;
            var last = _dal.LatestCode(playerId, purpose);
            if (last == null) return 0;
            var elapsed = (DateTime.UtcNow - last.CreatedAt).TotalSeconds;
            return elapsed >= cooldown ? 0 : (int)Math.Ceiling(cooldown - elapsed);
        }

        /// <summary>Kodu doğrular (deneme hakkı atomik düşülür). Başarılıysa kod tüketilir.</summary>
        private OperationResult<AuthCode> CheckCode(Player p, byte purpose, string code)
        {
            var row = _dal.LatestCode(p.PlayerId, purpose);
            var now = DateTime.UtcNow;
            if (row == null || row.ConsumedAt != null || row.ExpiresAt <= now)
                return OperationResult<AuthCode>.FieldError("code", ErrorCodes.Auth.CodeInvalid);

            var max = P.Int(ParameterKeys.Auth.CodeMaxAttempts);
            if (!_dal.TryUseAttempt(row.AuthCodeId, max))
                return OperationResult<AuthCode>.FieldError("code", ErrorCodes.Auth.CodeAttemptsExceeded);

            var expected = Encoding.ASCII.GetBytes(row.CodeHash);
            var actual = Encoding.ASCII.GetBytes(HashCode(p.PlayerId, purpose, code.Trim()));
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                return row.Attempts + 1 >= max
                    ? OperationResult<AuthCode>.FieldError("code", ErrorCodes.Auth.CodeAttemptsExceeded)
                    : OperationResult<AuthCode>.FieldError("code", ErrorCodes.Auth.CodeInvalid);
            }
            if (!_dal.ConsumeCode(row.AuthCodeId, now))
                return OperationResult<AuthCode>.FieldError("code", ErrorCodes.Auth.CodeInvalid);
            return OperationResult<AuthCode>.Ok(row);
        }

        /// <summary>Kullanıcı adı, e-posta ya da telefon → tek bir aktif hesap (birden fazla eşleşme = eşleşme yok).</summary>
        private Player? ResolveIdentifier(string identifier)
        {
            var id = identifier.Trim();
            var byName = _dal.FindByUserName(id);
            if (byName != null) return byName;
            if (id.Contains('@'))
            {
                var list = _dal.FindPlayersByEmail(id);
                return list.Count == 1 ? list[0] : null;
            }
            var digits = PhoneDigits.Last10(id);
            if (digits.Length == 10)
            {
                var list = _dal.FindPlayersByPhone(digits);
                return list.Count == 1 ? list[0] : null;
            }
            return null;
        }

        // ================================================================== Şifre sıfırlama

        public OperationResult<bool> ForgotPassword(string? identifier, string? channel, ClientInfo client)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return OperationResult<bool>.FieldError("identifier", ErrorCodes.Auth.IdentifierRequired);
            var ch = AuthChannels.Parse(channel);
            if (ch == null) return OperationResult<bool>.FieldError("channel", ErrorCodes.Auth.InvalidChannel);

            // Hesap var/yok, kanal adresi var/yok, bekleme süresi: hepsinde aynı yanıt (hesap listelenemez)
            var ok = OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Auth.ResetRequested));
            var player = ResolveIdentifier(identifier);
            if (player == null || player.IsDeleted)
            {
                _logger.LogInformation("Şifre sıfırlama: eşleşen hesap yok (kanal {Channel}).", ch);
                return ok;
            }

            var destination = ch == AuthChannels.Email ? player.Email?.Trim() : player.PhoneNumber?.Trim();
            if (string.IsNullOrEmpty(destination) || (ch == AuthChannels.Sms && PhoneDigits.Last10(destination).Length != 10)
                || (ch == AuthChannels.Email && !destination.Contains('@')))
            {
                _logger.LogInformation("Şifre sıfırlama: oyuncu {PlayerId} için {Channel} adresi yok.", player.PlayerId, ch);
                return ok;
            }
            if (CooldownRemaining(player.PlayerId, AuthCodePurposes.PasswordReset) > 0)
            {
                _logger.LogInformation("Şifre sıfırlama: oyuncu {PlayerId} bekleme süresinde, kod gönderilmedi.", player.PlayerId);
                return ok;
            }

            var ttl = P.Int(ParameterKeys.Auth.CodeTtlMinutes);
            var code = IssueCode(player, AuthCodePurposes.PasswordReset, ch.Value, destination, ttl, client);
            if (ch == AuthChannels.Email)
                _outbox.Enqueue(AuthTemplates.PasswordResetEmail(player, destination, code, ttl));
            else
                _outbox.Enqueue(AuthTemplates.PasswordResetSms(destination, code, ttl));
            _logger.LogInformation("Şifre sıfırlama kodu oluşturuldu: oyuncu {PlayerId}, kanal {Channel}, hedef {Destination}.",
                player.PlayerId, ch, ch == AuthChannels.Email ? Mask.Email(destination) : Mask.Phone(destination));
            return ok;
        }

        public OperationResult<Player> ResetPassword(string? identifier, string? code, string? newPassword)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return OperationResult<Player>.FieldError("identifier", ErrorCodes.Auth.IdentifierRequired);
            if (string.IsNullOrWhiteSpace(code))
                return OperationResult<Player>.FieldError("code", ErrorCodes.Auth.CodeRequired);
            var policy = PasswordPolicy.Validate(newPassword);
            if (policy != null) return OperationResult<Player>.FieldError("newPassword", policy.Code, policy.Args);

            var player = ResolveIdentifier(identifier);
            if (player == null || player.IsDeleted)
                return OperationResult<Player>.FieldError("code", ErrorCodes.Auth.CodeInvalid);

            var check = CheckCode(player, AuthCodePurposes.PasswordReset, code);
            if (!check.Success) return OperationResult<Player>.From(check);
            var row = check.Data!;

            var now = DateTime.UtcNow;
            _dal.SetPassword(player.PlayerId, PasswordHasher.Hash(newPassword!));
            // Kodu alabilmek adresin sahibi olmayı kanıtlar
            if (row.Channel == AuthChannels.Email) _dal.SetEmailConfirmed(player.PlayerId, row.Destination);
            else _dal.SetPhoneConfirmed(player.PlayerId, row.Destination);
            _dal.InvalidateCodes(player.PlayerId, AuthCodePurposes.PasswordReset, now);
            var revoked = _dal.RevokeAllForPlayer(player.PlayerId, RevokeReasons.PasswordReset, now);
            _logger.LogInformation("Şifre sıfırlandı: oyuncu {PlayerId}; {Count} oturum kapatıldı.", player.PlayerId, revoked);

            var fresh = _dal.FindPlayer(player.PlayerId) ?? player;
            NotifyPasswordChanged(fresh, now);
            return OperationResult<Player>.Ok(fresh, Msg.Get(MessageKeys.Auth.PasswordReset));
        }

        public OperationResult<Player> ChangePassword(int playerId, string? currentPassword, string? newPassword)
        {
            var player = _dal.FindPlayer(playerId);
            if (player == null || player.IsDeleted) return OperationResult<Player>.NotFound(ErrorCodes.Player.NotFound);
            if (!player.HasPassword) return OperationResult<Player>.BadRequest(ErrorCodes.Auth.PasswordNotSet);
            if (string.IsNullOrEmpty(currentPassword))
                return OperationResult<Player>.FieldError("currentPassword", ErrorCodes.Auth.PasswordRequired);
            if (!PasswordHasher.Verify(currentPassword, player.Password, out _))
                return OperationResult<Player>.FieldError("currentPassword", ErrorCodes.Auth.PasswordWrong);
            var policy = PasswordPolicy.Validate(newPassword);
            if (policy != null) return OperationResult<Player>.FieldError("newPassword", policy.Code, policy.Args);

            var now = DateTime.UtcNow;
            _dal.SetPassword(playerId, PasswordHasher.Hash(newPassword!));
            _dal.RevokeAllForPlayer(playerId, RevokeReasons.PasswordChanged, now);
            NotifyPasswordChanged(player, now);
            return OperationResult<Player>.Ok(_dal.FindPlayer(playerId) ?? player, Msg.Get(MessageKeys.Auth.PasswordChanged));
        }

        public void OnPasswordChangedExternally(int playerId)
        {
            var now = DateTime.UtcNow;
            _dal.RevokeAllForPlayer(playerId, RevokeReasons.PasswordChanged, now);
            var player = _dal.FindPlayer(playerId);
            if (player != null) NotifyPasswordChanged(player, now);
        }

        private void NotifyPasswordChanged(Player player, DateTime nowUtc)
        {
            var email = player.Email?.Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains('@')) return;
            _outbox.Enqueue(AuthTemplates.PasswordChangedEmail(player, email, nowUtc));
        }

        // ================================================================== E-posta doğrulama

        public OperationResult<int> SendEmailVerification(int playerId, bool enforceCooldown, ClientInfo client)
        {
            var player = _dal.FindPlayer(playerId);
            if (player == null || player.IsDeleted) return OperationResult<int>.NotFound(ErrorCodes.Player.NotFound);
            var email = player.Email?.Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains('@')) return OperationResult<int>.BadRequest(ErrorCodes.Auth.EmailMissing);
            if (player.EmailConfirmed) return OperationResult<int>.BadRequest(ErrorCodes.Auth.EmailAlreadyConfirmed);
            if (enforceCooldown)
            {
                var wait = CooldownRemaining(playerId, AuthCodePurposes.EmailVerification);
                if (wait > 0) return OperationResult<int>.BadRequest(ErrorCodes.Auth.CodeResendCooldown, wait);
            }

            var ttl = P.Int(ParameterKeys.Auth.EmailVerificationTtlMinutes);
            var code = IssueCode(player, AuthCodePurposes.EmailVerification, AuthChannels.Email, email, ttl, client);
            var link = string.IsNullOrWhiteSpace(_settings.FrontendBaseUrl)
                ? null
                : $"{_settings.FrontendBaseUrl.TrimEnd('/')}/verify-email?code={code}";
            _outbox.Enqueue(AuthTemplates.VerificationEmail(player, email, code, ttl, link));
            return OperationResult<int>.Ok(0, Msg.Get(MessageKeys.Auth.VerificationSent));
        }

        public OperationResult<Player> VerifyEmail(int playerId, string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return OperationResult<Player>.FieldError("code", ErrorCodes.Auth.CodeRequired);
            var player = _dal.FindPlayer(playerId);
            if (player == null || player.IsDeleted) return OperationResult<Player>.NotFound(ErrorCodes.Player.NotFound);
            if (player.EmailConfirmed) return OperationResult<Player>.BadRequest(ErrorCodes.Auth.EmailAlreadyConfirmed);

            var latest = _dal.LatestCode(playerId, AuthCodePurposes.EmailVerification);
            // Kod, adres değiştikten sonra geçersizdir
            if (latest != null && !string.Equals(latest.Destination, player.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
                return OperationResult<Player>.FieldError("code", ErrorCodes.Auth.CodeInvalid);

            var check = CheckCode(player, AuthCodePurposes.EmailVerification, code);
            if (!check.Success) return OperationResult<Player>.From(check);
            if (!_dal.SetEmailConfirmed(playerId, check.Data!.Destination))
                return OperationResult<Player>.FieldError("code", ErrorCodes.Auth.CodeInvalid);
            return OperationResult<Player>.Ok(_dal.FindPlayer(playerId)!, Msg.Get(MessageKeys.Auth.EmailVerified));
        }

        // ================================================================== KVKK

        public string CurrentTermsVersion => Parameters.Current.GetString(ParameterKeys.Legal.TermsVersion).Trim();

        public AppError? ValidateRegistrationConsent(string? termsVersion)
        {
            if (string.IsNullOrWhiteSpace(termsVersion)) return AppError.Bad(ErrorCodes.Auth.TermsRequired, CurrentTermsVersion);
            if (termsVersion.Trim() != CurrentTermsVersion) return AppError.Bad(ErrorCodes.Auth.TermsVersionMismatch, CurrentTermsVersion);
            return null;
        }

        public void ApplyRegistrationConsent(Player player, bool marketingConsent)
        {
            var now = DateTime.UtcNow;
            player.TermsVersion = CurrentTermsVersion;
            player.TermsAcceptedAt = now;
            player.PrivacyAcceptedAt = now;
            player.MarketingConsent = marketingConsent;
            player.MarketingConsentAt = now;
        }

        public OperationResult<Player> AcceptTerms(int playerId, string? termsVersion, bool marketingConsent)
        {
            var error = ValidateRegistrationConsent(termsVersion);
            if (error != null) return OperationResult<Player>.FieldError("termsVersion", error.Code, error.Args);
            var player = _dal.FindPlayer(playerId);
            if (player == null || player.IsDeleted) return OperationResult<Player>.NotFound(ErrorCodes.Player.NotFound);
            _dal.SetConsent(playerId, CurrentTermsVersion, marketingConsent, DateTime.UtcNow);
            return OperationResult<Player>.Ok(_dal.FindPlayer(playerId)!, Msg.Get(MessageKeys.Auth.TermsAccepted));
        }

        public bool ConsentRequired(Player player) => !string.Equals(player.TermsVersion?.Trim(), CurrentTermsVersion, StringComparison.Ordinal);

        /// <summary>Profil tamamlanmamış: telefon / soyad / yaş eksik (sosyal girişle açılan hesaplarda).</summary>
        public List<string> MissingProfileFields(Player player)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(player.PhoneNumber)) missing.Add("phoneNumber");
            if (string.IsNullOrWhiteSpace(player.PlayerSurname)) missing.Add("playerSurname");
            if (string.IsNullOrWhiteSpace(player.Email)) missing.Add("email");
            if (player.Age <= 0) missing.Add("age");
            return missing;
        }

        public Dictionary<string, object?> Export(int playerId) => _dal.Export(playerId);

        // ================================================================== Sosyal giriş bağlantıları

        public List<ExternalLogin> GetExternalLogins(int playerId) => _dal.GetLinks(playerId);

        public OperationResult<bool> UnlinkExternal(int playerId, string? provider)
        {
            var p = ExternalProviders.Normalize(provider);
            if (p == null) return OperationResult<bool>.BadRequest(ErrorCodes.Auth.ProviderUnknown);
            var player = _dal.FindPlayer(playerId);
            if (player == null || player.IsDeleted) return OperationResult<bool>.NotFound(ErrorCodes.Player.NotFound);
            var links = _dal.GetLinks(playerId);
            if (links.All(l => l.Provider != p)) return OperationResult<bool>.NotFound(ErrorCodes.Auth.ExternalLoginNotFound);
            // Tek giriş yolu bu bağlantıysa ve şifre yoksa kaldırılamaz (önce ForgotPassword ile şifre belirlenmeli)
            if (!player.HasPassword && links.Count <= 1)
                return OperationResult<bool>.BadRequest(ErrorCodes.Auth.ExternalLoginLastMethod);
            _dal.RemoveLink(playerId, p);
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Auth.ExternalUnlinked));
        }
    }
}
