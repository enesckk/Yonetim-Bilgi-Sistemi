using FluentValidation;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Organization;

public sealed class CreateOrganizationUnitCommandValidator : AbstractValidator<CreateOrganizationUnitCommand>
{
    public CreateOrganizationUnitCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Birim adı zorunludur.")
            .MaximumLength(200);

        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Geçerli e-posta girin.")
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.WorkingHours).MaximumLength(250);
        RuleFor(x => x.Capacity).InclusiveBetween(0, 1000000).When(x => x.Capacity.HasValue);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);

        RuleFor(x => x.Type).IsInEnum()
            .Must(t => Enum.IsDefined(t) && (byte)t != 0)
            .WithMessage("Geçerli bir birim tipi seçin.");

        RuleFor(x => x.Status).IsInEnum()
            .Must(s => Enum.IsDefined(s) && (byte)s != 0)
            .WithMessage("Geçerli bir durum seçin.");

        RuleFor(x => x.IdealStaffCount)
            .InclusiveBetween(0, 5000)
            .When(x => x.IdealStaffCount.HasValue);

        // Tesis değilse ideal kadro genelde boş bırakılır; tesis için önerilir (zorunlu değil)
        RuleFor(x => x.ParentId)
            .NotNull()
            .WithMessage("Belediye kökü dışında üst birim seçilmelidir.")
            .When(x => x.Type != OrganizationUnitType.Municipality);
    }
}

public sealed class UpdateOrganizationUnitCommandValidator : AbstractValidator<UpdateOrganizationUnitCommand>
{
    public UpdateOrganizationUnitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Birim adı zorunludur.")
            .MaximumLength(200);

        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Geçerli e-posta girin.")
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.WorkingHours).MaximumLength(250);
        RuleFor(x => x.Capacity).InclusiveBetween(0, 1000000).When(x => x.Capacity.HasValue);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);

        RuleFor(x => x.Type).IsInEnum()
            .Must(t => Enum.IsDefined(t) && (byte)t != 0);

        RuleFor(x => x.Status).IsInEnum()
            .Must(s => Enum.IsDefined(s) && (byte)s != 0);

        RuleFor(x => x.IdealStaffCount)
            .InclusiveBetween(0, 5000)
            .When(x => x.IdealStaffCount.HasValue);

        RuleFor(x => x)
            .Must(x => x.ParentId != x.Id)
            .WithMessage("Birim kendi üstü olamaz.");
    }
}
