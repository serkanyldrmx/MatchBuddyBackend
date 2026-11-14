using System.Linq.Expressions;
using MatcBuddy.Business.Concrete;
using MatcBuddy.Core.Errors;
using MatcBuddy.Core.Messaging;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Tests;

/// <summary>İşletme başvurusu: doğrulama, çift gönderim, yöneticiye e-posta.</summary>
public class BusinessApplicationTests
{
    private sealed class FakeDal : IBusinessApplicationDal
    {
        public List<BusinessApplication> Rows = new();
        public bool Recent;
        public List<BusinessApplication> List(byte? status) => Rows;
        public int NewCount() => Rows.Count;
        public bool RecentlyApplied(string phone) => Recent;
        public List<(string Email, string Name)> AdminEmails() => new() { ("admin@example.com", "Admin") };
        public List<BusinessApplication> GetAll(Expression<Func<BusinessApplication, bool>> filter = null!) => Rows;
        public BusinessApplication Get(Expression<Func<BusinessApplication, bool>> filter) => Rows.AsQueryable().FirstOrDefault(filter)!;
        public void Add(BusinessApplication entity) => Rows.Add(entity);
        public void Update(BusinessApplication entity) { }
        public void Delete(BusinessApplication entity) => Rows.Remove(entity);
    }

    private sealed class FakeQueue : IOutgoingMessageQueue
    {
        public List<EmailMessage> Emails = new();
        public void Enqueue(EmailMessage message) => Emails.Add(message);
        public void Enqueue(SmsMessage message) { }
    }

    private static BusinessApplicationInput Valid() => new()
    {
        BusinessName = "Yeşil Saha",
        ContactName = "Ali Veli",
        Phone = "532 123 45 67",
        City = "İstanbul",
        Consent = true,
    };

    [Fact]
    public void Valid_application_is_saved_with_normalized_phone_and_admins_emailed()
    {
        var dal = new FakeDal();
        var queue = new FakeQueue();
        var r = new BusinessApplicationManager(dal, queue).Submit(Valid());
        Assert.True(r.Success);
        Assert.Single(dal.Rows);
        Assert.Equal("05321234567", dal.Rows[0].Phone);
        Assert.Single(queue.Emails);
        Assert.Contains("Yeşil Saha", queue.Emails[0].Subject);
    }

    [Fact]
    public void Consent_and_phone_are_required()
    {
        var m = new BusinessApplicationManager(new FakeDal(), new FakeQueue());
        var noConsent = Valid();
        noConsent.Consent = false;
        Assert.Equal(ErrorCodes.BusinessApplication.ConsentRequired, m.Submit(noConsent).Code);
        var noPhone = Valid();
        noPhone.Phone = "";
        Assert.Equal(ErrorCodes.Validation.InvalidFormat, m.Submit(noPhone).Code);
    }

    [Fact]
    public void Repeat_within_a_day_is_not_saved_twice()
    {
        var dal = new FakeDal { Recent = true };
        var queue = new FakeQueue();
        var r = new BusinessApplicationManager(dal, queue).Submit(Valid());
        Assert.True(r.Success);
        Assert.Empty(dal.Rows);
        Assert.Empty(queue.Emails);
    }
}
