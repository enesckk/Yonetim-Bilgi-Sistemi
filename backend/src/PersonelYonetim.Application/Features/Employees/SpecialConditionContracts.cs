using FluentValidation;
using MediatR;

namespace PersonelYonetim.Application.Features.Employees;

/// <summary>
/// Özel durum — hassas alt kayıt.
/// Görüntüleme: Employees.ViewSpecialConditions
/// Yazma: Employees.ManageSpecialConditions
/// Belge: Files.Upload / Files.View (+ ilgili özel durum yetkisi)
/// </summary>
public sealed record GetSpecialConditionFormOptionsQuery : IRequest<SpecialConditionFormOptionsDto>;

public class UpsertSpecialConditionRequest
{
    public string ConditionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsPermanent { get; set; }
    public bool RequiresDutyAdjustment { get; set; }
    public bool RequiresWorkspaceAdjustment { get; set; }
}

public sealed class CreateSpecialConditionCommand : UpsertSpecialConditionRequest, IRequest<Guid>
{
    public Guid EmployeeId { get; set; }
}

public sealed class UpdateSpecialConditionCommand : UpsertSpecialConditionRequest, IRequest
{
    public Guid EmployeeId { get; set; }
    public Guid ConditionId { get; set; }
}

/// <summary>Soft-delete. ConditionId mutlaka EmployeeId'ye ait olmalı.</summary>
public sealed record DeleteSpecialConditionCommand(Guid EmployeeId, Guid ConditionId) : IRequest;

public sealed record UploadSpecialConditionDocumentCommand(
    Guid EmployeeId,
    Guid ConditionId,
    Stream Content,
    string OriginalFileName,
    string ContentType) : IRequest;

public sealed record RemoveSpecialConditionDocumentCommand(
    Guid EmployeeId,
    Guid ConditionId) : IRequest;

public sealed record OpenSpecialConditionDocumentQuery(
    Guid EmployeeId,
    Guid ConditionId) : IRequest<SpecialConditionDocumentDto>;

public sealed class SpecialConditionFormOptionsDto
{
    public IReadOnlyList<string> SuggestedTypes { get; init; } = [];
}

public sealed class SpecialConditionDocumentDto : IAsyncDisposable
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public required string DownloadFileName { get; init; }

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}

public sealed class CreateSpecialConditionCommandValidator
    : AbstractValidator<CreateSpecialConditionCommand>
{
    public CreateSpecialConditionCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertSpecialConditionRequestValidator());
    }
}

public sealed class UpdateSpecialConditionCommandValidator
    : AbstractValidator<UpdateSpecialConditionCommand>
{
    public UpdateSpecialConditionCommandValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.ConditionId).NotEmpty();
        RuleFor(x => x).SetValidator(new UpsertSpecialConditionRequestValidator());
    }
}
