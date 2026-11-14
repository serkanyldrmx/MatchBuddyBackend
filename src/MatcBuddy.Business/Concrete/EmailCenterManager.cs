using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Email;
using MatcBuddy.Core.Messaging;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
{
    public class EmailCenterManager : IEmailCenterService
    {
        // Kuyruk 1000 iletiyle sınırlı: kampanyalar turda en fazla bu kadar alıcıya parça parça gönderilir
        private const int BatchSize = 150;
        private const int OwnerMinDaysBetweenSends = 7;

        private readonly IEmailCenterDal _dal;
        private readonly IOutgoingMessageQueue _outbox;
        private readonly IEmailLinks _links;

        public EmailCenterManager(IEmailCenterDal dal, IOutgoingMessageQueue outbox, IEmailLinks links)
        {
            _dal = dal;
            _outbox = outbox;
            _links = links;
        }

        private string Site => _links.FrontendBaseUrl.TrimEnd('/');

        // ------------------------------------------------------------------ Varsayılan şablonlar

        private const string WelcomeBody =
            "# Hoş geldin, {{ad}}! ⚽\n\nMatcBuddy'ye katıldığın için çok mutluyuz. Artık halı saha maçlarını kurmak ve kadroyu tamamlamak çok kolay.\n\n## Neler yapabilirsin\n\n- **Maç kur**, arkadaşlarını tek dokunuşla kadroya çağır\n- **Eksik kadrolara katıl**, sahadaki boş yeri seç\n- **Anlaşmalı sahalardan** uygun saati ayırt\n- Maç sonrası oyuncuları **puanla**, profilini güçlendir\n\n> İpucu: Maç sayfasında \"Hatırlatma\"yı açarsan maçtan önce sana e-posta gönderelim.\n\nİyi maçlar!";

        private const string ReminderBody =
            "# {{mac}} {{kalan}} sonra başlıyor\n\nMerhaba {{ad}}, kramponlarını hazırla! Maç bilgilerin:\n\n| Tarih: {{tarih}}\n| Saat: {{saat}}\n| Saha: {{saha}}\n| Adres: {{adres}}\n\n> Gelemeyeceksen maç sayfasından haber ver; yerine yedekteki oyuncu alınsın.";

        // Eski sürüm metinler: hiç düzenlenmemiş sistem şablonları yeni tasarıma yükseltilir
        private static readonly Dictionary<string, (string subject, string body)> Legacy = new()
        {
            [EmailTemplateKeys.Welcome] = ("MatcBuddy'ye hoş geldin, {{ad}}!", "Merhaba {{ad}},\n\nMatcBuddy'ye hoş geldin! Artık maç kurabilir, eksik kadrolara katılabilir ve anlaşmalı halı sahalardan saat ayırtabilirsin.\n\nİlk adım olarak yakınındaki açık maçlara göz at ya da arkadaşlarınla kendi maçını kur. Maç saatinden önce sana hatırlatma e-postası da gönderebiliriz: maç sayfasında \"E-posta hatırlatıcı\"yı açman yeterli.\n\nİyi maçlar!"),
            [EmailTemplateKeys.MatchReminder] = ("{{mac}} {{kalan}} sonra başlıyor", "Merhaba {{ad}},\n\n\"{{mac}}\" maçın {{tarih}} saat {{saat}}'te {{saha}}'da başlıyor.\n\nAdres: {{adres}}\n\nGelemeyeceksen maç sayfasından haber ver; yerine yedekteki oyuncu alınsın."),
        };

        /// <summary>Eksik sistem şablonlarını ekler; hiç düzenlenmemiş eski sürümleri yeni metne yükseltir</summary>
        private void EnsureSystemTemplates()
        {
            var defaults = DefaultTemplates();
            _dal.EnsureDefaults(defaults);
            foreach (var d in defaults)
            {
                var t = _dal.SystemTemplate(d.Key);
                if (t == null || !Legacy.TryGetValue(d.Key, out var old) || t.Body.Replace("\r\n", "\n") != old.body) continue;
                t.Body = d.Body;
                if (t.Subject == old.subject) t.Subject = d.Subject;
                t.UpdatedAt = DateTime.Now;
                _dal.SaveTemplate(t);
            }
        }

        public List<EmailStarterDto> Starters(bool owner) => EmailStarters.For(owner).ToList();

        public static List<EmailTemplate> DefaultTemplates() => new()
        {
            new EmailTemplate
            {
                Key = EmailTemplateKeys.Welcome,
                Name = "Hoş geldin (kayıttan sonra)",
                Subject = "MatcBuddy'ye hoş geldin, {{ad}}!",
                Body = WelcomeBody,
                ButtonText = "Maçlara göz at",
                ButtonUrl = "{{site}}/home",
                IsSystem = true,
                IsActive = true,
                UpdatedAt = DateTime.Now,
            },
            new EmailTemplate
            {
                Key = EmailTemplateKeys.MatchReminder,
                Name = "Maç hatırlatma",
                Subject = "⏰ {{mac}} {{kalan}} sonra başlıyor",
                Body = ReminderBody,
                ButtonText = "Maçı aç",
                ButtonUrl = "{{link}}",
                IsSystem = true,
                IsActive = true,
                UpdatedAt = DateTime.Now,
            },
        };

        private static EmailTemplateDto ToDto(EmailTemplate t) => new()
        {
            EmailTemplateId = t.EmailTemplateId,
            Key = t.Key,
            Name = t.Name,
            Subject = t.Subject,
            Body = t.Body,
            ButtonText = t.ButtonText,
            ButtonUrl = t.ButtonUrl,
            IsSystem = t.IsSystem,
            IsActive = t.IsActive,
            UpdatedAt = t.UpdatedAt,
        };

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Konu / gövde / düğme doğrulaması (şablon ve kampanya ortak)</summary>
        private static AppError? ValidateContent(string? subject, string? body, string? buttonText, string? buttonUrl)
        {
            if (subject == null) return new AppError(OperationStatus.BadRequest, ErrorCodes.Validation.Required, "subject");
            if (subject.Length > 200) return new AppError(OperationStatus.BadRequest, ErrorCodes.Validation.TooLong, "subject", 200);
            if (body == null) return new AppError(OperationStatus.BadRequest, ErrorCodes.Validation.Required, "body");
            if (body.Length > 5000) return new AppError(OperationStatus.BadRequest, ErrorCodes.Validation.TooLong, "body", 5000);
            if (buttonText?.Length > 60) return new AppError(OperationStatus.BadRequest, ErrorCodes.Validation.TooLong, "buttonText", 60);
            if (buttonUrl != null && (buttonUrl.Length > 500 || !(buttonUrl.StartsWith("{{") || EmailLayout.IsHttp(buttonUrl))))
                return new AppError(OperationStatus.BadRequest, ErrorCodes.Email.InvalidUrl);
            return null;
        }

        // ------------------------------------------------------------------ Şablonlar

        public List<EmailTemplateDto> Templates(int? ownerPlayerId)
        {
            if (ownerPlayerId == null) EnsureSystemTemplates();
            return _dal.Templates(ownerPlayerId).Select(ToDto).ToList();
        }

        public OperationResult<int> SaveTemplate(int? ownerPlayerId, EmailTemplateInput input)
        {
            if (input == null) return OperationResult<int>.BadRequest(ErrorCodes.Validation.BodyRequired);
            var name = Clean(input.Name);
            var subject = Clean(input.Subject);
            var body = input.Body?.Trim();
            var buttonText = Clean(input.ButtonText);
            var buttonUrl = Clean(input.ButtonUrl);
            if (name == null) return OperationResult<int>.FieldError("name", ErrorCodes.Validation.Required, "name");
            if (name.Length > 100) return OperationResult<int>.FieldError("name", ErrorCodes.Validation.TooLong, "name", 100);
            var err = ValidateContent(subject, string.IsNullOrEmpty(body) ? null : body, buttonText, buttonUrl);
            if (err != null) return OperationResult<int>.BadRequest(err);

            EmailTemplate t;
            if (input.EmailTemplateId is int id && id > 0)
            {
                var existing = _dal.Template(id);
                if (existing == null || existing.OwnerPlayerId != ownerPlayerId) return OperationResult<int>.NotFound(ErrorCodes.Email.TemplateNotFound);
                t = existing;
            }
            else
            {
                t = new EmailTemplate { Key = "custom_" + Guid.NewGuid().ToString("N")[..12], OwnerPlayerId = ownerPlayerId };
            }
            t.Name = name;
            t.Subject = subject!;
            t.Body = body!;
            t.ButtonText = buttonText;
            t.ButtonUrl = buttonUrl;
            t.IsActive = input.IsActive;
            t.UpdatedAt = DateTime.Now;
            _dal.SaveTemplate(t);
            return OperationResult<int>.Ok(t.EmailTemplateId, Msg.Get(MessageKeys.Common.Updated));
        }

        public OperationResult<bool> DeleteTemplate(int? ownerPlayerId, int id)
        {
            var t = _dal.Template(id);
            if (t == null || t.OwnerPlayerId != ownerPlayerId) return OperationResult<bool>.NotFound(ErrorCodes.Email.TemplateNotFound);
            if (t.IsSystem) return OperationResult<bool>.BadRequest(ErrorCodes.Email.SystemTemplate);
            _dal.DeleteTemplate(id);
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Common.Deleted));
        }

        // ------------------------------------------------------------------ Kampanyalar

        private static EmailCampaignDto ToDto(EmailCampaign c) => new()
        {
            EmailCampaignId = c.EmailCampaignId,
            Subject = c.Subject,
            Body = c.Body,
            ButtonText = c.ButtonText,
            ButtonUrl = c.ButtonUrl,
            Audience = c.Audience,
            ScheduledAt = c.ScheduledAt,
            RepeatDays = c.RepeatDays,
            Status = c.Status,
            SentCount = c.SentCount,
            LastSentAt = c.LastSentAt,
            CreatedAt = c.CreatedAt,
        };

        public List<EmailCampaignDto> Campaigns(int? ownerPlayerId) => _dal.Campaigns(ownerPlayerId).Select(ToDto).ToList();

        private static bool AudienceAllowed(int? ownerPlayerId, int audience) => ownerPlayerId == null
            ? audience is EmailAudience.ConsentedPlayers or EmailAudience.Owners or EmailAudience.AllPlayersService
            : audience == EmailAudience.VenueCustomers;

        public int AudienceCount(int? ownerPlayerId, byte audience) =>
            AudienceAllowed(ownerPlayerId, audience) ? _dal.AudienceCount(audience, ownerPlayerId) : 0;

        public OperationResult<int> SaveCampaign(int actorPlayerId, int? ownerPlayerId, EmailCampaignInput input)
        {
            if (input == null) return OperationResult<int>.BadRequest(ErrorCodes.Validation.BodyRequired);
            var subject = Clean(input.Subject);
            var body = input.Body?.Trim();
            var buttonText = Clean(input.ButtonText);
            var buttonUrl = Clean(input.ButtonUrl);
            var err = ValidateContent(subject, string.IsNullOrEmpty(body) ? null : body, buttonText, buttonUrl);
            if (err != null) return OperationResult<int>.BadRequest(err);
            if (!AudienceAllowed(ownerPlayerId, input.Audience))
                return OperationResult<int>.FieldError("audience", ErrorCodes.Validation.InvalidValue, input.Audience, "audience");
            if (input.RepeatDays is int r && r is not (7 or 30))
                return OperationResult<int>.FieldError("repeatDays", ErrorCodes.Validation.InvalidValue, r, "repeatDays");
            var now = DateTime.Now;
            var when = input.ScheduledAt ?? now;
            if (when < now.AddMinutes(-5) || when > now.AddDays(180))
                return OperationResult<int>.FieldError("scheduledAt", ErrorCodes.Email.InvalidSchedule);

            // İşletmeler: haftada en fazla bir gönderim (müşterileri rahatsız etmesin)
            if (ownerPlayerId is int owner)
            {
                var last = _dal.LastOwnerSend(owner);
                if (last.HasValue && when < last.Value.AddDays(OwnerMinDaysBetweenSends))
                    return OperationResult<int>.BadRequest(ErrorCodes.Email.OwnerTooFrequent, OwnerMinDaysBetweenSends);
            }

            EmailCampaign c;
            if (input.EmailCampaignId is int id && id > 0)
            {
                var existing = _dal.Campaign(id);
                if (existing == null || existing.OwnerPlayerId != ownerPlayerId) return OperationResult<int>.NotFound(ErrorCodes.Email.CampaignNotFound);
                if (existing.Status is EmailCampaignStatus.Sending) return OperationResult<int>.BadRequest(ErrorCodes.Email.CampaignLocked);
                c = existing;
            }
            else
            {
                c = new EmailCampaign { CreatedByPlayerId = actorPlayerId, OwnerPlayerId = ownerPlayerId, CreatedAt = now };
            }
            c.Subject = subject!;
            c.Body = body!;
            c.ButtonText = buttonText;
            c.ButtonUrl = buttonUrl;
            c.Audience = (byte)input.Audience;
            c.ScheduledAt = when;
            c.RepeatDays = input.RepeatDays;
            c.Status = EmailCampaignStatus.Scheduled;
            c.Cursor = 0;
            _dal.SaveCampaign(c);
            return OperationResult<int>.Ok(c.EmailCampaignId, Msg.Get(MessageKeys.Email.CampaignSaved));
        }

        public OperationResult<bool> CancelCampaign(int? ownerPlayerId, int id)
        {
            var c = _dal.Campaign(id);
            if (c == null || c.OwnerPlayerId != ownerPlayerId) return OperationResult<bool>.NotFound(ErrorCodes.Email.CampaignNotFound);
            c.Status = EmailCampaignStatus.Cancelled;
            _dal.SaveCampaign(c);
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Common.Updated));
        }

        // ------------------------------------------------------------------ Önizleme / test

        private Dictionary<string, string?> SampleValues(Player? p)
        {
            var v = EmailLayout.PersonValues(p?.PlayerName, p?.PlayerSurname, p?.UserName, Site);
            v["mac"] = "Çarşamba akşam maçı";
            v["tarih"] = DateTime.Now.AddDays(2).ToString("dd.MM.yyyy");
            v["saat"] = "21:00";
            v["saha"] = "Yeşil Vadi Halı Saha";
            v["adres"] = "Kadıköy, İstanbul";
            v["kalan"] = "3 saat";
            v["link"] = $"{Site}/home";
            v["tesis"] = (p == null ? null : _dal.OwnerVenueName(p.PlayerId)) ?? "Yeşil Vadi Halı Saha";
            return v;
        }

        private EmailMessage Render(string to, string? toName, IReadOnlyDictionary<string, string?> values, string subject, string body,
            string? buttonText, string? buttonUrl, string? unsubscribe, string? senderNote = null) =>
            EmailLayout.Build(to, toName, EmailLayout.Fill(subject, values), EmailLayout.Fill(body, values),
                EmailLayout.Fill(buttonText, values), EmailLayout.Fill(buttonUrl, values), unsubscribe, Site, senderNote);

        public EmailPreviewDto Preview(int actorPlayerId, EmailPreviewInput input)
        {
            var values = SampleValues(_dal.Player(actorPlayerId));
            var m = Render("ornek@matcbuddy.com", null, values, input?.Subject ?? "", input?.Body ?? "", input?.ButtonText, input?.ButtonUrl, null);
            return new EmailPreviewDto { Subject = m.Subject, Html = m.HtmlBody ?? m.TextBody };
        }

        public OperationResult<bool> SendTest(int actorPlayerId, EmailPreviewInput input)
        {
            var me = _dal.Player(actorPlayerId);
            if (me == null || string.IsNullOrWhiteSpace(me.Email)) return OperationResult<bool>.BadRequest(ErrorCodes.Email.NoEmail);
            var err = ValidateContent(Clean(input?.Subject), Clean(input?.Body), Clean(input?.ButtonText), Clean(input?.ButtonUrl));
            if (err != null) return OperationResult<bool>.BadRequest(err);
            var m = Render(me.Email!, $"{me.PlayerName} {me.PlayerSurname}".Trim(), SampleValues(me), "[Test] " + input!.Subject, input.Body!,
                input.ButtonText, input.ButtonUrl, null);
            _outbox.Enqueue(m);
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Email.TestSent, me.Email!));
        }

        // ------------------------------------------------------------------ Hoş geldin

        public void SendWelcome(int playerId)
        {
            var p = _dal.Player(playerId);
            if (p == null || p.IsDeleted || string.IsNullOrWhiteSpace(p.Email)) return;
            EnsureSystemTemplates();
            var t = _dal.SystemTemplate(EmailTemplateKeys.Welcome);
            if (t == null || !t.IsActive) return;
            var values = EmailLayout.PersonValues(p.PlayerName, p.PlayerSurname, p.UserName, Site);
            _outbox.Enqueue(Render(p.Email!, $"{p.PlayerName} {p.PlayerSurname}".Trim(), values, t.Subject, t.Body, t.ButtonText, t.ButtonUrl, null));
        }

        // ------------------------------------------------------------------ Maç hatırlatma

        public MatchEmailRemindersDto GetReminders(int matchId, int playerId)
        {
            var p = _dal.Player(playerId);
            return new MatchEmailRemindersDto
            {
                Minutes = _dal.ReminderMinutes(matchId, playerId),
                Options = EmailReminderOptions.Minutes.ToList(),
                HasEmail = !string.IsNullOrWhiteSpace(p?.Email),
            };
        }

        public OperationResult<bool> SetReminders(int playerId, MatchEmailRemindersInput input)
        {
            if (input == null) return OperationResult<bool>.BadRequest(ErrorCodes.Validation.BodyRequired);
            var minutes = (input.Minutes ?? new List<int>()).Distinct().ToList();
            if (minutes.Any(m => !EmailReminderOptions.Minutes.Contains(m)))
                return OperationResult<bool>.FieldError("minutes", ErrorCodes.Validation.InvalidValue, string.Join(",", minutes), "minutes");
            if (minutes.Count > 0)
            {
                var p = _dal.Player(playerId);
                if (p == null || string.IsNullOrWhiteSpace(p.Email)) return OperationResult<bool>.BadRequest(ErrorCodes.Email.NoEmail);
                if (!_dal.InMatch(input.MatchId, playerId)) return OperationResult<bool>.BadRequest(ErrorCodes.Email.NotInMatch);
            }
            _dal.SetReminders(input.MatchId, playerId, minutes);
            return OperationResult<bool>.Ok(true, Msg.Get(minutes.Count > 0 ? MessageKeys.Email.RemindersOn : MessageKeys.Email.RemindersOff));
        }

        // ------------------------------------------------------------------ Zamanlayıcı

        public int RunDue()
        {
            var now = DateTime.Now;
            var sent = 0;
            EnsureSystemTemplates();

            // 1) Maç hatırlatmaları
            var reminder = _dal.SystemTemplate(EmailTemplateKeys.MatchReminder);
            foreach (var r in _dal.DueReminders(now, 300))
            {
                if (reminder is { IsActive: true })
                {
                    var values = EmailLayout.PersonValues(r.Name, r.Surname, r.UserName, Site);
                    values["mac"] = r.MatchName;
                    values["tarih"] = r.MatchDate.ToString("dd.MM.yyyy");
                    values["saat"] = r.MatchDate.ToString("HH:mm");
                    values["saha"] = r.StadiumName ?? "sahada";
                    values["adres"] = r.Address ?? "-";
                    values["kalan"] = EmailLayout.Remaining(Math.Max(1, (int)Math.Round((r.MatchDate - now).TotalMinutes / 5.0) * 5));
                    values["link"] = $"{Site}/match-details/{r.MatchId}";
                    _outbox.Enqueue(Render(r.Email, $"{r.Name} {r.Surname}".Trim(), values, reminder.Subject, reminder.Body,
                        reminder.ButtonText, reminder.ButtonUrl, null));
                    sent++;
                }
                _dal.MarkReminderSent(r.ReminderId, now);
            }

            // 2) Kampanyalar (parça parça)
            foreach (var c in _dal.DueCampaigns(now))
            {
                var batch = _dal.Recipients(c.Audience, c.OwnerPlayerId, c.Cursor, BatchSize);
                // Pazarlama kitlelerine abonelikten çıkma bağlantısı
                var marketing = c.Audience is EmailAudience.ConsentedPlayers or EmailAudience.VenueCustomers;
                var venue = c.OwnerPlayerId is int ownerId ? _dal.OwnerVenueName(ownerId) : null;
                var note = venue == null ? null : $"Bu e-postayı {venue} gönderdi: tesiste oynadığın ve kampanya e-postalarına izin verdiğin için aldın.";
                foreach (var to in batch)
                {
                    var values = EmailLayout.PersonValues(to.Name, to.Surname, to.UserName, Site);
                    values["tesis"] = venue ?? "MatcBuddy";
                    _outbox.Enqueue(Render(to.Email, $"{to.Name} {to.Surname}".Trim(), values, c.Subject, c.Body, c.ButtonText, c.ButtonUrl,
                        marketing ? _links.UnsubscribeUrl(to.PlayerId) : null, note));
                }
                sent += batch.Count;
                c.SentCount += batch.Count;
                if (batch.Count == BatchSize)
                {
                    c.Status = EmailCampaignStatus.Sending;
                    c.Cursor = batch[^1].PlayerId;
                }
                else
                {
                    c.LastSentAt = now;
                    c.Cursor = 0;
                    if (c.RepeatDays is int days)
                    {
                        while (c.ScheduledAt <= now) c.ScheduledAt = c.ScheduledAt.AddDays(days);
                        c.Status = EmailCampaignStatus.Scheduled;
                    }
                    else c.Status = EmailCampaignStatus.Done;
                }
                _dal.SaveCampaign(c);
            }
            return sent;
        }

        public OperationResult<bool> Unsubscribe(int playerId)
        {
            if (_dal.Player(playerId) == null) return OperationResult<bool>.NotFound(ErrorCodes.Player.NotFound);
            _dal.SetMarketingConsent(playerId, false);
            return OperationResult<bool>.Ok(true);
        }
    }
}
