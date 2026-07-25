using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Personel yazma/okuma-form — MediatR (eski IEmployeeCommandService).
/// CreateEmployeeRequest aynı zamanda Command: Import da ISender.Send kullanır.
/// </summary>
public sealed class CreateEmployeeRequest : IRequest<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }

    public DateOnly? BirthDate { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public string? PersonalPhone { get; set; }
    public string? CorporatePhone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? CorporateEmail { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    public string? NationalId { get; set; }

    public Guid? UnitId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? EmploymentTypeId { get; set; }
    public Guid? JobTitleId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    /// <summary>Fiili (ana) görev — verilirse birincil görev ataması oluşturulur.</summary>
    public Guid? PrimaryJobDutyId { get; set; }

    public DateOnly? HireDate { get; set; }
    public DateOnly? DirectorateStartDate { get; set; }
    public DateOnly? UnitStartDate { get; set; }
    public DateOnly? DutyStartDate { get; set; }
}

public class UpdateEmployeeRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }

    public DateOnly? BirthDate { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public string? PersonalPhone { get; set; }
    public string? CorporatePhone { get; set; }
    public string? PersonalEmail { get; set; }
    public string? CorporateEmail { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    public string? NationalId { get; set; }
    public bool NationalIdProvided { get; set; }

    public Guid? UnitId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? EmploymentTypeId { get; set; }
    public Guid? JobTitleId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    /// <summary>Fiili (ana) görev — birincil görev ataması ile eşitlenir.</summary>
    public Guid? PrimaryJobDutyId { get; set; }

    public DateOnly? HireDate { get; set; }
    public DateOnly? DirectorateStartDate { get; set; }
    public DateOnly? UnitStartDate { get; set; }
    public DateOnly? DutyStartDate { get; set; }
}

public sealed class UpdateEmployeeCommand : UpdateEmployeeRequest, IRequest
{
    public Guid Id { get; set; }
}

public sealed record GetEmployeeForEditQuery(Guid Id) : IRequest<EmployeeEditDto?>;

public sealed record GetEmployeeFormOptionsQuery : IRequest<EmployeeFormOptionsDto>;

public sealed class EmployeeEditDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? EmployeeNumber { get; init; }

    public DateOnly? BirthDate { get; init; }
    public Gender Gender { get; init; }
    public EmployeeStatus Status { get; init; }

    public string? PersonalPhone { get; init; }
    public string? CorporatePhone { get; init; }
    public string? PersonalEmail { get; init; }
    public string? CorporateEmail { get; init; }
    public string? Address { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactPhone { get; init; }

    public string? NationalId { get; init; }
    public bool CanEditNationalId { get; init; }
    public bool CanEditAddress { get; init; }
    public bool CanEditPhone { get; init; }
    public bool CanChangeStatus { get; init; }

    public Guid? UnitId { get; init; }
    public Guid? FacilityId { get; init; }
    public Guid? EmploymentTypeId { get; init; }
    public Guid? JobTitleId { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
    public Guid? PrimaryJobDutyId { get; init; }

    public DateOnly? HireDate { get; init; }
    public DateOnly? DirectorateStartDate { get; init; }
    public DateOnly? UnitStartDate { get; init; }
    public DateOnly? DutyStartDate { get; init; }
}

public sealed class EmployeeFormOptionsDto
{
    public IReadOnlyList<LookupItemDto> Units { get; init; } = [];
    public IReadOnlyList<LookupItemDto> Facilities { get; init; } = [];
    public IReadOnlyList<LookupItemDto> EmploymentTypes { get; init; } = [];
    public IReadOnlyList<LookupItemDto> JobTitles { get; init; } = [];
    public IReadOnlyList<LookupItemDto> Duties { get; init; } = [];
    public IReadOnlyList<LookupItemDto> Skills { get; init; } = [];
    /// <summary>Aktif personeller — yönetici seçimi için.</summary>
    public IReadOnlyList<LookupItemDto> Managers { get; init; } = [];
    public IReadOnlyList<EnumOptionDto> DutyCategories { get; init; } = [];
    public IReadOnlyList<EnumOptionDto> EducationLevels { get; init; } = [];
    public IReadOnlyList<EnumOptionDto> Genders { get; init; } = [];
    public IReadOnlyList<EnumOptionDto> Statuses { get; init; } = [];
}

public sealed class LookupItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public Guid? ParentId { get; init; }
    /// <summary>OrganizationUnitType (Facility hariç birimler için).</summary>
    public byte? Type { get; init; }
}

public sealed class EnumOptionDto
{
    public int Value { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x).SetValidator(new UpdateEmployeeRequestValidator());
    }
}

/// <summary>Personel durumunu günceller; ayrılış durumlarında görev geçmişi kaydı oluşturur.</summary>
public sealed class SetEmployeeStatusCommand : IRequest
{
    public Guid EmployeeId { get; set; }
    public EmployeeStatus Status { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class SetEmployeeStatusCommandValidator : AbstractValidator<SetEmployeeStatusCommand>
{
    public SetEmployeeStatusCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.EffectiveDate).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Durum değişikliği için gerekçe zorunludur.")
            .MaximumLength(500);
    }
}

/// <summary>Yanlış oluşturulmuş kaydı arşivler — fiziksel silme yok; yalnızca Employees.Archive.</summary>
public sealed class ArchiveEmployeeCommand : IRequest
{
    public Guid EmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ArchiveEmployeeCommandValidator : AbstractValidator<ArchiveEmployeeCommand>
{
    public ArchiveEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Arşivleme gerekçesi zorunludur.")
            .MaximumLength(500);
    }
}
