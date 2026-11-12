using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using MatcBuddy.Business.Auth;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.Api.Services
{
    /// <summary>
    /// Maç davet linki (docs/engagement-api.md §3). Her maça ilk paylaşımda 8 karakterlik bir kod verilir:
    /// {Share:BaseUrl}/m/{kod}. Bu adres WhatsApp gibi uygulamalarda önizleme kartı (Open Graph) gösterir ve
    /// insanları ön yüzdeki /davet/{kod} sayfasına yönlendirir. Kodu bilen herkes maçın özetini görebilir.
    /// </summary>
    public class ShareService
    {
        private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789"; // karışan harfler (i, l, o, 0, 1) yok
        private readonly string _frontendBase;
        private readonly string _shareBase;

        public ShareService(AuthSettings auth, IConfiguration configuration)
        {
            _frontendBase = auth.FrontendBaseUrl.TrimEnd('/');
            // Önizleme kartı için /m/{kod} isteklerini API'ye yönlendiren alan adı (Vercel: rewrites). Yoksa ön yüz adresi.
            _shareBase = (configuration["Share:BaseUrl"] ?? _frontendBase).TrimEnd('/');
        }

        public string ShareUrl(string code) => $"{_shareBase}/m/{code}";
        public string LandingUrl(string code) => $"{_frontendBase}/davet/{code}";

        private static string NewCode()
        {
            Span<char> chars = stackalloc char[8];
            for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            return new string(chars);
        }

        private static string VenueText(Stadium? s) =>
            s == null ? string.Empty
                : string.Join(", ", new[] { s.StadiumName, s.District ?? s.City }.Where(x => !string.IsNullOrWhiteSpace(x)));

        /// <summary>Maçın davet linki (yoksa oluşturur) ve WhatsApp'a hazır metin.</summary>
        public OperationResult<ShareLinkDto> GetOrCreate(int matchId)
        {
            using var context = new MatcBuddyContext();
            var match = context.Matchs.FirstOrDefault(m => m.MatchId == matchId);
            if (match == null) return OperationResult<ShareLinkDto>.NotFound(ErrorCodes.Match.NotFound);
            if (string.IsNullOrEmpty(match.ShareCode))
            {
                string code;
                do code = NewCode();
                while (context.Matchs.Any(m => m.ShareCode == code));
                match.ShareCode = code;
                context.SaveChanges();
            }
            var invite = Build(context, match);
            var url = ShareUrl(match.ShareCode!);
            var stadium = context.Stadiums.AsNoTracking().FirstOrDefault(s => s.StadiumId == match.StadiumId);
            var text = invite.Missing > 0 && !invite.IsPast
                ? Msg.Get(MessageKeys.Share.Text, match.MatchName, match.MatchDate, VenueText(stadium), invite.Missing, url)
                : Msg.Get(MessageKeys.Share.TextFull, match.MatchName, match.MatchDate, VenueText(stadium), url);
            return OperationResult<ShareLinkDto>.Ok(new ShareLinkDto { Code = match.ShareCode!, Url = url, Text = text.ReplaceLineEndings("\n") });
        }

        public int? MatchIdOf(string? code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 12) return null;
            using var context = new MatcBuddyContext();
            var c = code.Trim().ToLowerInvariant();
            return context.Matchs.AsNoTracking().Where(m => m.ShareCode == c).Select(m => (int?)m.MatchId).FirstOrDefault();
        }

        public PublicMatchInviteDto? Invite(string? code)
        {
            var id = MatchIdOf(code);
            if (id == null) return null;
            using var context = new MatcBuddyContext();
            var match = context.Matchs.AsNoTracking().First(m => m.MatchId == id);
            return Build(context, match);
        }

        private static PublicMatchInviteDto Build(MatcBuddyContext context, Match match)
        {
            var stadium = context.Stadiums.AsNoTracking().FirstOrDefault(s => s.StadiumId == match.StadiumId);
            var going = (from p in context.MatchParticipants.AsNoTracking()
                         join pl in context.Players.AsNoTracking() on p.PlayerId equals pl.PlayerId
                         where p.MatchId == match.MatchId && p.Status == ParticipantStatus.Going && !pl.IsDeleted
                         orderby p.JoinedAt
                         select pl).ToList();
            var guestCount = context.MatchGuests.AsNoTracking().Count(g => g.MatchId == match.MatchId);
            var host = match.CreatedByPlayerId.HasValue
                ? context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == match.CreatedByPlayerId && !p.IsDeleted)
                : null;
            var teams = context.Teams.AsNoTracking()
                .Where(t => t.TeamId == match.HomeTeamId || t.TeamId == match.AwayTeamId)
                .Select(t => new { t.TeamId, t.TeamName }).ToList();

            var lookup = VenueRules.MatchVenueLookup.Load(context, new[] { match });
            var fields = new MatchVenueFieldsDto();
            lookup.Fill(fields, match);

            var goingCount = going.Count + guestCount;
            var end = (match.MatchDate ?? DateTime.MinValue).AddMinutes(match.DurationMinutes <= 0 ? 60 : match.DurationMinutes);
            return new PublicMatchInviteDto
            {
                Code = match.ShareCode ?? string.Empty,
                MatchId = match.MatchId,
                MatchName = match.MatchName,
                MatchDate = match.MatchDate,
                DurationMinutes = match.DurationMinutes <= 0 ? 60 : match.DurationMinutes,
                VenueName = stadium?.StadiumName,
                District = stadium?.District,
                City = stadium?.City,
                IsExternal = stadium?.IsExternal ?? false,
                Capacity = match.UserCount,
                GoingCount = goingCount,
                Missing = Math.Max(match.UserCount - goingCount, 0),
                PerPlayer = fields.PerPlayer,
                HostName = host == null ? null : string.Join(' ', new[] { host.PlayerName, host.PlayerSurname }.Where(s => !string.IsNullOrWhiteSpace(s))),
                HostPhotoUrl = host?.ProfilePictureUrl,
                // Herkese açık sayfa: yalnızca ad ve fotoğraf (kişisel bilgiler gitmez)
                Participants = going.Take(10).Select(p => new PlayerSummaryDto
                {
                    PlayerId = p.PlayerId,
                    PlayerName = p.PlayerName,
                    PlayerSurname = string.IsNullOrEmpty(p.PlayerSurname) ? null : p.PlayerSurname[..1] + ".",
                    ProfilePictureUrl = p.ProfilePictureUrl,
                }).ToList(),
                HomeTeam = teams.FirstOrDefault(t => t.TeamId == match.HomeTeamId)?.TeamName,
                AwayTeam = teams.FirstOrDefault(t => t.TeamId == match.AwayTeamId)?.TeamName,
                IsPast = end < DateTime.Now,
                IsClosed = match.IsActive == MatchStatus.Rejected,
            };
        }

        /// <summary>
        /// Önizleme kartı sayfası: Open Graph etiketleri (WhatsApp, Telegram, X…) + insanlar için ön yüze yönlendirme.
        /// </summary>
        public string PreviewHtml(string code, PublicMatchInviteDto? invite, string imageUrl)
        {
            static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
            var landing = LandingUrl(code);
            string title, description;
            if (invite == null)
            {
                title = Msg.Get(MessageKeys.Share.NotFoundTitle);
                description = Msg.Get(ErrorCodes.Share.NotFound);
                landing = _frontendBase;
            }
            else
            {
                var venue = string.Join(", ", new[] { invite.VenueName, invite.District ?? invite.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
                title = invite.Missing > 0 && !invite.IsPast && !invite.IsClosed
                    ? Msg.Get(MessageKeys.Share.OgTitle, invite.MatchName, invite.Missing)
                    : Msg.Get(MessageKeys.Share.OgTitleFull, invite.MatchName);
                description = Msg.Get(MessageKeys.Share.OgDescription, invite.MatchDate, venue);
                if (invite.PerPlayer.HasValue)
                    description += " · " + Msg.Get(MessageKeys.Share.OgPerPlayer, invite.PerPlayer.Value);
            }
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return $"""
                <!doctype html>
                <html lang="{E(lang)}">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{E(title)}</title>
                <meta name="description" content="{E(description)}">
                <meta property="og:type" content="website">
                <meta property="og:site_name" content="MatcBuddy">
                <meta property="og:title" content="{E(title)}">
                <meta property="og:description" content="{E(description)}">
                <meta property="og:url" content="{E(ShareUrl(code))}">
                <meta property="og:image" content="{E(imageUrl)}">
                <meta name="twitter:card" content="summary">
                <meta http-equiv="refresh" content="0; url={E(landing)}">
                <link rel="canonical" href="{E(landing)}">
                </head>
                <body style="font-family:system-ui,sans-serif;background:#070b12;color:#e8eef6;padding:32px">
                <p>{E(title)}</p>
                <p><a style="color:#c6f432" href="{E(landing)}">MatcBuddy</a></p>
                <script>location.replace({System.Text.Json.JsonSerializer.Serialize(landing)});</script>
                </body>
                </html>
                """;
        }

        public string DefaultImageUrl => $"{_frontendBase}/og-match.png";
    }
}
