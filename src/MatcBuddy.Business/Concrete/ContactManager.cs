using System.Net;
using System.Net.Mail;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Core.Messaging;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
{
    /// <summary>
    /// İletişim formu: mesaj kaydedilir, yöneticilere e-posta bildirimi gider; yönetim panelinde okunur / "ilgilenildi" işaretlenir.
    /// </summary>
    public class ContactManager : IContactService
    {
        public const int MaxNameLength = 100;
        public const int MaxEmailLength = 150;
        public const int MaxSubjectLength = 150;
        public const int MaxMessageLength = 2000;
        public const int MaxPageSize = 100;

        private readonly IContactDal _dal;
        private readonly IOutgoingMessageQueue _outbox;

        public ContactManager(IContactDal dal, IOutgoingMessageQueue outbox)
        {
            _dal = dal;
            _outbox = outbox;
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // Ad / konu e-posta başlığına girer: satır sonu ve kontrol karakterleri boşluğa çevrilir
        private static string? SingleLine(string? s) =>
            Clean(s == null ? null : new string(s.Select(c => char.IsControl(c) ? ' ' : c).ToArray()));

        /// <summary>a@b.c biçimi: MailAddress ayrıştırabilmeli, adresin tamamı girilen metin olmalı ve alan adında nokta bulunmalı.</summary>
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > MaxEmailLength) return false;
            if (!MailAddress.TryCreate(email, out var addr) || addr.Address != email) return false;
            var host = addr.Host;
            return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.');
        }

        /// <summary>Girdiyi doğrular; hata yoksa null. Alan adları istemcinin gönderdiği adlardır (name, email, subject, message).</summary>
        public static OperationResult<bool>? Validate(ContactMessageInput? input)
        {
            if (input == null) return OperationResult<bool>.BadRequest(ErrorCodes.Validation.BodyRequired);
            var name = SingleLine(input.Name);
            var email = Clean(input.Email);
            var subject = SingleLine(input.Subject);
            var message = Clean(input.Message);

            if (name == null) return OperationResult<bool>.FieldError("name", ErrorCodes.Validation.Required, "name");
            if (name.Length > MaxNameLength) return OperationResult<bool>.FieldError("name", ErrorCodes.Validation.TooLong, "name", MaxNameLength);
            if (email == null) return OperationResult<bool>.FieldError("email", ErrorCodes.Validation.Required, "email");
            if (!IsValidEmail(email)) return OperationResult<bool>.FieldError("email", ErrorCodes.Validation.InvalidEmail);
            if (subject?.Length > MaxSubjectLength)
                return OperationResult<bool>.FieldError("subject", ErrorCodes.Validation.TooLong, "subject", MaxSubjectLength);
            if (message == null) return OperationResult<bool>.FieldError("message", ErrorCodes.Validation.Required, "message");
            if (message.Length > MaxMessageLength)
                return OperationResult<bool>.FieldError("message", ErrorCodes.Validation.TooLong, "message", MaxMessageLength);
            return null;
        }

        public OperationResult<bool> Submit(ContactMessageInput input, int? playerId)
        {
            var invalid = Validate(input);
            if (invalid != null) return invalid;

            var row = new ContactMessage
            {
                Name = SingleLine(input.Name)!,
                Email = Clean(input.Email)!,
                Subject = SingleLine(input.Subject),
                Message = Clean(input.Message)!,
                PlayerId = playerId is > 0 ? playerId : null,
                CreatedAt = DateTime.UtcNow,
            };
            _dal.Add(row);
            NotifyAdmins(row);
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Contact.Received));
        }

        private void NotifyAdmins(ContactMessage m)
        {
            var subject = $"Yeni iletişim mesajı: {m.Subject ?? m.Name}";
            var lines = new List<string>
            {
                $"Gönderen: {m.Name} <{m.Email}>",
            };
            if (m.PlayerId.HasValue) lines.Add($"Üye no: {m.PlayerId}");
            if (m.Subject != null) lines.Add($"Konu: {m.Subject}");
            lines.Add("");
            lines.Add(m.Message);
            lines.Add("");
            lines.Add("Yönetim paneli → Mesajlar / İletişim bölümünden takip edebilirsiniz.");
            var text = string.Join("\n", lines);
            var html = "<p>" + string.Join("<br>", lines.Select(WebUtility.HtmlEncode)) + "</p>";
            foreach (var (to, name) in _dal.AdminEmails())
            {
                _outbox.Enqueue(new EmailMessage(to, name, subject, text, html));
            }
        }

        public PagedDto<ContactMessageDto> List(bool? handled, int page, int pageSize) =>
            _dal.List(handled, Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));

        public int UnhandledCount() => _dal.UnhandledCount();

        public OperationResult<bool> SetHandled(ContactHandledInput input)
        {
            if (input == null) return OperationResult<bool>.BadRequest(ErrorCodes.Validation.BodyRequired);
            return _dal.SetHandled(input.ContactMessageId, input.Handled, DateTime.UtcNow)
                ? OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Common.Updated))
                : OperationResult<bool>.NotFound(ErrorCodes.Contact.NotFound);
        }
    }
}
