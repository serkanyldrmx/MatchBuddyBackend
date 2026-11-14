using System.Net;
using System.Net.Mail;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Core.Messaging;
using MatcBuddy.Core.Utilities;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
{
    /// <summary>
    /// İşletme üyelik başvurusu: başvuru kaydedilir, yöneticilere e-posta gider; hesap yönetici tarafından açılır.
    /// </summary>
    public class BusinessApplicationManager : IBusinessApplicationService
    {
        private readonly IBusinessApplicationDal _dal;
        private readonly IOutgoingMessageQueue _outbox;

        public BusinessApplicationManager(IBusinessApplicationDal dal, IOutgoingMessageQueue outbox)
        {
            _dal = dal;
            _outbox = outbox;
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        public OperationResult<bool> Submit(BusinessApplicationInput input)
        {
            if (input == null) return OperationResult<bool>.BadRequest(ErrorCodes.Validation.BodyRequired);
            var business = Clean(input.BusinessName);
            var contact = Clean(input.ContactName);
            var phone = PhoneNumbers.Normalize(input.Phone);
            var email = Clean(input.Email);
            var city = Clean(input.City);
            var district = Clean(input.District);
            var note = Clean(input.Note);

            if (business == null) return OperationResult<bool>.FieldError("businessName", ErrorCodes.Validation.Required, "businessName");
            if (business.Length > 120) return OperationResult<bool>.FieldError("businessName", ErrorCodes.Validation.TooLong, "businessName", 120);
            if (contact == null) return OperationResult<bool>.FieldError("contactName", ErrorCodes.Validation.Required, "contactName");
            if (contact.Length > 100) return OperationResult<bool>.FieldError("contactName", ErrorCodes.Validation.TooLong, "contactName", 100);
            if (phone == null || phone.Length < 10 || phone.Length > 20)
                return OperationResult<bool>.FieldError("phone", ErrorCodes.Validation.InvalidFormat);
            if (email != null && (email.Length > 150 || !MailAddress.TryCreate(email, out _)))
                return OperationResult<bool>.FieldError("email", ErrorCodes.Validation.InvalidEmail);
            if (city?.Length > 60) return OperationResult<bool>.FieldError("city", ErrorCodes.Validation.TooLong, "city", 60);
            if (district?.Length > 60) return OperationResult<bool>.FieldError("district", ErrorCodes.Validation.TooLong, "district", 60);
            if (input.PitchCount is < 1 or > 50)
                return OperationResult<bool>.FieldError("pitchCount", ErrorCodes.Validation.OutOfRange, "pitchCount", 1, 50);
            if (note?.Length > 1000) return OperationResult<bool>.FieldError("note", ErrorCodes.Validation.TooLong, "note", 1000);
            if (!input.Consent) return OperationResult<bool>.FieldError("consent", ErrorCodes.BusinessApplication.ConsentRequired);

            var received = Msg.Get(MessageKeys.BusinessApplication.Received);
            // Aynı numaradan 24 saat içinde ikinci başvuru: tekrar kaydedilmez, kullanıcıya yine "alındı" denir
            if (_dal.RecentlyApplied(phone)) return OperationResult<bool>.Ok(true, received);

            var app = new BusinessApplication
            {
                BusinessName = business,
                ContactName = contact,
                Phone = phone,
                Email = email,
                City = city,
                District = district,
                PitchCount = input.PitchCount,
                Note = note,
                Status = BusinessApplicationStatus.New,
                CreatedAt = DateTime.Now,
            };
            _dal.Add(app);
            NotifyAdmins(app);
            return OperationResult<bool>.Ok(true, received);
        }

        private void NotifyAdmins(BusinessApplication a)
        {
            var subject = $"Yeni işletme başvurusu: {a.BusinessName}";
            var place = string.Join(" / ", new[] { a.City, a.District }.Where(x => !string.IsNullOrEmpty(x)));
            var lines = new List<string>
            {
                $"İşletme: {a.BusinessName}",
                $"Yetkili: {a.ContactName}",
                $"Telefon: {a.Phone}",
            };
            if (a.Email != null) lines.Add($"E-posta: {a.Email}");
            if (place.Length > 0) lines.Add($"Konum: {place}");
            if (a.PitchCount.HasValue) lines.Add($"Saha sayısı: {a.PitchCount}");
            if (a.Note != null) lines.Add($"Not: {a.Note}");
            lines.Add("");
            lines.Add("Yönetim paneli → İşletme başvuruları bölümünden takip edebilirsiniz.");
            var text = string.Join("\n", lines);
            var html = "<p>" + string.Join("<br>", lines.Select(WebUtility.HtmlEncode)) + "</p>";
            foreach (var (to, name) in _dal.AdminEmails())
            {
                _outbox.Enqueue(new EmailMessage(to, name, subject, text, html));
            }
        }

        public List<BusinessApplicationDto> List(byte? status) => _dal.List(status).Select(a => new BusinessApplicationDto
        {
            BusinessApplicationId = a.BusinessApplicationId,
            BusinessName = a.BusinessName,
            ContactName = a.ContactName,
            Phone = a.Phone,
            Email = a.Email,
            City = a.City,
            District = a.District,
            PitchCount = a.PitchCount,
            Note = a.Note,
            Status = a.Status,
            AdminNote = a.AdminNote,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
        }).ToList();

        public int NewCount() => _dal.NewCount();

        public OperationResult<bool> Update(BusinessApplicationUpdateInput input)
        {
            if (input == null) return OperationResult<bool>.BadRequest(ErrorCodes.Validation.BodyRequired);
            if (!BusinessApplicationStatus.IsValid(input.Status))
                return OperationResult<bool>.FieldError("status", ErrorCodes.Validation.InvalidValue, input.Status, "status");
            var note = Clean(input.AdminNote);
            if (note?.Length > 1000) return OperationResult<bool>.FieldError("adminNote", ErrorCodes.Validation.TooLong, "adminNote", 1000);
            var app = _dal.Get(a => a.BusinessApplicationId == input.BusinessApplicationId);
            if (app == null) return OperationResult<bool>.NotFound(ErrorCodes.BusinessApplication.NotFound);
            app.Status = (byte)input.Status;
            app.AdminNote = note;
            app.UpdatedAt = DateTime.Now;
            _dal.Update(app);
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Common.Updated));
        }
    }
}
