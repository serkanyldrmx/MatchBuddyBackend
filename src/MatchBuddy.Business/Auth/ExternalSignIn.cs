using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MatchBuddy.Business.Security;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Auth
{
    /// <summary>Sağlayıcının doğruladığı kimlik.</summary>
    public sealed record ExternalIdentity(
        string Provider,
        string ProviderKey,
        string? Email,
        bool EmailVerified,
        string? Name,
        string? GivenName = null,
        string? FamilyName = null,
        string? PictureUrl = null);

    /// <summary>Google id_token / Facebook access token doğrulayıcı. Uygulamaları API katmanında.</summary>
    public interface IExternalTokenValidator
    {
        string Provider { get; }
        bool IsConfigured { get; }
        /// <summary>Hata: AUTH_EXTERNAL_TOKEN_INVALID (401) veya AUTH_PROVIDER_UNAVAILABLE (502, BadRequest durumunda kod ile ayırt edilir).</summary>
        Task<OperationResult<ExternalIdentity>> ValidateAsync(string token, CancellationToken ct = default);
    }

    public sealed record ExternalResolution(Player Player, bool IsNew, bool Linked, ExternalIdentity Identity);

    /// <summary>
    /// Hesap eşleştirme kuralı (docs/auth-api.md §Sosyal giriş):
    /// 1) (sağlayıcı, anahtar) bağlantısı varsa o oyuncu;
    /// 2) sağlayıcı e-postayı doğruladıysa ve bu e-postaya sahip, e-postası yerelde de doğrulanmış TEK bir oyuncu varsa → bağla;
    /// 3) aksi halde yeni oyuncu (benzersiz kullanıcı adı, kullanılamaz rastgele şifre, HasPassword=false).
    /// </summary>
    public sealed class ExternalAccountResolver
    {
        private readonly IExternalAccountStore _store;

        public ExternalAccountResolver(IExternalAccountStore store) => _store = store;

        public ExternalResolution Resolve(ExternalIdentity id, DateTime nowUtc)
        {
            var link = _store.FindLink(id.Provider, id.ProviderKey);
            if (link != null)
            {
                var linked = _store.FindPlayer(link.PlayerId);
                if (linked != null && !linked.IsDeleted)
                {
                    _store.TouchLink(link.ExternalLoginId, nowUtc);
                    return new ExternalResolution(linked, false, false, id);
                }
                _store.RemoveLinkById(link.ExternalLoginId); // silinmiş hesaba ait eski bağlantı
            }

            var email = string.IsNullOrWhiteSpace(id.Email) ? null : id.Email.Trim();
            if (email != null && id.EmailVerified)
            {
                var confirmed = _store.FindPlayersByEmail(email).Where(p => p.EmailConfirmed && !p.IsDeleted).ToList();
                if (confirmed.Count == 1 && !_store.HasLink(confirmed[0].PlayerId, id.Provider))
                {
                    _store.AddLink(NewLink(id, confirmed[0].PlayerId, nowUtc));
                    return new ExternalResolution(confirmed[0], false, true, id);
                }
            }

            var player = new Player
            {
                PlayerName = Trunc(FirstNonEmpty(id.GivenName, id.Name, Msg.GetDefault(MessageKeys.Auth.DefaultPlayerName)), 100),
                PlayerSurname = Trunc(id.FamilyName ?? (id.GivenName == null ? string.Empty : LastWord(id.Name)), 100),
                UserName = UserNameGenerator.Generate(_store.UserNameExists, id.Name, id.GivenName, email),
                Password = PasswordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                HasPassword = false,
                Email = email ?? string.Empty,
                EmailConfirmed = email != null && id.EmailVerified,
                PhoneNumber = string.Empty,
                Address = string.Empty,
                ProfilePictureUrl = string.Empty,
                Role = PlayerRoles.Player,
                IsAdmin = 0,
            };
            _store.CreatePlayerWithLink(player, NewLink(id, 0, nowUtc));
            return new ExternalResolution(player, true, false, id);
        }

        private static ExternalLogin NewLink(ExternalIdentity id, int playerId, DateTime nowUtc) => new()
        {
            Provider = id.Provider,
            ProviderKey = id.ProviderKey,
            PlayerId = playerId,
            Email = id.Email is { Length: > 0 } e ? Trunc(e, 256) : null,
            DisplayName = id.Name is { Length: > 0 } n ? Trunc(n, 200) : null,
            CreatedAt = nowUtc,
            LastUsedAt = nowUtc,
        };

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

        private static string LastWord(string? name)
        {
            var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[^1] : string.Empty;
        }

        private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];
    }

    /// <summary>Addan benzersiz kullanıcı adı: Türkçe karakterler sadeleştirilir, [a-z0-9_], en fazla 25 karakter.</summary>
    public static class UserNameGenerator
    {
        public const int MaxLength = 25;
        private const int BaseMax = 18;

        public static string Slug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var s = value.Trim()
                .Replace('ı', 'i').Replace('İ', 'i').Replace('ğ', 'g').Replace('Ğ', 'g').Replace('ş', 's').Replace('Ş', 's')
                .Replace('ç', 'c').Replace('Ç', 'c').Replace('ö', 'o').Replace('Ö', 'o').Replace('ü', 'u').Replace('Ü', 'u')
                .Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                var c = char.ToLowerInvariant(ch);
                if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
                else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
            }
            var slug = sb.ToString().Trim('_');
            return slug.Length > BaseMax ? slug[..BaseMax].TrimEnd('_') : slug;
        }

        public static string Generate(Func<string, bool> exists, string? name, string? givenName, string? email)
        {
            var stem = Slug(name);
            if (stem.Length < 3) stem = Slug(givenName);
            if (stem.Length < 3 && email != null && email.Contains('@')) stem = Slug(email[..email.IndexOf('@')]);
            if (stem.Length < 3) stem = "oyuncu";

            if (!exists(stem)) return stem;
            for (var i = 0; i < 20; i++)
            {
                var candidate = $"{stem}_{RandomNumberGenerator.GetInt32(1000, 10000)}";
                if (!exists(candidate)) return candidate;
            }
            return $"{stem[..Math.Min(stem.Length, 12)]}_{Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant()}";
        }
    }

    /// <summary>Sosyal giriş: token doğrula → hesabı eşleştir/oluştur.</summary>
    public sealed class ExternalSignInService
    {
        private readonly IEnumerable<IExternalTokenValidator> _validators;
        private readonly ExternalAccountResolver _resolver;

        public ExternalSignInService(IEnumerable<IExternalTokenValidator> validators, IExternalAccountStore store)
        {
            _validators = validators;
            _resolver = new ExternalAccountResolver(store);
        }

        public IExternalTokenValidator? Validator(string provider) =>
            _validators.FirstOrDefault(v => v.Provider == provider);

        public async Task<OperationResult<ExternalResolution>> SignInAsync(string provider, string? token, CancellationToken ct = default)
        {
            var validator = Validator(provider);
            if (validator == null || !validator.IsConfigured)
                return OperationResult<ExternalResolution>.BadRequest(ErrorCodes.Auth.ProviderNotConfigured, provider);
            if (string.IsNullOrWhiteSpace(token))
                return OperationResult<ExternalResolution>.FieldError("token", ErrorCodes.Auth.ExternalTokenRequired);

            var identity = await validator.ValidateAsync(token.Trim(), ct);
            if (!identity.Success || identity.Data == null)
                return OperationResult<ExternalResolution>.From(identity);
            if (identity.Data.Provider != provider || string.IsNullOrWhiteSpace(identity.Data.ProviderKey))
                return OperationResult<ExternalResolution>.Fail(OperationStatus.Unauthorized, ErrorCodes.Auth.ExternalTokenInvalid);

            return OperationResult<ExternalResolution>.Ok(_resolver.Resolve(identity.Data, DateTime.UtcNow));
        }
    }
}
