using MediatR;
using Microsoft.AspNetCore.Mvc;
using PersonelYonetim.Api.Authorization;
using PersonelYonetim.Application.Common;
using PersonelYonetim.Application.Common.Models;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeQueryService _employeeQueryService;
    private readonly ISender _sender;

    public EmployeesController(IEmployeeQueryService employeeQueryService, ISender sender)
    {
        _employeeQueryService = employeeQueryService;
        _sender = sender;
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<EmployeeListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<EmployeeListItemDto>>>> GetList(
        [FromQuery] EmployeeListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _employeeQueryService.GetListAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<EmployeeListItemDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("form-options")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<EmployeeFormOptionsDto>>> GetFormOptions(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetEmployeeFormOptionsQuery(), cancellationToken);
        return Ok(ApiResponse<EmployeeFormOptionsDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeDetailDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _employeeQueryService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<EmployeeDetailDto>.Fail(
                new ApiError
                {
                    Code = ErrorCodes.NotFound,
                    Message = "Personel bulunamadı veya bu kayda erişim yetkiniz yok."
                },
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<EmployeeDetailDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}/edit")]
    [RequirePermission(PermissionCodes.EmployeesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeEditDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmployeeEditDto>>> GetForEdit(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetEmployeeForEditQuery(id), cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<EmployeeEditDto>.Fail(
                new ApiError
                {
                    Code = ErrorCodes.NotFound,
                    Message = "Personel bulunamadı."
                },
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<EmployeeEditDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.EmployeesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _sender.Send(request, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { id }, HttpContext.TraceIdentifier);
        return CreatedAtAction(nameof(GetById), new { id }, payload);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.EmployeesUpdate)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEmployeeCommand
        {
            Id = id,
            FirstName = body.FirstName,
            LastName = body.LastName,
            EmployeeNumber = body.EmployeeNumber,
            BirthDate = body.BirthDate,
            Gender = body.Gender,
            Status = body.Status,
            PersonalPhone = body.PersonalPhone,
            CorporatePhone = body.CorporatePhone,
            PersonalEmail = body.PersonalEmail,
            CorporateEmail = body.CorporateEmail,
            Address = body.Address,
            EmergencyContactName = body.EmergencyContactName,
            EmergencyContactPhone = body.EmergencyContactPhone,
            NationalId = body.NationalId,
            NationalIdProvided = body.NationalIdProvided,
            UnitId = body.UnitId,
            FacilityId = body.FacilityId,
            EmploymentTypeId = body.EmploymentTypeId,
            JobTitleId = body.JobTitleId,
            ManagerEmployeeId = body.ManagerEmployeeId,
            PrimaryJobDutyId = body.PrimaryJobDutyId,
            HireDate = body.HireDate,
            DirectorateStartDate = body.DirectorateStartDate,
            UnitStartDate = body.UnitStartDate,
            DutyStartDate = body.DutyStartDate
        };

        await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    /// <summary>Personel durumunu günceller (ayrılış/emeklilik vb.) — kayıt silinmez.</summary>
    [HttpPatch("{id:guid}/status")]
    [RequirePermission(PermissionCodes.EmployeesSetStatus)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> SetStatus(
        Guid id,
        [FromBody] SetEmployeeStatusBody body,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            new SetEmployeeStatusCommand
            {
                EmployeeId = id,
                Status = body.Status,
                EffectiveDate = body.EffectiveDate,
                Reason = body.Reason ?? string.Empty
            },
            cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    /// <summary>Yanlış oluşturulmuş kaydı arşivler — yalnızca sistem yöneticisi; fiziksel silme yok.</summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission(PermissionCodes.EmployeesArchive)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Archive(
        Guid id,
        [FromBody] ArchiveEmployeeBody body,
        CancellationToken cancellationToken)
    {
        await _sender.Send(
            new ArchiveEmployeeCommand
            {
                EmployeeId = id,
                Reason = body.Reason ?? string.Empty
            },
            cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Profil fotoğrafı — ham dosya (ApiResponse sarmalayıcısı yok). Employees.View + birim kapsamı.
    /// </summary>
    [HttpGet("{id:guid}/photo")]
    [RequirePermission(PermissionCodes.EmployeesView)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetPhoto(Guid id, CancellationToken cancellationToken)
    {
        var photo = await _sender.Send(new OpenEmployeePhotoQuery(id), cancellationToken);
        return File(photo.Stream, photo.ContentType, enableRangeProcessing: false);
    }

    /// <summary>multipart/form-data: field adı "file". Employees.Update + Files.Upload.</summary>
    [HttpPost("{id:guid}/photo")]
    [RequirePermission(PermissionCodes.EmployeesUpdate)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> UploadPhoto(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new PersonelYonetim.Application.Common.Exceptions.ValidationException(
                "file",
                "Dosya seçilmedi.");

        await using var stream = file.OpenReadStream();
        await _sender.Send(
            new UploadEmployeePhotoCommand(id, stream, file.FileName, file.ContentType),
            cancellationToken);

        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:guid}/photo")]
    [RequirePermission(PermissionCodes.EmployeesUpdate)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> DeletePhoto(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteEmployeePhotoCommand(id), cancellationToken);
        return Ok(ApiResponse.Ok(HttpContext.TraceIdentifier));
    }
}

public sealed class SetEmployeeStatusBody
{
    public EmployeeStatus Status { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Reason { get; set; }
}

public sealed class ArchiveEmployeeBody
{
    public string? Reason { get; set; }
}
