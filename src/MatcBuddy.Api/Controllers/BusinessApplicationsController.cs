using MatcBuddy.Api.Security;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>
    /// İşletme üyelik başvuruları: işletmeler sayfasındaki form (herkese açık) ve yönetim paneli listesi.
    /// Hesap kendiliğinden açılmaz; yönetici başvuranla görüşüp hesabı İşletmeler bölümünden oluşturur.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BusinessApplicationsController : ControllerBase
    {
        private readonly IBusinessApplicationService _service;

        public BusinessApplicationsController(IBusinessApplicationService service) => _service = service;

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public IActionResult Submit(BusinessApplicationInput input)
        {
            var result = _service.Submit(input);
            return result.Success ? Ok(new { Success = true, result.Message }) : this.ToHttp(result);
        }

        [HttpGet]
        [Authorize(Roles = Roles.Admin)]
        public ActionResult<List<BusinessApplicationDto>> List([FromQuery] byte? status) => _service.List(status);

        [HttpGet("NewCount")]
        [Authorize(Roles = Roles.Admin)]
        public ActionResult<int> NewCount() => _service.NewCount();

        [HttpPost("Update")]
        [Authorize(Roles = Roles.Admin)]
        public IActionResult Update(BusinessApplicationUpdateInput input)
        {
            var result = _service.Update(input);
            return result.Success ? Ok(new { Success = true, result.Message }) : this.ToHttp(result);
        }
    }
}
