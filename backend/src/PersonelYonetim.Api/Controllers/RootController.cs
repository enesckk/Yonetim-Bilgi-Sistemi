using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Application.Common.Models;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// API bilgisi. Kök adresi üretimde React SPA karşılar.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class RootController : ControllerBase
{
    [HttpGet("/api")]
    public ActionResult<ApiResponse<object>> Get()
    {
        var data = new
        {
            service = "PersonelYonetim.Api",
            message = "API çalışıyor.",
            ui = "/",
            health = "/api/health",
            login = "POST /api/auth/login"
        };

        return Ok(ApiResponse<object>.Ok(data, HttpContext.TraceIdentifier));
    }
}
