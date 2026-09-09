using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Features.Events;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Settlements;

namespace PersonelYonetim.Infrastructure.Events;

internal static class EventSettlementSync
{
    public static async Task ApplyAsync(
        AppDbContext db,
        Event entity,
        IReadOnlyList<EventSettlementInputDto> inputs,
        string actor,
        CancellationToken ct)
    {
        var existing = await db.EventSettlements.IgnoreQueryFilters()
            .Where(x => x.EventId == entity.Id)
            .ToListAsync(ct);

        if (inputs.Count == 0)
        {
            if (existing.Count == 0 && entity.Latitude is double lat && entity.Longitude is double lng)
            {
                var hit = await FindByPointAsync(db, lat, lng, ct);
                if (hit is not null)
                {
                    db.EventSettlements.Add(new EventSettlement
                    {
                        EventId = entity.Id,
                        SettlementId = hit.Id,
                        AttendanceCount = entity.ExpectedAttendees ?? 0,
                        UniqueBeneficiaryCount = entity.ExpectedAttendees,
                        CreatedBy = actor
                    });
                }
            }

            return;
        }

        var ids = inputs.Select(x => x.SettlementId).Distinct().ToList();
        var found = await db.Settlements.AsNoTracking()
            .Where(s => ids.Contains(s.Id) && s.IsActive)
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (found.Count != ids.Count)
            throw new ValidationException("settlements", "Seçilen yerleşimlerden bazıları bulunamadı.");

        foreach (var row in existing)
        {
            if (!ids.Contains(row.SettlementId))
            {
                row.IsDeleted = true;
                row.DeletedAtUtc = DateTime.UtcNow;
                row.DeletedBy = actor;
            }
        }

        foreach (var input in inputs)
        {
            var current = existing.FirstOrDefault(x => x.SettlementId == input.SettlementId);
            if (current is null)
            {
                db.EventSettlements.Add(new EventSettlement
                {
                    EventId = entity.Id,
                    SettlementId = input.SettlementId,
                    AttendanceCount = input.AttendanceCount,
                    UniqueBeneficiaryCount = input.UniqueBeneficiaryCount,
                    Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                    CreatedBy = actor
                });
            }
            else
            {
                current.AttendanceCount = input.AttendanceCount;
                current.UniqueBeneficiaryCount = input.UniqueBeneficiaryCount;
                current.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
                current.IsDeleted = false;
                current.DeletedAtUtc = null;
                current.DeletedBy = null;
                current.UpdatedBy = actor;
                current.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
    }

    public static async Task<Settlement?> FindByPointAsync(
        AppDbContext db, double lat, double lng, CancellationToken ct)
    {
        var rows = await db.Settlements.AsNoTracking()
            .Where(s => s.IsActive && s.GeometryJson != null)
            .Select(s => new { s.Id, s.GeometryJson })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            if (SettlementGeoHelper.Contains(row.GeometryJson, lat, lng))
                return new Settlement { Id = row.Id };
        }

        return null;
    }
}
