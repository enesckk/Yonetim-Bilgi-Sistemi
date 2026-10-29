using MediatR;

namespace PersonelYonetim.Application.Features.Events;

public sealed record PreviewEventParticipantsCommand(Stream FileStream, string FileName) : IRequest<EventParticipantsPreview>;
public sealed record EventParticipantsPreview(IReadOnlyList<string> Names, int DuplicateNameCount);
public sealed record GetEventParticipantsFileQuery(Guid? EventId = null) : IRequest<EventImportTemplateFile>;
