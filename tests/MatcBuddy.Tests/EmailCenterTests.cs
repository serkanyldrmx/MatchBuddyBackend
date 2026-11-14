using MatcBuddy.Business.Concrete;
using MatcBuddy.Business.Email;
using MatcBuddy.Core.Errors;
using MatcBuddy.Core.Messaging;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Tests;

/// <summary>E-posta merkezi: şablon doldurma, hatırlatma seçimi, kampanya gönderimi ve tekrar.</summary>
public class EmailCenterTests
{
    private sealed class Links : IEmailLinks
    {
        public string FrontendBaseUrl => "https://matcbuddy.com";
        public string UnsubscribeUrl(int playerId) => $"https://matcbuddy.com/api/Email/Unsubscribe?p={playerId}&t=x";
    }

    private sealed class Queue : IOutgoingMessageQueue
    {
        public List<EmailMessage> Emails = new();
        public void Enqueue(EmailMessage message) => Emails.Add(message);
        public void Enqueue(SmsMessage message) { }
    }

    private sealed class FakeDal : IEmailCenterDal
    {
        public List<EmailTemplate> TemplatesList = new();
        public List<EmailCampaign> CampaignsList = new();
        public List<EmailRecipient> People = new();
        public Dictionary<(int, int), List<int>> Reminders = new();
        public List<DueEmailReminder> Due = new();
        public HashSet<int> Sent = new();
        public Player Me = new() { PlayerId = 1, PlayerName = "Ali", Email = "ali@example.com" };
        public bool InMatchValue = true;

        public void EnsureDefaults(IEnumerable<EmailTemplate> defaults)
        {
            foreach (var d in defaults.Where(d => TemplatesList.All(t => t.Key != d.Key))) TemplatesList.Add(d);
        }
        public List<EmailTemplate> Templates(int? ownerPlayerId) => TemplatesList.Where(t => t.OwnerPlayerId == ownerPlayerId).ToList();
        public EmailTemplate? Template(int id) => TemplatesList.FirstOrDefault(t => t.EmailTemplateId == id);
        public EmailTemplate? SystemTemplate(string key) => TemplatesList.FirstOrDefault(t => t.OwnerPlayerId == null && t.Key == key);
        public void SaveTemplate(EmailTemplate t) { if (!TemplatesList.Contains(t)) TemplatesList.Add(t); }
        public void DeleteTemplate(int id) => TemplatesList.RemoveAll(t => t.EmailTemplateId == id);
        public List<EmailCampaign> Campaigns(int? ownerPlayerId) => CampaignsList;
        public EmailCampaign? Campaign(int id) => CampaignsList.FirstOrDefault(c => c.EmailCampaignId == id);
        public void SaveCampaign(EmailCampaign c) { if (!CampaignsList.Contains(c)) CampaignsList.Add(c); }
        public List<EmailCampaign> DueCampaigns(DateTime now) => CampaignsList
            .Where(c => c.Status == EmailCampaignStatus.Sending || (c.Status == EmailCampaignStatus.Scheduled && c.ScheduledAt <= now)).ToList();
        public List<EmailRecipient> Recipients(byte audience, int? ownerPlayerId, int afterPlayerId, int take) =>
            People.Where(p => p.PlayerId > afterPlayerId).OrderBy(p => p.PlayerId).Take(take).ToList();
        public int AudienceCount(byte audience, int? ownerPlayerId) => People.Count;
        public DateTime? LastOwnerSend(int ownerPlayerId) => null;
        public List<int> ReminderMinutes(int matchId, int playerId) => Reminders.GetValueOrDefault((matchId, playerId)) ?? new();
        public void SetReminders(int matchId, int playerId, IEnumerable<int> minutes) => Reminders[(matchId, playerId)] = minutes.ToList();
        public List<DueEmailReminder> DueReminders(DateTime now, int take) => Due.Where(d => !Sent.Contains(d.ReminderId)).ToList();
        public void MarkReminderSent(int reminderId, DateTime at) => Sent.Add(reminderId);
        public bool InMatch(int matchId, int playerId) => InMatchValue;
        public Player? Player(int playerId) => playerId == Me.PlayerId ? Me : null;
        public void SetMarketingConsent(int playerId, bool value) => Me.MarketingConsent = value;
        public string? OwnerVenueName(int ownerPlayerId) => ownerPlayerId == 77 ? "Yıldız Halı Saha" : null;
    }

    [Fact]
    public void Fill_replaces_fields_and_html_is_encoded()
    {
        var values = new Dictionary<string, string?> { ["ad"] = "Ali", ["mac"] = "<b>Maç</b>" };
        Assert.Equal("Merhaba Ali, <b>Maç</b> ", EmailLayout.Fill("Merhaba {{ad}}, {{ MAC }} {{bilinmeyen}}", values));
        var m = EmailLayout.Build("a@b.com", null, "Konu", "Satır <script>", "Aç", "https://matcbuddy.com", null);
        Assert.Contains("Satır &lt;script&gt;", m.HtmlBody);
        Assert.DoesNotContain("<script>", m.HtmlBody);
        Assert.Equal("3 saat", EmailLayout.Remaining(180));
        Assert.Equal("1 gün", EmailLayout.Remaining(1440));
    }

    [Fact]
    public void Body_markup_renders_hero_lists_callout_and_info_card()
    {
        var m = EmailLayout.Build("a@b.com", null, "Konu",
            "# Büyük <başlık>\n\nMerhaba **Ali**\n\n- bir\n- iki\n\n> dikkat\n\n| Saat: 21:00\n| Saha: Yeşil", "Aç", "https://x.com/a", null,
            "https://matcbuddy.com", "Bu e-postayı Yıldız gönderdi");
        Assert.Contains("&lt;başlık&gt;", m.HtmlBody);
        Assert.Contains("<strong", m.HtmlBody);
        Assert.Contains(">21:00<", m.HtmlBody);
        Assert.Contains("https://matcbuddy.com/settings", m.HtmlBody);
        Assert.Contains("Bu e-postayı Yıldız gönderdi", m.TextBody);
        Assert.DoesNotContain("**", m.TextBody);
        Assert.Contains("• bir", m.TextBody);
        Assert.DoesNotContain("# ", m.TextBody);
    }

    [Fact]
    public void Starters_are_valid_and_render_for_both_areas()
    {
        var dal = new FakeDal();
        var svc = new EmailCenterManager(dal, new Queue(), new Links());
        foreach (var owner in new[] { false, true })
        {
            var list = svc.Starters(owner);
            Assert.True(list.Count >= 6);
            Assert.Equal(list.Count, list.Select(x => x.Key).Distinct().Count());
            foreach (var st in list)
            {
                var html = svc.Preview(dal.Me.PlayerId, new EmailPreviewInput { Subject = st.Subject, Body = st.Body, ButtonText = st.ButtonText, ButtonUrl = st.ButtonUrl }).Html;
                Assert.DoesNotContain("{{", html);
                if (st.ButtonUrl != null) Assert.Contains("https://matcbuddy.com/", html);
            }
        }
        Assert.All(svc.Starters(true), st => Assert.Contains("{{tesis}}", st.Subject + st.Body));
    }

    [Fact]
    public void Reminders_accept_only_known_options_and_need_email()
    {
        var dal = new FakeDal();
        var m = new EmailCenterManager(dal, new Queue(), new Links());
        Assert.Equal(ErrorCodes.Validation.InvalidValue, m.SetReminders(1, new MatchEmailRemindersInput { MatchId = 5, Minutes = new() { 45 } }).Code);
        Assert.True(m.SetReminders(1, new MatchEmailRemindersInput { MatchId = 5, Minutes = new() { 60, 180 } }).Success);
        Assert.Equal(new[] { 60, 180 }, dal.Reminders[(5, 1)]);
        dal.Me.Email = "";
        Assert.Equal(ErrorCodes.Email.NoEmail, m.SetReminders(1, new MatchEmailRemindersInput { MatchId = 5, Minutes = new() { 60 } }).Code);
    }

    [Fact]
    public void RunDue_sends_reminders_once_and_weekly_campaign_reschedules()
    {
        var dal = new FakeDal();
        var q = new Queue();
        dal.Due.Add(new DueEmailReminder { ReminderId = 9, MinutesBefore = 60, Email = "x@y.com", Name = "Can", MatchId = 3, MatchName = "Salı maçı", MatchDate = DateTime.Now.AddMinutes(60) });
        dal.People.AddRange(Enumerable.Range(1, 3).Select(i => new EmailRecipient { PlayerId = i, Email = $"p{i}@x.com", Name = $"P{i}" }));
        var start = DateTime.Now.AddMinutes(-1);
        dal.CampaignsList.Add(new EmailCampaign { EmailCampaignId = 1, Subject = "Merhaba {{ad}}", Body = "Duyuru", Audience = EmailAudience.ConsentedPlayers, ScheduledAt = start, RepeatDays = 7 });

        var m = new EmailCenterManager(dal, q, new Links());
        Assert.Equal(4, m.RunDue());
        Assert.Contains(q.Emails, e => e.Subject.Contains("Salı maçı"));
        Assert.Contains(q.Emails, e => e.Subject == "Merhaba P2" && e.TextBody.Contains("Unsubscribe"));
        var c = dal.CampaignsList[0];
        Assert.Equal(EmailCampaignStatus.Scheduled, c.Status);
        Assert.True(c.ScheduledAt > DateTime.Now.AddDays(6));
        Assert.Equal(3, c.SentCount);

        q.Emails.Clear();
        Assert.Equal(0, m.RunDue()); // hatırlatma ikinci kez gitmez, kampanyanın zamanı gelmedi
    }
}
