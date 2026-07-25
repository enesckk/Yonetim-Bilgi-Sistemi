using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Application.Common.Models;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> Get()
    {
        var data = new
        {
            status = "ok",
            service = "PersonelYonetim.Api",
            utc = DateTime.UtcNow
        };

        return Ok(ApiResponse<object>.Ok(data, HttpContext.TraceIdentifier));
    }
}
