using System.Text.Encodings.Web;
using System.Text.Json;
using MatcBuddy.Api.Security;
using MatcBuddy.Business.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>Oturum sahibinin kendi verileri (KVKK) — docs/auth-api.md §Veri dışa aktarımı.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MeController : ControllerBase
    {
        private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly IAuthService _auth;
        public MeController(IAuthService auth) => _auth = auth;

        /// <summary>Kişisel verilerin JSON dosyası olarak indirilmesi (Content-Disposition: attachment).</summary>
        [HttpGet("Export")]
        public IActionResult Export()
        {
            var me = User.GetPlayerId();
            var player = _auth.GetPlayer(me);
            if (player == null || player.IsDeleted) return this.NotFoundError(ErrorCodes.Player.NotFound);

            var data = new Dictionary<string, object?>
            {
                ["exportedAt"] = DateTime.UtcNow,
                ["format"] = "matcbuddy-export/1",
            };
            foreach (var (key, value) in _auth.Export(me)) data[key] = value;

            var bytes = JsonSerializer.SerializeToUtf8Bytes(data, ExportJson);
            Response.Headers.CacheControl = "no-store";
            return File(bytes, "application/json; charset=utf-8", $"matcbuddy-export-{player.UserName}-{DateTime.UtcNow:yyyyMMdd}.json");
        }
    }
}
