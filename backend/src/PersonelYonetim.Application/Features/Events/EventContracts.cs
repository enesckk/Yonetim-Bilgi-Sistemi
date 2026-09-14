using FluentValidation;
using MediatR;
using PersonelYonetim.Domain.Enums;

namespace PersonelYonetim.Application.Features.Events;

public sealed class EventListItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public EventStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime StartAtUtc { get; init; }
    public DateTime? EndAtUtc { get; init; }
    public string? OrganizingUnitName { get; init; }
    public Guid? FacilityId { get; init; }
    public string? FacilityName { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Address { get; init; }
    public int? ExpectedAttendees { get; init; }
    public int? ActualAttendees { get; init; }
    public int? AttendanceCount { get; set; }
    public Guid? SeriesId { get; init; }
    public EventRecurrenceFrequency RecurrenceFrequency { get; init; }
    public string? RecurrenceLabel { get; init; }
    public Guid? ResponsibleEmployeeId { get; init; }
    public string? ResponsibleEmployeeName { get; init; }
    public string? Category { get; init; }
    public string? CategoryLabel { get; set; }
}

public sealed class EventDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public EventStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime StartAtUtc { get; init; }
    public DateTime? EndAtUtc { get; init; }
    public Guid? OrganizingUnitId { get; init; }
    public string? OrganizingUnitName { get; init; }
    public Guid? FacilityId { get; init; }
    public string? FacilityName { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? Address { get; init; }
    public int? ExpectedAttendees { get; init; }
    public int? ActualAttendees { get; init; }
    public int? AttendanceCount { get; set; }
    public Guid? SeriesId { get; init; }
    public EventRecurrenceFrequency RecurrenceFrequency { get; init; }
    public string? RecurrenceLabel { get; init; }
    public int SeriesCount { get; init; }
    public Guid? ResponsibleEmployeeId { get; init; }
    public string? ResponsibleEmployeeName { get; init; }
    public string? Category { get; init; }
    public string? CategoryLabel { get; init; }
    public IReadOnlyList<EventSettlementDto> Settlements { get; init; } = [];
    public IReadOnlyList<EventStatus> AllowedTransitions { get; init; } = [];
}

public sealed class EventListDto
{
    public IReadOnlyList<EventListItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
}

public sealed class EventConflictItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public EventStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime StartAtUtc { get; init; }
    public DateTime? EndAtUtc { get; init; }
}

public sealed class EventConflictsDto
{
    public bool HasConflicts { get; init; }
    public IReadOnlyList<EventConflictItemDto> Items { get; init; } = [];
}

public sealed class EventSettlementDto
{
    public Guid SettlementId { get; init; }
    public string SettlementName { get; init; } = string.Empty;
    public string OfficialCode { get; init; } = string.Empty;
    public int AttendanceCount { get; init; }
    public int? UniqueBeneficiaryCount { get; init; }
    public string? Notes { get; init; }
}

public sealed class EventSettlementInputDto
{
    public Guid SettlementId { get; set; }
    public int AttendanceCount { get; set; }
    public int? UniqueBeneficiaryCount { get; set; }
    public string? Notes { get; set; }
}

public sealed record GetEventsQuery(
    string? Search = null,
    EventStatus? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? SettlementId = null,
    Guid? FacilityId = null) : IRequest<EventListDto>;

public sealed record GetEventByIdQuery(Guid Id) : IRequest<EventDetailDto?>;

public sealed record CheckEventConflictsQuery(
    Guid? FacilityId,
    DateTime StartAtUtc,
    DateTime? EndAtUtc,
    Guid? ExcludeEventId = null) : IRequest<EventConflictsDto>;

public sealed class CreateEventCommand : IRequest<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public Guid? OrganizingUnitId { get; set; }
    public Guid? FacilityId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public int? ExpectedAttendees { get; set; }
    public int? ActualAttendees { get; set; }
    public Guid? ResponsibleEmployeeId { get; set; }
    public EventRecurrenceFrequency RecurrenceFrequency { get; set; } = EventRecurrenceFrequency.None;
    /// <summary>Seri kaç kez tekrarlasın (ilk kayıt dahil). 2–26.</summary>
    public int? RecurrenceOccurrences { get; set; }
    /// <summary>true ise tesis saat çakışması engellenmez (kullanıcı onayı sonrası).</summary>
    public bool AllowConflicts { get; set; }
    public string? Category { get; set; }
    public List<EventSettlementInputDto> Settlements { get; set; } = [];
}

public sealed class UpdateEventCommand : IRequest
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime? EndAtUtc { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public Guid? OrganizingUnitId { get; set; }
    public Guid? FacilityId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public int? ExpectedAttendees { get; set; }
    public int? ActualAttendees { get; set; }
    public Guid? ResponsibleEmployeeId { get; set; }
    public bool AllowConflicts { get; set; }
    public string? Category { get; set; }
    public List<EventSettlementInputDto> Settlements { get; set; } = [];
}

public sealed class ChangeEventStatusCommand : IRequest<EventDetailDto>
{
    public Guid Id { get; set; }
    public EventStatus Status { get; set; }
    public bool AllowConflicts { get; set; }
}

public sealed record DeleteEventCommand(Guid Id) : IRequest;

public static class EventStatusTransitions
{
    public static IReadOnlyList<EventStatus> AllowedFrom(EventStatus current) => current switch
    {
        EventStatus.Draft => [EventStatus.Published, EventStatus.Cancelled],
        EventStatus.Published => [EventStatus.Completed, EventStatus.Cancelled, EventStatus.Draft],
        EventStatus.Cancelled => [EventStatus.Draft],
        EventStatus.Completed => [EventStatus.Draft],
        _ => []
    };

    public static bool CanTransition(EventStatus from, EventStatus to) =>
        from == to || AllowedFrom(from).Contains(to);
}

public sealed class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Etkinlik başlığı zorunludur.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.StartAtUtc).NotEmpty().WithMessage("Başlangıç zamanı zorunludur.");
        RuleFor(x => x).Must(x => x.EndAtUtc is null || x.EndAtUtc >= x.StartAtUtc)
            .WithMessage("Bitiş, başlangıçtan önce olamaz.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.ExpectedAttendees).InclusiveBetween(1, 100_000).When(x => x.ExpectedAttendees.HasValue);
        RuleFor(x => x.ActualAttendees).InclusiveBetween(0, 100_000).When(x => x.ActualAttendees.HasValue);
        RuleForEach(x => x.Settlements).ChildRules(s =>
        {
            s.RuleFor(v => v.SettlementId).NotEmpty();
            s.RuleFor(v => v.AttendanceCount).InclusiveBetween(0, 1_000_000);
            s.RuleFor(v => v.UniqueBeneficiaryCount).InclusiveBetween(0, 1_000_000)
                .When(v => v.UniqueBeneficiaryCount.HasValue);
        });
        RuleFor(x => x.RecurrenceFrequency).IsInEnum();
        RuleFor(x => x.RecurrenceOccurrences)
            .InclusiveBetween(2, 26)
            .When(x => x.RecurrenceFrequency != EventRecurrenceFrequency.None)
            .WithMessage("Tekrar sayısı 2–26 arasında olmalıdır.");
        RuleFor(x => x).Must(x =>
                (x.Latitude.HasValue && x.Longitude.HasValue)
                || x.FacilityId.HasValue
                || x.Settlements.Any(s => s.SettlementId != Guid.Empty))
            .WithMessage("Planlanan veya yapılan etkinlik için mahalle, tesis veya koordinat gerekir.")
            .When(x => x.Status is EventStatus.Published or EventStatus.Completed);
    }
}

public sealed class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Etkinlik başlığı zorunludur.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.StartAtUtc).NotEmpty().WithMessage("Başlangıç zamanı zorunludur.");
        RuleFor(x => x).Must(x => x.EndAtUtc is null || x.EndAtUtc >= x.StartAtUtc)
            .WithMessage("Bitiş, başlangıçtan önce olamaz.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.ExpectedAttendees).InclusiveBetween(1, 100_000).When(x => x.ExpectedAttendees.HasValue);
        RuleFor(x => x.ActualAttendees).InclusiveBetween(0, 100_000).When(x => x.ActualAttendees.HasValue);
        RuleForEach(x => x.Settlements).ChildRules(s =>
        {
            s.RuleFor(v => v.SettlementId).NotEmpty();
            s.RuleFor(v => v.AttendanceCount).InclusiveBetween(0, 1_000_000);
            s.RuleFor(v => v.UniqueBeneficiaryCount).InclusiveBetween(0, 1_000_000)
                .When(v => v.UniqueBeneficiaryCount.HasValue);
        });
        RuleFor(x => x).Must(x =>
                (x.Latitude.HasValue && x.Longitude.HasValue)
                || x.FacilityId.HasValue
                || x.Settlements.Any(s => s.SettlementId != Guid.Empty))
            .WithMessage("Planlanan veya yapılan etkinlik için mahalle, tesis veya koordinat gerekir.")
            .When(x => x.Status is EventStatus.Published or EventStatus.Completed);
    }
}

public sealed class ChangeEventStatusCommandValidator : AbstractValidator<ChangeEventStatusCommand>
{
    public ChangeEventStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

// —— Harita ——

public sealed class MapPinDto
{
    public string Id { get; init; } = string.Empty;
    /// <summary>facility | event</summary>
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Subtitle { get; init; }
    public string? LinkPath { get; init; }
    public DateTime? StartAtUtc { get; init; }
    public string? StatusLabel { get; init; }
    public string? CategoryName { get; init; }
}

public sealed class MapPinsDto
{
    public IReadOnlyList<MapPinDto> Pins { get; init; } = [];
    public double DefaultLatitude { get; init; } = 37.0662;
    public double DefaultLongitude { get; init; } = 37.3833;
    public int DefaultZoom { get; init; } = 12;
}

public sealed record GetMapPinsQuery(
    string? Kinds = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IRequest<MapPinsDto>;

// —— Zaman çizelgesi / istatistik / import ——

public sealed class EventTimelineItemDto
{
    public DateTime OccurredAtUtc { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? Detail { get; init; }
}

public sealed class EventTimelineDto
{
    public IReadOnlyList<EventTimelineItemDto> Items { get; init; } = [];
}

public sealed record GetEventTimelineQuery(Guid EventId) : IRequest<EventTimelineDto>;

public sealed class FacilityEventStatDto
{
    public Guid FacilityId { get; init; }
    public string FacilityName { get; init; } = string.Empty;
    public int TotalEvents { get; init; }
    public int UpcomingEvents { get; init; }
    public int PublishedEvents { get; init; }
}

public sealed class EventFacilityStatsDto
{
    public IReadOnlyList<FacilityEventStatDto> Items { get; init; } = [];
}

public sealed record GetEventFacilityStatsQuery : IRequest<EventFacilityStatsDto>;

public sealed class EventImportRowResultDto
{
    public int RowNumber { get; init; }
    public bool Success { get; init; }
    public Guid? EventId { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class EventImportResultDto
{
    public int TotalRows { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public IReadOnlyList<EventImportRowResultDto> Rows { get; init; } = [];
}

public sealed class ImportEventsCommand : IRequest<EventImportResultDto>
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
}

public sealed class EventImportTemplateFile
{
    public byte[] Content { get; init; } = [];
    public string FileName { get; init; } = "etkinlik-import.xlsx";
    public string ContentType { get; init; } =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public sealed record GetEventImportTemplateQuery : IRequest<EventImportTemplateFile>;

public sealed class FacilityCoordsImportRowDto
{
    public int RowNumber { get; init; }
    public bool Success { get; init; }
    public Guid? FacilityId { get; init; }
    public string? FacilityName { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class FacilityCoordsImportResultDto
{
    public int TotalRows { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public IReadOnlyList<FacilityCoordsImportRowDto> Rows { get; init; } = [];
}

public sealed class ImportFacilityCoordsCommand : IRequest<FacilityCoordsImportResultDto>
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
}

public sealed class EventNoteDto
{
    public Guid Id { get; init; }
    public Guid AuthorUserId { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public int SharedRecipientCount { get; init; }
}

public sealed record GetEventNotesQuery(Guid EventId) : IRequest<IReadOnlyList<EventNoteDto>>;

public sealed class CreateEventNoteCommand : IRequest<EventNoteDto>
{
    public Guid EventId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool ShareToDirectors { get; set; }
    public bool ShareToUnitManagers { get; set; }
    public List<Guid> RecipientUserIds { get; set; } = [];
}

public sealed class CreateEventNoteValidator : AbstractValidator<CreateEventNoteCommand>
{
    public CreateEventNoteValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().WithMessage("Not boş olamaz.").MaximumLength(4000);
    }
}

public sealed class HallBookingDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public EventStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public DateTime StartAtUtc { get; init; }
    public DateTime? EndAtUtc { get; init; }
    public int? ExpectedAttendees { get; init; }
    public int? ActualAttendees { get; init; }
    public int? AttendanceCount { get; init; }
}

public sealed class HallBoardItemDto
{
    public Guid FacilityId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ParentName { get; init; }
    public int? Capacity { get; init; }
    public string Group { get; init; } = "other";
    public bool OccupiedNow { get; init; }
    public HallBookingDto? LastDone { get; init; }
    public HallBookingDto? Next { get; init; }
    public IReadOnlyList<HallBookingDto> Bookings { get; init; } = [];
}

public sealed class HallBoardDto
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public IReadOnlyList<HallBoardItemDto> Halls { get; init; } = [];
}

public sealed record GetHallBoardQuery(DateTime? FromUtc = null, DateTime? ToUtc = null) : IRequest<HallBoardDto>;
