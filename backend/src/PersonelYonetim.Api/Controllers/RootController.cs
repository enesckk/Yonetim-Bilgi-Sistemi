using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Application.Common.Models;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Kök adres (/) — tarayıcıda açılınca 404 yerine bilgilendirici cevap döner.
/// Asıl arayüz React SPA'dır (http://localhost:5173).
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class RootController : ControllerBase
{
    [HttpGet("/")]
    public ActionResult<ApiResponse<object>> Get()
    {
        var data = new
        {
            service = "PersonelYonetim.Api",
            message = "Bu adres yalnızca API'dir. Arayüz için React SPA'yı açın.",
            ui = "http://localhost:5173",
            health = "/api/health",
            login = "POST /api/auth/login"
        };

        return Ok(ApiResponse<object>.Ok(data, HttpContext.TraceIdentifier));
    }
}
