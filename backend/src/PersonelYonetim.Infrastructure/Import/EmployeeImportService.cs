using ClosedXML.Excel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Employees;
using PersonelYonetim.Application.Features.Import;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using AppValidationException = PersonelYonetim.Application.Common.Exceptions.ValidationException;

namespace PersonelYonetim.Infrastructure.Import;

/// <summary>
/// ClosedXML ile .xlsx okuma/yazma.
/// Strateji: satır satır Create — başarılı satırlar kalır, hatalılar raporda görünür.
/// </summary>
public sealed class EmployeeImportService : IEmployeeImportService
{
    public const int MaxRows = 500;

    // Şablon sütun başlıkları — kullanıcıya Türkçe; kodda sabit sözlük
    private static readonly string[] RequiredHeaders =
    [
        "Ad",
        "Soyad",
        "SicilNo",
        "Cinsiyet",
        "Durum",
        "DogumTarihi",
        "Telefon",
        "Eposta",
        "BirimKodu",
        "UnvanAdi",
        "IseGirisTarihi",
        "TCKN"
    ];

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;
    private readonly IValidator<CreateEmployeeRequest> _createValidator;
    private readonly IUserNotificationService _notifications;

    public EmployeeImportService(
        AppDbContext db,
        ICurrentUserService currentUser,
        ISender sender,
        IValidator<CreateEmployeeRequest> createValidator,
        IUserNotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _sender = sender;
        _createValidator = createValidator;
        _notifications = notifications;
    }

    public async Task<ImportTemplateFile> BuildTemplateAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanImport();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Personeller");

        for (var i = 0; i < RequiredHeaders.Length; i++)
            sheet.Cell(1, i + 1).Value = RequiredHeaders[i];

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);

        // Örnek satır (kullanıcı silip doldurur)
        sheet.Cell(2, 1).Value = "Ali";
        sheet.Cell(2, 2).Value = "Veli";
        sheet.Cell(2, 3).Value = "SICIL-ORN-001";
        sheet.Cell(2, 4).Value = "Erkek";
        sheet.Cell(2, 5).Value = "Aktif";
        sheet.Cell(2, 6).Value = "1990-05-15";
        sheet.Cell(2, 7).Value = "05321234567";
        sheet.Cell(2, 8).Value = "ali.veli@ornek.local";
        sheet.Cell(2, 9).Value = "BILIM";
        sheet.Cell(2, 10).Value = "Bilim Merkezi Personeli";
        sheet.Cell(2, 11).Value = "2024-01-10";
        sheet.Cell(2, 12).Value = "";

        sheet.Columns().AdjustToContents();

        var refSheet = workbook.Worksheets.Add("Referans");
        refSheet.Cell(1, 1).Value = "BirimKodu";
        refSheet.Cell(1, 2).Value = "BirimAdi";
        refSheet.Cell(1, 3).Value = "UnvanAdi";
        refSheet.Row(1).Style.Font.Bold = true;

        var units = await _db.OrganizationUnits
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new { x.Code, x.Name })
            .ToListAsync(cancellationToken);

        var titles = await _db.JobTitles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < units.Count; i++)
        {
            refSheet.Cell(i + 2, 1).Value = units[i].Code;
            refSheet.Cell(i + 2, 2).Value = units[i].Name;
        }

        for (var i = 0; i < titles.Count; i++)
            refSheet.Cell(i + 2, 3).Value = titles[i];

        refSheet.Columns().AdjustToContents();

        var help = workbook.Worksheets.Add("Yardim");
        help.Cell(1, 1).Value = "Nasıl kullanılır?";
        help.Cell(2, 1).Value = "1) Personeller sayfasındaki örnek satırı silin veya üzerine yazın.";
        help.Cell(3, 1).Value = "2) BirimKodu ve UnvanAdi için Referans sayfasına bakın.";
        help.Cell(4, 1).Value = "3) Cinsiyet: Erkek | Kadın | Diğer | Belirtilmedi";
        help.Cell(5, 1).Value = "4) Durum: Aktif | Pasif | İzinli (boş = Aktif)";
        help.Cell(6, 1).Value = "5) Tarihler: yyyy-MM-dd veya gg.aa.yyyy";
        help.Cell(7, 1).Value = "6) TCKN yalnızca ViewNationalId yetkisi olanlar için işlenir; diğerlerinde yok sayılır.";
        help.Cell(8, 1).Value = $"7) En fazla {MaxRows} veri satırı.";
        help.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return new ImportTemplateFile
        {
            Content = ms.ToArray(),
            FileName = $"personel-import-sablon-{DateTime.UtcNow:yyyyMMdd}.xlsx"
        };
    }

    public async Task<EmployeeImportResultDto> ImportAsync(
        Stream excelStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        EnsureCanImport();

        if (!_currentUser.HasPermission(PermissionCodes.EmployeesCreate))
            throw new ForbiddenException("Toplu aktarım için Employees.Create yetkisi de gerekir.");

        var ext = Path.GetExtension(fileName);
        if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new AppValidationException("file", "Yalnızca .xlsx dosyaları kabul edilir.");

        // Belleğe al — ClosedXML seek ister; üst sınır ~10 MB
        await using var buffered = new MemoryStream();
        await excelStream.CopyToAsync(buffered, cancellationToken);
        if (buffered.Length == 0)
            throw new AppValidationException("file", "Dosya boş.");
        if (buffered.Length > 10 * 1024 * 1024)
            throw new AppValidationException("file", "Excel dosyası en fazla 10 MB olabilir.");

        buffered.Position = 0;

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(buffered);
        }
        catch
        {
            throw new AppValidationException("file", "Excel dosyası okunamadı. Geçerli bir .xlsx yükleyin.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(w =>
                w.Name.Equals("Personeller", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.First();

            var headerMap = ReadHeaderMap(sheet);
            ValidateHeaders(headerMap);

            var unitsByCode = await _db.OrganizationUnits
                .AsNoTracking()
                .Where(x => x.Code != null && x.Code != "")
                .ToDictionaryAsync(
                    x => x.Code!,
                    x => x.Id,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);

            var titlesByName = await _db.JobTitles
                .AsNoTracking()
                .Where(x => x.Name != null && x.Name != "")
                .ToDictionaryAsync(
                    x => x.Name!,
                    x => x.Id,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken);

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            var rowResults = new List<EmployeeImportRowResultDto>();
            var usedSicilsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var canNationalId = _currentUser.HasPermission(PermissionCodes.EmployeesViewNationalId);
            var canSetStatus = _currentUser.HasPermission(PermissionCodes.EmployeesSetStatus);

            var dataRowCount = 0;
            for (var row = 2; row <= lastRow; row++)
            {
                if (IsRowEmpty(sheet, row, headerMap.Count))
                    continue;

                dataRowCount++;
                if (dataRowCount > MaxRows)
                {
                    rowResults.Add(new EmployeeImportRowResultDto
                    {
                        RowNumber = row,
                        Success = false,
                        Errors = [$"Dosyada en fazla {MaxRows} satır işlenir; kalan satırlar atlandı."]
                    });
                    break;
                }

                var rowResult = await ProcessRowAsync(
                    sheet,
                    row,
                    headerMap,
                    unitsByCode,
                    titlesByName,
                    usedSicilsInFile,
                    canNationalId,
                    canSetStatus,
                    cancellationToken);

                rowResults.Add(rowResult);
            }

            if (dataRowCount == 0)
                throw new AppValidationException("file", "İşlenecek veri satırı bulunamadı (başlık satırından sonra).");

            var result = new EmployeeImportResultDto
            {
                TotalRows = rowResults.Count,
                SuccessCount = rowResults.Count(r => r.Success),
                FailureCount = rowResults.Count(r => !r.Success),
                Rows = rowResults
            };

            if (result.FailureCount > 0 && _currentUser.UserId is Guid uid)
            {
                await _notifications.NotifyAsync(
                    uid,
                    "Toplu veri aktarımında hatalı kayıt bulundu",
                    $"{result.FailureCount} satır hatalı, {result.SuccessCount} satır başarılı aktarıldı ({fileName}).",
                    NotificationSeverity.Warning,
                    Application.Features.Notifications.NotificationCategories.Import,
                    "/employees/import",
                    cancellationToken);
            }

            return result;
        }
    }

    private async Task<EmployeeImportRowResultDto> ProcessRowAsync(
        IXLWorksheet sheet,
        int row,
        Dictionary<string, int> headerMap,
        Dictionary<string, Guid> unitsByCode,
        Dictionary<string, Guid> titlesByName,
        HashSet<string> usedSicilsInFile,
        bool canNationalId,
        bool canSetStatus,
        CancellationToken ct)
    {
        var errors = new List<string>();
        var ad = Cell(sheet, row, headerMap, "Ad");
        var soyad = Cell(sheet, row, headerMap, "Soyad");
        var sicil = Cell(sheet, row, headerMap, "SicilNo");
        var cinsiyetRaw = Cell(sheet, row, headerMap, "Cinsiyet");
        var durumRaw = Cell(sheet, row, headerMap, "Durum");
        var dogumRaw = Cell(sheet, row, headerMap, "DogumTarihi");
        var telefon = Cell(sheet, row, headerMap, "Telefon");
        var eposta = Cell(sheet, row, headerMap, "Eposta");
        var birimKodu = Cell(sheet, row, headerMap, "BirimKodu");
        var unvanAdi = Cell(sheet, row, headerMap, "UnvanAdi");
        var iseGirisRaw = Cell(sheet, row, headerMap, "IseGirisTarihi");
        var tckn = Cell(sheet, row, headerMap, "TCKN");

        if (!TryParseGender(cinsiyetRaw, out var gender, out var genderError))
            errors.Add(genderError!);

        if (!TryParseStatus(durumRaw, canSetStatus, out var status, out var statusError))
            errors.Add(statusError!);

        DateOnly? birth = null;
        if (!string.IsNullOrWhiteSpace(dogumRaw))
        {
            if (!TryParseDate(dogumRaw, out var d))
                errors.Add("DogumTarihi geçersiz (yyyy-MM-dd veya gg.aa.yyyy).");
            else
                birth = d;
        }

        DateOnly? hire = null;
        if (!string.IsNullOrWhiteSpace(iseGirisRaw))
        {
            if (!TryParseDate(iseGirisRaw, out var d))
                errors.Add("IseGirisTarihi geçersiz.");
            else
                hire = d;
        }

        Guid? unitId = null;
        if (!string.IsNullOrWhiteSpace(birimKodu))
        {
            if (!unitsByCode.TryGetValue(birimKodu.Trim(), out var uid))
                errors.Add($"BirimKodu bulunamadı: {birimKodu}");
            else
                unitId = uid;
        }

        Guid? titleId = null;
        if (!string.IsNullOrWhiteSpace(unvanAdi))
        {
            if (!titlesByName.TryGetValue(unvanAdi.Trim(), out var tid))
                errors.Add($"UnvanAdi bulunamadı: {unvanAdi}");
            else
                titleId = tid;
        }

        if (!string.IsNullOrWhiteSpace(sicil))
        {
            var key = sicil.Trim();
            if (!usedSicilsInFile.Add(key))
                errors.Add($"Dosya içinde tekrarlayan SicilNo: {key}");
        }

        var request = new CreateEmployeeRequest
        {
            FirstName = ad,
            LastName = soyad,
            EmployeeNumber = string.IsNullOrWhiteSpace(sicil) ? null : sicil.Trim(),
            Gender = gender,
            Status = status,
            BirthDate = birth,
            PersonalPhone = string.IsNullOrWhiteSpace(telefon) ? null : telefon.Trim(),
            PersonalEmail = string.IsNullOrWhiteSpace(eposta) ? null : eposta.Trim(),
            UnitId = unitId,
            JobTitleId = titleId,
            HireDate = hire,
            NationalId = canNationalId && !string.IsNullOrWhiteSpace(tckn) ? tckn.Trim() : null
        };

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            errors.AddRange(validation.Errors.Select(e => e.ErrorMessage).Distinct());

        if (errors.Count > 0)
        {
            return new EmployeeImportRowResultDto
            {
                RowNumber = row,
                Success = false,
                FullName = $"{ad} {soyad}".Trim(),
                Errors = errors
            };
        }

        try
        {
            var id = await _sender.Send(request, ct);
            return new EmployeeImportRowResultDto
            {
                RowNumber = row,
                Success = true,
                EmployeeId = id,
                FullName = $"{request.FirstName} {request.LastName}".Trim(),
                Errors = []
            };
        }
        catch (AppException ex)
        {
            return new EmployeeImportRowResultDto
            {
                RowNumber = row,
                Success = false,
                FullName = $"{request.FirstName} {request.LastName}".Trim(),
                Errors = [ex.Message]
            };
        }
        catch (Exception)
        {
            return new EmployeeImportRowResultDto
            {
                RowNumber = row,
                Success = false,
                FullName = $"{request.FirstName} {request.LastName}".Trim(),
                Errors = ["Beklenmeyen hata; satır atlandı."]
            };
        }
    }

    private void EnsureCanImport()
    {
        if (!_currentUser.HasPermission(PermissionCodes.ImportExcel))
            throw new ForbiddenException("Excel aktarımı için Import.Excel yetkisi gerekir.");
    }

    private static Dictionary<string, int> ReadHeaderMap(IXLWorksheet sheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var col = 1; col <= lastCol; col++)
        {
            var name = sheet.Cell(1, col).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (!map.ContainsKey(name))
                map[name] = col;
        }
        return map;
    }

    private static void ValidateHeaders(Dictionary<string, int> headerMap)
    {
        var missing = RequiredHeaders
            .Where(h => !headerMap.ContainsKey(h))
            .ToList();

        if (missing.Count > 0)
        {
            throw new AppValidationException("file",
                $"Eksik sütun başlıkları: {string.Join(", ", missing)}. Şablonu indirip kullanın.");
        }
    }

    private static string Cell(IXLWorksheet sheet, int row, Dictionary<string, int> map, string header)
    {
        if (!map.TryGetValue(header, out var col))
            return string.Empty;

        var cell = sheet.Cell(row, col);
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue(out DateTime dt))
            return dt.ToString("yyyy-MM-dd");

        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double num))
        {
            // Excel bazen sicil/telefonu sayı yazar
            if (Math.Abs(num % 1) < double.Epsilon)
                return ((long)num).ToString();
            return num.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return cell.GetString().Trim();
    }

    private static bool IsRowEmpty(IXLWorksheet sheet, int row, int columnCount)
    {
        for (var col = 1; col <= Math.Max(columnCount, 12); col++)
        {
            if (!string.IsNullOrWhiteSpace(sheet.Cell(row, col).GetString()))
                return false;
        }
        return true;
    }

    private static bool TryParseGender(string raw, out Gender gender, out string? error)
    {
        gender = Gender.Unspecified;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var v = raw.Trim().ToLowerInvariant();
        gender = v switch
        {
            "erkek" or "e" or "male" or "2" => Gender.Male,
            "kadın" or "kadin" or "k" or "female" or "1" => Gender.Female,
            "diğer" or "diger" or "other" or "3" => Gender.Other,
            "belirtilmedi" or "0" => Gender.Unspecified,
            _ => Gender.Unspecified
        };

        if (gender == Gender.Unspecified
            && v is not ("belirtilmedi" or "0" or ""))
        {
            error = $"Cinsiyet anlaşılamadı: {raw}";
            return false;
        }

        return true;
    }

    private static bool TryParseStatus(
        string raw,
        bool canSetStatus,
        out EmployeeStatus status,
        out string? error)
    {
        status = EmployeeStatus.Active;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var v = raw.Trim().ToLowerInvariant();
        EmployeeStatus? parsed = v switch
        {
            "aktif" or "1" => EmployeeStatus.Active,
            "pasif" or "2" => EmployeeStatus.Passive,
            "izinli" or "3" => EmployeeStatus.OnLeave,
            "uzun süreli izinli" or "uzun sureli izinli" or "4" => EmployeeStatus.LongTermLeave,
            "geçici görevli" or "gecici gorevli" or "5" => EmployeeStatus.TemporaryAssignment,
            "işten ayrıldı" or "isten ayrildi" or "6" => EmployeeStatus.LeftJob,
            "emekli" or "emekli oldu" or "7" => EmployeeStatus.Retired,
            "başka müdürlüğe geçti" or "baska mudurluge gecti" or "8"
                => EmployeeStatus.TransferredToOtherDirectorate,
            "askıda" or "askida" or "9" => EmployeeStatus.Suspended,
            "göreve dönmesi bekleniyor" or "goreve donmesi bekleniyor" or "10"
                => EmployeeStatus.AwaitingReturn,
            "görevden ayrıldı" or "gorevden ayrildi" or "11" => EmployeeStatus.DutyEnded,
            _ => null
        };

        if (parsed is null)
        {
            error = $"Durum anlaşılamadı: {raw}";
            return false;
        }

        status = parsed.Value;
        if (!canSetStatus && status != EmployeeStatus.Active)
        {
            error = "Durum değiştirme yetkiniz yok; yalnızca Aktif bırakın veya boş bırakın.";
            return false;
        }

        return true;
    }

    private static bool TryParseDate(string raw, out DateOnly date)
    {
        date = default;
        raw = raw.Trim();

        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out date))
            return true;
        if (DateOnly.TryParseExact(raw, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out date))
            return true;
        if (DateOnly.TryParseExact(raw, "d.M.yyyy", null, System.Globalization.DateTimeStyles.None, out date))
            return true;
        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"),
                System.Globalization.DateTimeStyles.None, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }
}
