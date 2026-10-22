using MediatR;
using PersonelYonetim.Application.Common.Interfaces;
namespace PersonelYonetim.Application.Features.Stock;
public sealed record UploadStockPhotoCommand(Guid ItemId, Stream Content, string FileName, string ContentType) : IRequest;
public sealed record DeleteStockPhotoCommand(Guid ItemId) : IRequest;
public sealed record OpenStockPhotoQuery(Guid ItemId) : IRequest<StoredFileOpenResult>;
