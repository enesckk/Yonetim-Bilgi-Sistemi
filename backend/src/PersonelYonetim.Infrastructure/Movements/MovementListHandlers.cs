using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Movements;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Reports;

namespace PersonelYonetim.Infrastructure.Movements;

public static class MovementLabels
{
    public static string For(MovementType type) => type switch
    {
        MovementType.UnitChange => "Birim değişikliği",
        MovementType.FacilityChange => "Tesis değişikliği",
        MovementType.DutyChange => "Görev değişikliği",
        MovementType.TitleChange => "Unvan değişikliği",
        MovementType.TemporaryAssignment => "Geçici görevlendirme",
        MovementType.PermanentAssignment => "Kalıcı görevlendirme",
        MovementType.AdditionalDutyAssigned => "Ek görev",
        MovementType.DutyRemoved => "Görevden alma",
        MovementType.TransferToOtherDirectorate => "Başka müdürlüğe geçiş",
        MovementType.LeftJob => "İşten ayrılma",
        MovementType.Retirement => "Emeklilik",
        MovementType.ReturnToDuty => "Göreve dönüş",
        _ => type.ToString()
    };

    public static IReadOnlyList<MovementTypeOptionDto> AllOptions() =>
        Enum.GetValues<MovementType>()
            .OrderBy(x => (byte)x)
            .Select(x => new MovementTypeOptionDto { Value = (int)x, Label = For(x) })
            .ToList();
}

internal static class MovementListQueryCore
{
    public static async Task<(List<MovementListItemDto> Items, int TotalBeforeTake)> LoadAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        GetMovementListQuery filter,
        int? takeOverride,
        CancellationToken ct)
    {
        if (!currentUser.HasPermission(PermissionCodes.MovementsView))
            throw new ForbiddenException("Görev geçmişi için Movements.View gerekir.");

        var employees = db.Employees.AsNoTracking().AsQueryable();
        var scoped = await UnitScopeHelper.ApplyAsync(db, currentUser, employees, ct);
        if (scoped is null)
            return ([], 0);

        var scopedIds = scoped.Select(e => e.Id);

        var query = db.EmployeeMovements.AsNoTracking()
            .Where(m => scopedIds.Contains(m.EmployeeId));

        if (filter.MovementType.HasValue)
            query = query.Where(m => m.MovementType == filter.MovementType);

        if (filter.From.HasValue)
            query = query.Where(m => m.StartDate >= filter.From);

        if (filter.To.HasValue)
            query = query.Where(m => m.StartDate <= filter.To);

        if (filter.UnitId.HasValue)
        {
            var unitId = filter.UnitId.Value;
            query = query.Where(m =>
                m.OldUnitId == unitId || m.NewUnitId == unitId || m.Employee.UnitId == unitId);
        }

        if (filter.FacilityId.HasValue)
        {
            var facilityId = filter.FacilityId.Value;
            query = query.Where(m =>
                m.OldFacilityId == facilityId
                || m.NewFacilityId == facilityId
                || m.Employee.FacilityId == facilityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var q = filter.Search.Trim().ToLower();
            query = query.Where(m =>
                (m.Employee.FirstName + " " + m.Employee.LastName).ToLower().Contains(q)
                || (m.Employee.EmployeeNumber != null && m.Employee.EmployeeNumber.ToLower().Contains(q))
                || (m.Reason != null && m.Reason.ToLower().Contains(q))
                || (m.Description != null && m.Description.ToLower().Contains(q)));
        }

        var total = await query.CountAsync(ct);
        var take = takeOverride ?? filter.Take;

        var rows = await query
            .OrderByDescending(m => m.StartDate)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Take(take)
            .Select(m => new
            {
                m.Id,
                m.EmployeeId,
                EmployeeName = m.Employee.FirstName + " " + m.Employee.LastName,
                m.Employee.EmployeeNumber,
                CurrentUnitName = m.Employee.Unit != null ? m.Employee.Unit.Name : null,
                m.MovementType,
                m.StartDate,
                m.EndDate,
                OldUnitName = m.OldUnitId != null
                    ? db.OrganizationUnits.Where(u => u.Id == m.OldUnitId).Select(u => u.Name).FirstOrDefault()
                    : null,
                NewUnitName = m.NewUnitId != null
                    ? db.OrganizationUnits.Where(u => u.Id == m.NewUnitId).Select(u => u.Name).FirstOrDefault()
                    : null,
                OldFacilityName = m.OldFacilityId != null
                    ? db.OrganizationUnits.Where(u => u.Id == m.OldFacilityId).Select(u => u.Name).FirstOrDefault()
                    : null,
                NewFacilityName = m.NewFacilityId != null
                    ? db.OrganizationUnits.Where(u => u.Id == m.NewFacilityId).Select(u => u.Name).FirstOrDefault()
                    : null,
                OldJobTitleName = m.OldJobTitleId != null
                    ? db.JobTitles.Where(t => t.Id == m.OldJobTitleId).Select(t => t.Name).FirstOrDefault()
                    : null,
                NewJobTitleName = m.NewJobTitleId != null
                    ? db.JobTitles.Where(t => t.Id == m.NewJobTitleId).Select(t => t.Name).FirstOrDefault()
                    : null,
                OldJobDutyName = m.OldJobDutyId != null
                    ? db.JobDuties.Where(d => d.Id == m.OldJobDutyId).Select(d => d.Name).FirstOrDefault()
                    : null,
                NewJobDutyName = m.NewJobDutyId != null
                    ? db.JobDuties.Where(d => d.Id == m.NewJobDutyId).Select(d => d.Name).FirstOrDefault()
                    : null,
                m.Reason,
                m.Description,
                m.ApprovedBy,
                m.CreatedBy,
                m.CreatedAtUtc
            })
            .ToListAsync(ct);

        var items = rows.Select(m => new MovementListItemDto
        {
            Id = m.Id,
            EmployeeId = m.EmployeeId,
            EmployeeName = m.EmployeeName.Trim(),
            EmployeeNumber = m.EmployeeNumber,
            CurrentUnitName = m.CurrentUnitName,
            MovementType = (int)m.MovementType,
            MovementTypeLabel = MovementLabels.For(m.MovementType),
            StartDate = m.StartDate,
            EndDate = m.EndDate,
            OldUnitName = m.OldUnitName,
            NewUnitName = m.NewUnitName,
            OldFacilityName = m.OldFacilityName,
            NewFacilityName = m.NewFacilityName,
            OldJobTitleName = m.OldJobTitleName,
            NewJobTitleName = m.NewJobTitleName,
            OldJobDutyName = m.OldJobDutyName,
            NewJobDutyName = m.NewJobDutyName,
            Reason = m.Reason,
            Description = m.Description,
            ApprovedBy = m.ApprovedBy,
            CreatedBy = m.CreatedBy,
            CreatedAtUtc = m.CreatedAtUtc
        }).ToList();

        return (items, total);
    }

    public static async Task<List<MovementTypeStatDto>> StatsAsync(
        AppDbContext db,
        ICurrentUserService currentUser,
        GetMovementListQuery filter,
        CancellationToken ct)
    {
        var employees = db.Employees.AsNoTracking().AsQueryable();
        var scoped = await UnitScopeHelper.ApplyAsync(db, currentUser, employees, ct);
        if (scoped is null)
            return [];

        var scopedIds = scoped.Select(e => e.Id);
        var query = db.EmployeeMovements.AsNoTracking()
            .Where(m => scopedIds.Contains(m.EmployeeId));

        // Stats ignore movementType chip so all chips stay visible; other filters apply.
        if (filter.From.HasValue)
            query = query.Where(m => m.StartDate >= filter.From);
        if (filter.To.HasValue)
            query = query.Where(m => m.StartDate <= filter.To);
        if (filter.UnitId.HasValue)
        {
            var unitId = filter.UnitId.Value;
            query = query.Where(m =>
                m.OldUnitId == unitId || m.NewUnitId == unitId || m.Employee.UnitId == unitId);
        }
        if (filter.FacilityId.HasValue)
        {
            var facilityId = filter.FacilityId.Value;
            query = query.Where(m =>
                m.OldFacilityId == facilityId
                || m.NewFacilityId == facilityId
                || m.Employee.FacilityId == facilityId);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var q = filter.Search.Trim().ToLower();
            query = query.Where(m =>
                (m.Employee.FirstName + " " + m.Employee.LastName).ToLower().Contains(q)
                || (m.Employee.EmployeeNumber != null && m.Employee.EmployeeNumber.ToLower().Contains(q))
                || (m.Reason != null && m.Reason.ToLower().Contains(q)));
        }

        var groups = await query
            .GroupBy(m => m.MovementType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return groups
            .OrderByDescending(g => g.Count)
            .Select(g => new MovementTypeStatDto
            {
                MovementType = (int)g.Type,
                Label = MovementLabels.For(g.Type),
                Count = g.Count
            })
            .ToList();
    }
}

public sealed class GetMovementListHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<GetMovementListQuery, MovementListResultDto>
{
    public async Task<MovementListResultDto> Handle(GetMovementListQuery request, CancellationToken ct)
    {
        var (items, total) = await MovementListQueryCore.LoadAsync(db, currentUser, request, null, ct);
        var stats = await MovementListQueryCore.StatsAsync(db, currentUser, request, ct);

        return new MovementListResultDto
        {
            TotalCount = total,
            ReturnedCount = items.Count,
            TypeStats = stats,
            Items = items,
            MovementTypes = MovementLabels.AllOptions()
        };
    }
}

public sealed class ExportMovementListExcelHandler(AppDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<ExportMovementListExcelQuery, MovementExportFileDto>
{
    public async Task<MovementExportFileDto> Handle(ExportMovementListExcelQuery request, CancellationToken ct)
    {
        if (!currentUser.HasPermission(PermissionCodes.MovementsView))
            throw new ForbiddenException("Görev geçmişi için Movements.View gerekir.");

        var filter = new GetMovementListQuery
        {
            Search = request.Search,
            UnitId = request.UnitId,
            FacilityId = request.FacilityId,
            MovementType = request.MovementType,
            From = request.From,
            To = request.To,
            Take = 500
        };

        var (items, _) = await MovementListQueryCore.LoadAsync(db, currentUser, filter, 2000, ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Görev geçmişi");
        var headers = new[]
        {
            "Personel", "Sicil", "Birim", "Hareket", "Başlangıç", "Bitiş",
            "Eski birim", "Yeni birim", "Eski tesis", "Yeni tesis",
            "Eski unvan", "Yeni unvan", "Eski görev", "Yeni görev",
            "Neden", "Onaylayan", "Kayıt"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var m in items)
        {
            ws.Cell(r, 1).Value = m.EmployeeName;
            ws.Cell(r, 2).Value = m.EmployeeNumber;
            ws.Cell(r, 3).Value = m.CurrentUnitName;
            ws.Cell(r, 4).Value = m.MovementTypeLabel;
            ws.Cell(r, 5).Value = m.StartDate.ToString("dd.MM.yyyy");
            ws.Cell(r, 6).Value = m.EndDate?.ToString("dd.MM.yyyy");
            ws.Cell(r, 7).Value = m.OldUnitName;
            ws.Cell(r, 8).Value = m.NewUnitName;
            ws.Cell(r, 9).Value = m.OldFacilityName;
            ws.Cell(r, 10).Value = m.NewFacilityName;
            ws.Cell(r, 11).Value = m.OldJobTitleName;
            ws.Cell(r, 12).Value = m.NewJobTitleName;
            ws.Cell(r, 13).Value = m.OldJobDutyName;
            ws.Cell(r, 14).Value = m.NewJobDutyName;
            ws.Cell(r, 15).Value = m.Reason;
            ws.Cell(r, 16).Value = m.ApprovedBy;
            ws.Cell(r, 17).Value = m.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            r++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new MovementExportFileDto
        {
            Content = ms.ToArray(),
            FileName = $"gorev-gecmisi-{DateTime.Now:yyyyMMdd-HHmm}.xlsx"
        };
    }
}
