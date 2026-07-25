using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Account;

namespace PersonelYonetim.Api.Controllers;

/// <summary>
/// Hesabım — kullanıcı kendi kullanıcı adı, ad-soyad, e-posta ve şifresini yönetir.
/// Ek yetki gerekmez; yalnızca oturum sahibi kendi kaydına eriştiği için Authorize yeterli.
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly ISender _sender;

    public AccountController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<MyAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MyAccountDto>>> Get(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyAccountQuery(), cancellationToken);
        return Ok(ApiResponse<MyAccountDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Update(
        [FromBody] UpdateMyAccountCommand command,
        CancellationToken cancellationToken)
    {
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    /// <summary>Şifre değişince tüm oturumlar kapanır; kullanıcı yeniden giriş yapar.</summary>
    [HttpPost("password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> ChangePassword(
        [FromBody] ChangeMyPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}
