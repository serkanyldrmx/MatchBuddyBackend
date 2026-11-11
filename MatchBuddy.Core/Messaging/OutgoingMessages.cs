namespace MatchBuddy.Core.Messaging
{
    /// <summary>Gönderilecek e-posta. HtmlBody boşsa yalnızca düz metin gönderilir.</summary>
    public sealed record EmailMessage(string To, string? ToName, string Subject, string TextBody, string? HtmlBody = null);

    /// <summary>Gönderilecek SMS. Telefon serbest biçimde olabilir; gönderici normalize eder.</summary>
    public sealed record SmsMessage(string To, string Text);

    /// <summary>E-posta gönderici (SMTP / geliştirmede outbox dosyası). docs/auth-api.md §Yapılandırma.</summary>
    public interface IEmailSender
    {
        /// <summary>Kanal yapılandırılmış mı (outbox her zaman true döner).</summary>
        bool IsConfigured { get; }
        Task SendAsync(EmailMessage message, CancellationToken ct = default);
    }

    /// <summary>SMS gönderici (Netgsm / geliştirmede outbox dosyası).</summary>
    public interface ISmsSender
    {
        bool IsConfigured { get; }
        Task SendAsync(SmsMessage message, CancellationToken ct = default);
    }

    /// <summary>
    /// Gönderimi istekten ayırır: mesaj kuyruğa alınır, arka plan servisi gönderir. Böylece yanıt süresi
    /// "hesap var mı" bilgisini sızdırmaz (şifre sıfırlama) ve SMTP yavaşlığı isteği bekletmez.
    /// </summary>
    public interface IOutgoingMessageQueue
    {
        void Enqueue(EmailMessage message);
        void Enqueue(SmsMessage message);
    }

    /// <summary>KVKK: loglarda adres/telefonun tamamı yazılmaz.</summary>
    public static class Mask
    {
        public static string Email(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;
            var at = email.IndexOf('@');
            if (at <= 0) return "***";
            var local = email[..at];
            var domain = email[(at + 1)..];
            return (local.Length <= 1 ? local : local[0] + new string('*', Math.Min(local.Length - 1, 5))) + "@" + domain;
        }

        public static string Phone(string? phone)
        {
            var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length < 4) return "***";
            return new string('*', Math.Min(digits.Length - 2, 8)) + digits[^2..];
        }
    }
}
