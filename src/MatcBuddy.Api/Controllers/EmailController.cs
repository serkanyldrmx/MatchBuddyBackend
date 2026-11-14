using System.Net;
using MatcBuddy.Api.Security;
using MatcBuddy.Api.Services;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>
    /// E-posta merkezi (docs/email-api.md): şablonlar ve toplu / düzenli gönderimler (yönetici: /api/Email/Admin/...,
    /// işletme: /api/Email/Owner/...), maç hatırlatma e-postası seçimi ve abonelikten çıkma.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmailController : ControllerBase
    {
        private readonly IEmailCenterService _service;
        private readonly EmailLinks _links;

        public EmailController(IEmailCenterService service, EmailLinks links)
        {
            _service = service;
            _links = links;
        }

        /// <summary>Yönetici: null, işletme: kendi id'si; diğerleri yetkisiz</summary>
        private int? Scope(string area) => area == "Admin" ? null : User.GetPlayerId();

        private bool Allowed(string area) => area switch
        {
            "Admin" => User.IsAdmin(),
            "Owner" => User.IsOwner(),
            _ => false,
        };

        // ------------------------------------------------------------------ Şablonlar

        [HttpGet("{area:regex(^(Admin|Owner)$)}/Templates")]
        public IActionResult Templates(string area) =>
            Allowed(area) ? Ok(_service.Templates(Scope(area))) : Forbid();

        /// <summary>Hazır şablon galerisi</summary>
        [HttpGet("{area:regex(^(Admin|Owner)$)}/Starters")]
        public IActionResult Starters(string area) =>
            Allowed(area) ? Ok(_service.Starters(area == "Owner")) : Forbid();

        [HttpPost("{area:regex(^(Admin|Owner)$)}/Templates")]
        public IActionResult SaveTemplate(string area, EmailTemplateInput input)
        {
            if (!Allowed(area)) return Forbid();
            var r = _service.SaveTemplate(Scope(area), input);
            return r.Success ? Ok(new { Success = true, r.Message, Id = r.Data }) : this.ToHttp(r);
        }

        [HttpPost("{area:regex(^(Admin|Owner)$)}/Templates/Delete")]
        public IActionResult DeleteTemplate(string area, [FromQuery] int id)
        {
            if (!Allowed(area)) return Forbid();
            var r = _service.DeleteTemplate(Scope(area), id);
            return r.Success ? Ok(new { Success = true, r.Message }) : this.ToHttp(r);
        }

        // ------------------------------------------------------------------ Gönderimler

        [HttpGet("{area:regex(^(Admin|Owner)$)}/Campaigns")]
        public IActionResult Campaigns(string area) =>
            Allowed(area) ? Ok(_service.Campaigns(Scope(area))) : Forbid();

        [HttpGet("{area:regex(^(Admin|Owner)$)}/AudienceCount")]
        public IActionResult AudienceCount(string area, [FromQuery] byte audience) =>
            Allowed(area) ? Ok(_service.AudienceCount(Scope(area), audience)) : Forbid();

        [HttpPost("{area:regex(^(Admin|Owner)$)}/Campaigns")]
        public IActionResult SaveCampaign(string area, EmailCampaignInput input)
        {
            if (!Allowed(area)) return Forbid();
            var r = _service.SaveCampaign(User.GetPlayerId(), Scope(area), input);
            return r.Success ? Ok(new { Success = true, r.Message, Id = r.Data }) : this.ToHttp(r);
        }

        [HttpPost("{area:regex(^(Admin|Owner)$)}/Campaigns/Cancel")]
        public IActionResult CancelCampaign(string area, [FromQuery] int id)
        {
            if (!Allowed(area)) return Forbid();
            var r = _service.CancelCampaign(Scope(area), id);
            return r.Success ? Ok(new { Success = true, r.Message }) : this.ToHttp(r);
        }

        [HttpPost("{area:regex(^(Admin|Owner)$)}/Preview")]
        public IActionResult Preview(string area, EmailPreviewInput input) =>
            Allowed(area) ? Ok(_service.Preview(User.GetPlayerId(), input)) : Forbid();

        [HttpPost("{area:regex(^(Admin|Owner)$)}/SendTest")]
        public IActionResult SendTest(string area, EmailPreviewInput input)
        {
            if (!Allowed(area)) return Forbid();
            var r = _service.SendTest(User.GetPlayerId(), input);
            return r.Success ? Ok(new { Success = true, r.Message }) : this.ToHttp(r);
        }

        // ------------------------------------------------------------------ Maç hatırlatma (oyuncu)

        [HttpGet("MatchReminders")]
        public ActionResult<MatchEmailRemindersDto> MatchReminders([FromQuery] int matchId) =>
            _service.GetReminders(matchId, User.GetPlayerId());

        [HttpPost("MatchReminders")]
        public IActionResult SetMatchReminders(MatchEmailRemindersInput input)
        {
            var r = _service.SetReminders(User.GetPlayerId(), input);
            return r.Success ? Ok(new { Success = true, r.Message }) : this.ToHttp(r);
        }

        // ------------------------------------------------------------------ Abonelikten çıkma (e-postadaki bağlantı)

        [HttpGet("Unsubscribe")]
        [AllowAnonymous]
        public ContentResult Unsubscribe([FromQuery] int p, [FromQuery] string? t)
        {
            var ok = _links.Verify(p, t) && _service.Unsubscribe(p).Success;
            var title = ok ? "Abonelikten çıktınız" : "Bağlantı geçersiz";
            var text = ok
                ? "Artık kampanya ve duyuru e-postaları almayacaksınız. Maç hatırlatmaları gibi kendi açtığınız e-postalar devam eder. İstediğiniz zaman Ayarlar'dan yeniden açabilirsiniz."
                : "Bu bağlantı geçersiz ya da eksik. Ayarlar'dan e-posta tercihlerinizi değiştirebilirsiniz.";
            string E(string s) => WebUtility.HtmlEncode(s);
            var html = $@"<!doctype html><html lang=""tr""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><title>{E(title)} · MatcBuddy</title></head>
<body style=""margin:0;font-family:Segoe UI,Helvetica,Arial,sans-serif;background:#0b1220;color:#e5e7eb;display:grid;place-items:center;min-height:100vh;padding:24px"">
<main style=""max-width:440px;background:#111a2b;border-radius:16px;padding:28px;text-align:center"">
<div style=""font-size:40px"">&#9917;</div><h1 style=""font-size:22px;margin:8px 0 12px"">{E(title)}</h1>
<p style=""line-height:1.6;color:#9ca3af"">{E(text)}</p>
<p><a href=""{E(_links.FrontendBaseUrl)}/home"" style=""display:inline-block;margin-top:8px;padding:10px 18px;border-radius:10px;background:#a3e635;color:#0b1220;font-weight:700;text-decoration:none"">MatcBuddy'yi aç</a></p>
</main></body></html>";
            return Content(html, "text/html; charset=utf-8");
        }
    }
}
