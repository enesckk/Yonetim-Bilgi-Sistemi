using MediatR;

namespace PersonelYonetim.Application.Features.Employees;

public sealed record UploadEmployeePhotoCommand(
    Guid EmployeeId,
    Stream Content,
    string OriginalFileName,
    string ContentType) : IRequest;

public sealed record DeleteEmployeePhotoCommand(Guid EmployeeId) : IRequest;

public sealed record OpenEmployeePhotoQuery(Guid EmployeeId) : IRequest<EmployeePhotoDto>;

public sealed class EmployeePhotoDto : IAsyncDisposable
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public required string DownloadFileName { get; init; }

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}
