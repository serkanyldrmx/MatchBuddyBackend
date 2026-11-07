using System.Globalization;
using System.Net;
using MatchBuddy.Core.Messaging;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Auth
{
    /// <summary>
    /// Kimlik e-postaları / SMS'leri. Tüm metinler Messages.resx'ten (MAIL_* / SMS_*), istek dilinde;
    /// HTML yalnızca basit, markalı bir yerleşim (tablo tabanlı, satır içi stil — e-posta istemcileri için).
    /// </summary>
    public static class AuthTemplates
    {
        private const string Brand = "MatchBuddy";
        private const string BrandColor = "#16a34a";

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
            var html = $@"<!doctype html>
<html lang=""{E(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)}""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><title>{E(subject)}</title></head>
<body style=""margin:0;padding:0;background:#f3f4f6;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#111827"">
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:#f3f4f6;padding:24px 0""><tr><td align=""center"">
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width:520px;background:#ffffff;border-radius:12px;overflow:hidden"">
<tr><td style=""background:{BrandColor};padding:18px 24px;color:#ffffff;font-size:20px;font-weight:700"">&#9917; {Brand}</td></tr>
<tr><td style=""padding:24px;font-size:15px;line-height:1.6"">
<p style=""margin:0 0 12px"">{E(greeting)}</p>
<p style=""margin:0 0 16px"">{E(body)}</p>
{(code == null ? "" : $@"<div style=""margin:8px 0 20px;text-align:center""><span style=""display:inline-block;padding:12px 20px;border-radius:8px;background:#f0fdf4;border:1px solid #bbf7d0;font-size:28px;letter-spacing:6px;font-weight:700;font-family:Consolas,monospace"">{E(code)}</span></div>")}
{(link == null ? "" : $@"<p style=""margin:0 0 20px;text-align:center""><a href=""{E(link)}"" style=""display:inline-block;padding:10px 18px;border-radius:8px;background:{BrandColor};color:#ffffff;text-decoration:none;font-weight:600"">{E(linkText ?? link)}</a></p>")}
{(ignore == null ? "" : $@"<p style=""margin:0;color:#6b7280;font-size:13px"">{E(ignore)}</p>")}
</td></tr>
<tr><td style=""padding:14px 24px;background:#f9fafb;color:#6b7280;font-size:12px"">{E(footer)}</td></tr>
</table></td></tr></table></body></html>";

            return new EmailMessage(to, $"{p.PlayerName} {p.PlayerSurname}".Trim(), subject, text, html);
        }
    }
}
