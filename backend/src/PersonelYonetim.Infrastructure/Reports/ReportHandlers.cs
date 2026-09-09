using System.Text.Json;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Reports;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Domain.Settings;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Security;

namespace PersonelYonetim.Infrastructure.Reports;

/// <summary>
/// Excel export — ClosedXML (import ile aynı kütüphane).
/// Fark: çıktıda hassas sütunlar yetkiye bağlı; satır limiti var.
/// </summary>
public sealed class ExportEmployeesExcelHandler
    : IRequestHandler<ExportEmployeesExcelQuery, ReportFileResult>
{
    public const int MaxRows = 5000;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INationalIdProtector _nationalId;
    private readonly IUserNotificationService _notifications;
    private readonly IAppSettingsService _settings;

    public ExportEmployeesExcelHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        INationalIdProtector nationalId,
        IUserNotificationService notifications,
        IAppSettingsService settings)
    {
        _db = db;
        _currentUser = currentUser;
        _nationalId = nationalId;
        _notifications = notifications;
        _settings = settings;
    }

    public async Task<ReportFileResult> Handle(
        ExportEmployeesExcelQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.ReportsExportExcel))
            throw new ForbiddenException("Excel dışa aktarım için Reports.ExportExcel gerekir.");

        if (!_currentUser.HasPermission(PermissionCodes.EmployeesView))
            throw new ForbiddenException("Personel verisi için Employees.View gerekir.");

        var canPhone = _currentUser.HasPermission(PermissionCodes.EmployeesViewPhone);
        var canAddress = _currentUser.HasPermission(PermissionCodes.EmployeesViewAddress);
        var canNationalId = _currentUser.HasPermission(PermissionCodes.EmployeesViewNationalId);

        var maxRows = await _settings.GetIntAsync(
            AppSettingKeys.ReportsExcelMaxRows,
            MaxRows,
            cancellationToken);

        var rows = await EmployeeExportQuery.LoadAsync(
            _db,
            _currentUser,
            request,
            maxRows,
            cancellationToken);

        // Export, SaveChanges üretmez — manuel audit (öğrenme: interceptor her şeyi görmez)
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "Export",
            EntityName = "Employee",
            EntityId = null,
            UserId = _currentUser.UserId?.ToString(),
            UserName = _currentUser.UserName ?? "system",
            NewValuesJson = JsonSerializer.Serialize(new
            {
                format = "xlsx",
                rowCount = rows.Count,
                search = request.Search,
                status = request.Status?.ToString(),
                unitId = request.UnitId,
                facilityId = request.FacilityId,
                employmentTypeId = request.EmploymentTypeId,
                includeSubUnits = request.IncludeSubUnits
            })
        });
        await _db.SaveChangesAsync(cancellationToken);

        if (_currentUser.UserId is Guid uid)
        {
            await _notifications.NotifyAsync(
                uid,
                "Excel dışa aktarım tamamlandı",
                $"{rows.Count} personel satırı Excel dosyasına yazıldı.",
                NotificationSeverity.Info,
                "Export",
                "/reports",
                cancellationToken);
        }

        return BuildWorkbook(rows, canPhone, canAddress, canNationalId, _nationalId);
    }

    private static ReportFileResult BuildWorkbook(
        List<Employee> rows,
        bool canPhone,
        bool canAddress,
        bool canNationalId,
        INationalIdProtector nationalId)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Personeller");

        var headers = new List<string>
        {
            "Ad", "Soyad", "SicilNo", "Durum", "Unvan", "Birim", "Tesis",
            "IstihdamTuru", "IseGirisTarihi", "ProfilTamamlanma"
        };
        if (canPhone)
        {
            headers.Add("Telefon");
            headers.Add("KurumsalTelefon");
        }
        if (canAddress)
            headers.Add("Adres");
        if (canNationalId)
            headers.Add("TCKN");
        headers.Add("OzelDurumVar");

        for (var i = 0; i < headers.Count; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);

        var r = 2;
        foreach (var e in rows)
        {
            var c = 1;
            sheet.Cell(r, c++).Value = e.FirstName;
            sheet.Cell(r, c++).Value = e.LastName;
            sheet.Cell(r, c++).Value = e.EmployeeNumber ?? "";
            sheet.Cell(r, c++).Value = EmployeeExportQuery.StatusLabel(e.Status);
            sheet.Cell(r, c++).Value = e.JobTitle?.Name ?? "";
            sheet.Cell(r, c++).Value = e.Unit?.Name ?? "";
            sheet.Cell(r, c++).Value = e.Facility?.Name ?? "";
            sheet.Cell(r, c++).Value = e.EmploymentType?.Name ?? "";
            sheet.Cell(r, c++).Value = e.HireDate?.ToString("yyyy-MM-dd") ?? "";
            sheet.Cell(r, c++).Value = e.ProfileCompletionPercent;
            if (canPhone)
            {
                sheet.Cell(r, c++).Value = e.PersonalPhone ?? "";
                sheet.Cell(r, c++).Value = e.CorporatePhone ?? "";
            }
            if (canAddress)
                sheet.Cell(r, c++).Value = e.Address ?? "";
            if (canNationalId)
                sheet.Cell(r, c++).Value = nationalId.Unprotect(e.SensitiveData?.NationalIdEncrypted) ?? "";
            sheet.Cell(r, c).Value = e.SpecialConditions.Count > 0 ? "Evet" : "Hayır";
            r++;
        }

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return new ReportFileResult
        {
            Content = ms.ToArray(),
            FileName = $"personel-rapor-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx"
        };
    }
}


public sealed class GetReportsSummaryHandler
    : IRequestHandler<GetReportsSummaryQuery, ReportsSummaryDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetReportsSummaryHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ReportsSummaryDto> Handle(
        GetReportsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.ReportsView))
            throw new ForbiddenException("Raporlar için Reports.View gerekir.");

        var employees = _db.Employees.AsNoTracking().AsQueryable();
        var scoped = await UnitScopeHelper.ApplyAsync(_db, _currentUser, employees, cancellationToken);
        if (scoped is null)
        {
            return new ReportsSummaryDto
            {
                TotalEmployees = 0,
                ByStatus = []
            };
        }

        employees = scoped;

        var total = await employees.CountAsync(cancellationToken);
        var byStatus = await employees
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var withSpecial = await employees.CountAsync(x => x.SpecialConditions.Any(), cancellationToken);
        var unitCount = await _db.OrganizationUnits.CountAsync(cancellationToken);

        return new ReportsSummaryDto
        {
            TotalEmployees = total,
            ActiveEmployees = byStatus.FirstOrDefault(x => x.Status == EmployeeStatus.Active)?.Count ?? 0,
            PassiveEmployees = byStatus.FirstOrDefault(x => x.Status == EmployeeStatus.Passive)?.Count ?? 0,
            OnLeaveEmployees = byStatus.FirstOrDefault(x => x.Status == EmployeeStatus.OnLeave)?.Count ?? 0,
            WithSpecialCondition = withSpecial,
            OrganizationUnitCount = unitCount,
            ByStatus = byStatus
                .OrderBy(x => x.Status)
                .Select(x => new StatusCountDto
                {
                    Status = x.Status,
                    StatusLabel = StatusLabel(x.Status),
                    Count = x.Count
                })
                .ToList()
        };

        static string StatusLabel(EmployeeStatus s) => s switch
        {
            EmployeeStatus.Active => "Aktif",
            EmployeeStatus.Passive => "Pasif",
            EmployeeStatus.OnLeave => "İzinli",
            EmployeeStatus.LeftJob => "İşten ayrıldı",
            _ => s.ToString()
        };
    }
}
