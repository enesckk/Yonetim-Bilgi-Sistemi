using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
    {
        if (!await _db.Database.CanConnectAsync(cancellationToken))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail(new ApiError { Code = "DATABASE_UNAVAILABLE", Message = "Servis hazır değil." }, HttpContext.TraceIdentifier));

        var data = new
        {
            status = "ok",
            service = "PersonelYonetim.Api",
            utc = DateTime.UtcNow
        };

        return Ok(ApiResponse<object>.Ok(data, HttpContext.TraceIdentifier));
    }
}
