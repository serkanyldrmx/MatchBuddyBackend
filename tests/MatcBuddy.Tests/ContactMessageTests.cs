using MatcBuddy.Business.Concrete;
using MatcBuddy.Core.Errors;
using MatcBuddy.Core.Messaging;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Tests;

/// <summary>İletişim formu: doğrulama, kayıt, yöneticiye e-posta, ilgilenildi işareti.</summary>
public class ContactMessageTests
{
    private sealed class FakeDal : IContactDal
    {
        public List<ContactMessage> Rows = new();
        public void Add(ContactMessage message)
        {
            message.ContactMessageId = Rows.Count + 1;
            Rows.Add(message);
        }
        public PagedDto<ContactMessageDto> List(bool? handled, int page, int pageSize) => new() { Page = page, PageSize = pageSize };
        public int UnhandledCount() => Rows.Count(r => !r.IsHandled);
        public bool SetHandled(int id, bool handled, DateTime utcNow)
        {
            var r = Rows.FirstOrDefault(x => x.ContactMessageId == id);
            if (r == null) return false;
            r.IsHandled = handled;
            r.HandledAt = handled ? utcNow : null;
            return true;
        }
        public List<(string Email, string Name)> AdminEmails() => new() { ("admin@example.com", "Admin") };
    }

    private sealed class FakeQueue : IOutgoingMessageQueue
    {
        public List<EmailMessage> Emails = new();
        public void Enqueue(EmailMessage message) => Emails.Add(message);
        public void Enqueue(SmsMessage message) { }
    }

    private static ContactMessageInput Valid() => new()
    {
        Name = "Ayşe Yılmaz",
        Email = "ayse@example.com",
        Subject = "Saha önerisi",
        Message = "Merhaba, mahallemizdeki sahayı da ekleyebilir misiniz?",
    };

    [Fact]
    public void Valid_message_is_saved_and_admins_emailed()
    {
        var dal = new FakeDal();
        var queue = new FakeQueue();
        var r = new ContactManager(dal, queue).Submit(Valid(), 42);
        Assert.True(r.Success);
        var row = Assert.Single(dal.Rows);
        Assert.Equal(42, row.PlayerId);
        Assert.False(row.IsHandled);
        Assert.Equal(DateTimeKind.Utc, row.CreatedAt.Kind);
        var mail = Assert.Single(queue.Emails);
        Assert.Contains("Saha önerisi", mail.Subject);
        Assert.Contains("ayse@example.com", mail.TextBody);
    }

    [Fact]
    public void Anonymous_message_has_no_player()
    {
        var dal = new FakeDal();
        Assert.True(new ContactManager(dal, new FakeQueue()).Submit(Valid(), null).Success);
        Assert.Null(dal.Rows[0].PlayerId);
    }

    [Theory]
    [InlineData("", "a@b.co", "mesaj", "name", ErrorCodes.Validation.Required)]
    [InlineData("Ali", "", "mesaj", "email", ErrorCodes.Validation.Required)]
    [InlineData("Ali", "yanlis", "mesaj", "email", ErrorCodes.Validation.InvalidEmail)]
    [InlineData("Ali", "a@b", "mesaj", "email", ErrorCodes.Validation.InvalidEmail)]
    [InlineData("Ali", "Ali <a@b.co>", "mesaj", "email", ErrorCodes.Validation.InvalidEmail)]
    [InlineData("Ali", "a@b.co", "   ", "message", ErrorCodes.Validation.Required)]
    public void Invalid_fields_are_rejected(string name, string email, string message, string field, string code)
    {
        var dal = new FakeDal();
        var r = new ContactManager(dal, new FakeQueue()).Submit(new ContactMessageInput { Name = name, Email = email, Message = message }, null);
        Assert.False(r.Success);
        Assert.Equal(code, r.Code);
        Assert.True(r.Errors!.ContainsKey(field));
        Assert.Empty(dal.Rows);
    }

    [Fact]
    public void Message_limit_is_2000_characters()
    {
        var m = new ContactManager(new FakeDal(), new FakeQueue());
        var ok = Valid();
        ok.Message = new string('a', 2000);
        Assert.True(m.Submit(ok, null).Success);
        var tooLong = Valid();
        tooLong.Message = new string('a', 2001);
        Assert.Equal(ErrorCodes.Validation.TooLong, m.Submit(tooLong, null).Code);
    }

    [Fact]
    public void Subject_is_optional_and_kept_on_one_line()
    {
        var dal = new FakeDal();
        var queue = new FakeQueue();
        var m = new ContactManager(dal, queue);
        var noSubject = Valid();
        noSubject.Subject = null;
        Assert.True(m.Submit(noSubject, null).Success);
        Assert.Null(dal.Rows[0].Subject);
        Assert.Contains("Ayşe Yılmaz", queue.Emails[0].Subject);

        var injected = Valid();
        injected.Subject = "Merhaba\r\nBcc: x@y.com";
        Assert.True(m.Submit(injected, null).Success);
        Assert.DoesNotContain('\n', dal.Rows[1].Subject!);
        Assert.DoesNotContain('\n', queue.Emails[1].Subject);
    }

    [Fact]
    public void Set_handled_toggles_and_reports_missing()
    {
        var dal = new FakeDal();
        var m = new ContactManager(dal, new FakeQueue());
        m.Submit(Valid(), null);
        Assert.True(m.SetHandled(new ContactHandledInput { ContactMessageId = 1 }).Success);
        Assert.True(dal.Rows[0].IsHandled);
        Assert.NotNull(dal.Rows[0].HandledAt);
        Assert.True(m.SetHandled(new ContactHandledInput { ContactMessageId = 1, Handled = false }).Success);
        Assert.Null(dal.Rows[0].HandledAt);
        Assert.Equal(ErrorCodes.Contact.NotFound, m.SetHandled(new ContactHandledInput { ContactMessageId = 9 }).Code);
    }
}
