using System.IO.Compression;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MediatR;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Authorization;

namespace PersonelYonetim.Infrastructure.Events;

public sealed class PreviewEventParticipantsHandler(ICurrentUserService user)
    : IRequestHandler<PreviewEventParticipantsCommand, EventParticipantsPreview>
{
    public Task<EventParticipantsPreview> Handle(PreviewEventParticipantsCommand request, CancellationToken ct)
    {
        if (!user.HasPermission(PermissionCodes.EventsManage)) throw new ForbiddenException("Katılımcı listesi yükleme yetkiniz yok.");
        if (!string.Equals(Path.GetExtension(request.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("file", "Katılımcı listesi .xlsx biçiminde olmalıdır.");
        try
        {
            using var input = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = request.FileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                if (input.Length + read > 10 * 1024 * 1024) throw new ValidationException("file", "Dosya en fazla 10 MB olabilir.");
                input.Write(buffer, 0, read);
            }
            input.Position = 0;
            using (var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
            {
                if (zip.Entries.Count > 1000 || zip.Entries.Sum(e => e.Length) > 50 * 1024 * 1024)
                    throw new ValidationException("file", "Excel dosyasının açılmış boyutu çok büyük.");
            }
            input.Position = 0;
            using var wb = new XLWorkbook(input);
            var ws = wb.Worksheets.FirstOrDefault() ?? throw new ValidationException("file", "Excel sayfası bulunamadı.");
            var header = ws.FirstRowUsed() ?? throw new ValidationException("file", "Excel boş.");
            var headers = header.CellsUsed().GroupBy(c => Fold(c.GetString())).ToDictionary(g => g.Key, g => g.ToList());
            int Find(params string[] aliases)
            {
                var matches = aliases.Where(headers.ContainsKey).SelectMany(a => headers[a]).ToList();
                if (matches.Count > 1) throw new ValidationException("file", "Aynı isim başlığı birden fazla sütunda bulunuyor.");
                return matches.Count == 1 ? matches[0].Address.ColumnNumber : 0;
            }
            var full = Find("adsoyad", "adisoyadi", "isimsoyisim", "katilimci", "katilimciadi");
            var first = Find("ad", "adi", "isim");
            var last = Find("soyad", "soyadi", "soyisim");
            if (full == 0 && (first == 0 || last == 0))
                throw new ValidationException("file", "İlk sayfanın başlık satırında Ad Soyad veya ayrı Ad ve Soyad sütunları bulunmalıdır.");
            if ((ws.LastRowUsed()?.RowNumber() ?? 0) - header.RowNumber() > 10000)
                throw new ValidationException("file", "Excel en fazla 10000 veri satırı içerebilir; boş satırları temizleyin.");
            var names = new List<string>();
            foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                ct.ThrowIfCancellationRequested();
                var cells = full != 0 ? new[] { row.Cell(full) } : new[] { row.Cell(first), row.Cell(last) };
                if (cells.All(c => c.IsEmpty())) continue;
                if (cells.Any(c => c.HasFormula || c.DataType != XLDataType.Text || string.IsNullOrWhiteSpace(c.GetString())))
                    throw new ValidationException("file", $"{row.RowNumber()}. satır: ad ve soyad metin olmalıdır; formül veya eksik isim kabul edilmez.");
                var name = string.Join(" ", cells.Select(c => c.GetString().Trim()));
                if (name.Any(char.IsControl) || name.Length > 200)
                    throw new ValidationException("file", $"{row.RowNumber()}. satır: isim en fazla 200 karakter olabilir ve kontrol karakteri içeremez.");
                names.Add(Regex.Replace(name, " +", " "));
                if (names.Count > 5000) throw new ValidationException("file", "En fazla 5000 katılımcı yüklenebilir.");
            }
            if (names.Count == 0) throw new ValidationException("file", "Dosyada katılımcı ismi bulunamadı.");
            var duplicates = names.Count - names.Select(Fold).Distinct().Count();
            return Task.FromResult(new EventParticipantsPreview(names, duplicates));
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or FormatException or System.Xml.XmlException or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ValidationException("file", "Excel dosyası okunamadı. Geçerli, şifresiz bir .xlsx dosyası yükleyin.");
        }
    }

    private static string Fold(string value) => Regex.Replace(value.ToLowerInvariant().Replace('ı', 'i').Replace('İ', 'i').Replace('ş', 's').Replace('ğ', 'g').Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c'), "[^a-z0-9]", "");
}

public sealed class GetEventParticipantsFileHandler(ISender sender, ICurrentUserService user)
    : IRequestHandler<GetEventParticipantsFileQuery, EventImportTemplateFile>
{
    public async Task<EventImportTemplateFile> Handle(GetEventParticipantsFileQuery request, CancellationToken ct)
    {
        IReadOnlyList<string> names = [];
        if (request.EventId is Guid id)
        {
            // Reuse the existing event permission and unit scope checks.
            var ev = await sender.Send(new GetEventByIdQuery(id), ct) ?? throw new NotFoundException("Etkinlik bulunamadı.");
            names = ev.ParticipantNames;
        }
        else if (!user.HasPermission(PermissionCodes.EventsManage)) throw new ForbiddenException("Yetkiniz yok.");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Katılımcılar");
        ws.Cell(1, 1).Value = "Ad Soyad";
        ws.Row(1).Style.Font.Bold = true;
        ws.Column(1).Width = 42;
        for (var i = 0; i < names.Count; i++) ws.Cell(i + 2, 1).Value = names[i];
        using var output = new MemoryStream();
        wb.SaveAs(output);
        return new EventImportTemplateFile { Content = output.ToArray(), FileName = request.EventId.HasValue ? "katilimci-listesi.xlsx" : "katilimci-listesi-sablon.xlsx" };
    }
}
