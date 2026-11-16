using MatcBuddy.Api.Security;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>
    /// İletişim formu (herkese açık) ve yönetim paneli "Mesajlar / İletişim".
    /// Giriş yapmış kullanıcı gönderirse mesaja PlayerId yazılır.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly IContactService _service;

        public ContactController(IContactService service) => _service = service;

        /// <summary>Gövde: { name, email, subject?, message (≤ 2000) }. Yanıt: 200 { ok: true }; hata: 400 { code, errors }.</summary>
        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public IActionResult Send(ContactMessageInput input)
        {
            int? playerId = User.Identity?.IsAuthenticated == true ? User.GetPlayerId() : null;
            var result = _service.Submit(input, playerId);
            return result.Success ? Ok(new { ok = true, message = result.Message }) : this.ToHttp(result);
        }

        /// <summary>Okunmamışlar önce, sonra en yeni. ?handled=true|false ile süzülür. Yanıt: { items, total, page, pageSize }.</summary>
        [HttpGet]
        [Authorize(Roles = Roles.Admin)]
        public ActionResult<PagedDto<ContactMessageDto>> List([FromQuery] bool? handled, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
            _service.List(handled, page, pageSize);

        [HttpGet("UnhandledCount")]
        [Authorize(Roles = Roles.Admin)]
        public ActionResult<int> UnhandledCount() => _service.UnhandledCount();

        /// <summary>{ contactMessageId, handled (varsayılan true) }</summary>
        [HttpPost("SetHandled")]
        [Authorize(Roles = Roles.Admin)]
        public IActionResult SetHandled(ContactHandledInput input) => this.ToHttp(_service.SetHandled(input));
    }
}
