using System.Globalization;
using System.Net;
using MatcBuddy.Core.Messaging;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Auth
{
    /// <summary>
    /// Kimlik e-postaları / SMS'leri. Tüm metinler Messages.resx'ten (MAIL_* / SMS_*), istek dilinde;
    /// HTML yalnızca basit, markalı bir yerleşim (tablo tabanlı, satır içi stil — e-posta istemcileri için).
    /// </summary>
    public static class AuthTemplates
    {

        public static EmailMessage PasswordResetEmail(Player p, string email, string code, int minutes) =>
            Build(p, email, MessageKeys.Auth.MailResetSubject, Msg.Get(MessageKeys.Auth.MailResetBody, minutes), code, null, null, true);

        public static SmsMessage PasswordResetSms(string phone, string code, int minutes) =>
            new(phone, Msg.Get(MessageKeys.Auth.SmsResetBody, code, minutes));

        public static EmailMessage VerificationEmail(Player p, string email, string code, int minutes, string? link) =>
            Build(p, email, MessageKeys.Auth.MailVerifySubject, Msg.Get(MessageKeys.Auth.MailVerifyBody, minutes), code,
                link, link == null ? null : Msg.Get(MessageKeys.Auth.MailVerifyButton), true);

        public static EmailMessage PasswordChangedEmail(Player p, string email, DateTime whenUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(whenUtc, TurkeyTime);
            var body = Msg.Get(MessageKeys.Auth.MailPasswordChangedBody, local.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture));
            return Build(p, email, MessageKeys.Auth.MailPasswordChangedSubject, body, null, null, null, false);
        }

        private static TimeZoneInfo TurkeyTime
        {
            get
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
                catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
            }
        }

        private static EmailMessage Build(Player p, string to, string subjectKey, string body, string? code,
            string? link, string? linkText, bool ignoreNotice)
        {
            var name = string.IsNullOrWhiteSpace(p.PlayerName) ? p.UserName ?? string.Empty : p.PlayerName!;
            var subject = Msg.Get(subjectKey);
            var greeting = Msg.Get(MessageKeys.Auth.MailGreeting, name);
            var ignore = ignoreNotice ? Msg.Get(MessageKeys.Auth.MailIgnoreNotice) : null;
            var footer = Msg.Get(MessageKeys.Auth.MailFooter);

            var text = string.Join("\n\n", new[]
            {
                greeting, body, code, link, ignore, "— " + footer,
            }.Where(s => !string.IsNullOrEmpty(s)));

            string E(string s) => WebUtility.HtmlEncode(s);
            // Uygulama görünümündeki ortak kabuk (EmailLayout.Shell); kod büyük, okunaklı bir kutuda
            var content = $@"<p style=""margin:0 0 14px;font-size:16px;font-weight:600"">{E(greeting)}</p>
<p style=""margin:0 0 18px"">{E(body)}</p>
{(code == null ? "" : $@"<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" align=""center"" style=""margin:6px auto 22px""><tr><td style=""background:#070b12;border-radius:14px;padding:16px 26px;font-family:Consolas,'Courier New',monospace;font-size:32px;letter-spacing:10px;font-weight:800;color:#c6f432;text-align:center"">{E(code)}</td></tr></table>")}
{(link == null ? "" : Email.EmailLayout.Button(linkText ?? link, link))}
{(ignore == null ? "" : $@"<p style=""margin:18px 0 0;padding-top:14px;border-top:1px solid #e2e8dc;color:#6b7280;font-size:13px"">{E(ignore)}</p>")}";
            var html = Email.EmailLayout.Shell(subject, body, subject, content, $@"<p style=""margin:0"">{E(footer)}</p>");

            return new EmailMessage(to, $"{p.PlayerName} {p.PlayerSurname}".Trim(), subject, text, html);
        }
    }
}
