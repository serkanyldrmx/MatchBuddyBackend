using MatcBuddy.Api.Security;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>Sistem parametreleri yönetimi (docs/platform-api.md). Yalnızca Admin.</summary>
    [Route("api/Admin/Parameters")]
    [ApiController]
    [Authorize(Roles = Roles.Admin)]
    public class AdminParametersController : ControllerBase
    {
        private readonly IParameterStore _store;

        public AdminParametersController(IParameterStore store)
        {
            _store = store;
        }

        /// <summary>Tüm parametreler, gruplara ayrılmış.</summary>
        [HttpGet]
        public ActionResult<List<ParameterGroupDto>> List() => _store.ListGrouped();

        /// <summary>Değeri günceller (tür/aralık doğrulaması + denetim kaydı + önbellek temizliği).</summary>
        [HttpPut("{key}")]
        public IActionResult Update(string key, [FromBody] UpdateParameterInput input)
        {
            var result = _store.Update(key, input?.Value, User.GetPlayerId());
            return this.ToHttp(result, result.Success ? new { success = true, message = result.Message, parameter = result.Data } : null);
        }

        /// <summary>Değişiklik geçmişi (yeniden eskiye).</summary>
        [HttpGet("{key}/History")]
        public IActionResult History(string key, [FromQuery] int take = 50)
        {
            var result = _store.History(key, take);
            return this.ToHttp(result, result.Success ? result.Data : null);
        }
    }

    /// <summary>Herkese açık parametreler (anonim, önbelleklenebilir).</summary>
    [Route("api/Parameters")]
    [ApiController]
    [AllowAnonymous]
    public class ParametersController : ControllerBase
    {
        private readonly IParameterStore _store;

        public ParametersController(IParameterStore store)
        {
            _store = store;
        }

        [HttpGet("Public")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
        public ActionResult<Dictionary<string, object?>> Public() => _store.PublicValues();
    }
}
