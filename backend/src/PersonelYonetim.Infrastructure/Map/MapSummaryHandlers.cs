using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Map;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Caching;
using PersonelYonetim.Infrastructure.Organization;
using PersonelYonetim.Infrastructure.Persistence;
using PersonelYonetim.Infrastructure.Settlements;

namespace PersonelYonetim.Infrastructure.Map;

public sealed class GetSettlementSummariesHandler
    : IRequestHandler<GetSettlementSummariesQuery, SettlementSummaryListDto>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    public GetSettlementSummariesHandler(AppDbContext db, ICurrentUserService currentUser, IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<SettlementSummaryListDto> Handle(
        GetSettlementSummariesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Haritayı görüntüleme yetkiniz yok.");

        var cacheKey =
            $"map:sum:{_cache.MapVersion()}:{request.FromUtc:o}|{request.ToUtc:o}|{request.Status}|{request.Category}|{request.Search}";
        if (_cache.TryGetValue(cacheKey, out SettlementSummaryListDto? cached) && cached is not null)
            return cached;

        var settlements = await _db.Settlements.AsNoTracking()
            .Where(s => s.IsActive)
            .Where(s => string.IsNullOrWhiteSpace(request.Search)
                || s.Name.Contains(request.Search)
                || (s.DisplayName != null && s.DisplayName.Contains(request.Search)))
            .Select(s => new
            {
                s.Id,
                s.OfficialCode,
                s.Name,
                s.DisplayName,
                s.SettlementType,
                s.IsRural,
                s.CentroidLat,
                s.CentroidLng,
                s.HeadmanName,
                s.HeadmanPhone,
                Pop = s.Populations
                    .OrderByDescending(p => p.Year)
                    .Select(p => new
                    {
                        p.Year,
                        p.Population,
                        p.MaleCount,
                        p.FemaleCount,
                        p.ChildCount,
                        p.Source,
                        p.IsOfficial
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var eventQuery = _db.EventSettlements.AsNoTracking()
            .Where(x => x.Event.Status != EventStatus.Cancelled && x.Event.Status != EventStatus.Draft);

        if (request.Status is EventStatus st)
            eventQuery = eventQuery.Where(x => x.Event.Status == st);

        if (request.FromUtc is DateTime from)
            eventQuery = eventQuery.Where(x => x.Event.StartAtUtc >= from);
        if (request.ToUtc is DateTime to)
            eventQuery = eventQuery.Where(x => x.Event.StartAtUtc <= to);
        if (!string.IsNullOrWhiteSpace(request.Category))
            eventQuery = eventQuery.Where(x => x.Event.Category == request.Category);

        var aggregates = await eventQuery
            .GroupBy(x => x.SettlementId)
            .Select(g => new
            {
                SettlementId = g.Key,
                ActivityCount = g.Select(x => x.EventId).Distinct().Count(),
                AttendanceCount = g.Sum(x => x.AttendanceCount),
                UniqueSum = g.Sum(x => x.UniqueBeneficiaryCount ?? 0),
                UniqueKnown = g.Count(x => x.UniqueBeneficiaryCount != null),
                UniqueTotal = g.Count(),
                LastActivityDate = g.Max(x => (DateTime?)x.Event.StartAtUtc)
            })
            .ToListAsync(cancellationToken);

        var byId = aggregates.ToDictionary(x => x.SettlementId);

        var items = settlements.Select(s =>
        {
            byId.TryGetValue(s.Id, out var agg);
            var activityCount = agg?.ActivityCount ?? 0;
            var attendance = agg?.AttendanceCount ?? 0;
            var hasUnique = agg is not null && agg.UniqueKnown == agg.UniqueTotal && agg.UniqueTotal > 0;
            int? unique = hasUnique ? agg!.UniqueSum : null;
            var population = s.Pop?.Population;
            decimal? rate = null;
            if (population is > 0 && activityCount > 0)
            {
                var numerator = hasUnique ? unique!.Value : attendance;
                rate = Math.Min(100m, Math.Round(numerator * 100m / population.Value, 1));
            }

            return new SettlementSummaryDto
            {
                SettlementId = s.Id,
                OfficialCode = s.OfficialCode,
                Name = s.Name,
                DisplayName = s.DisplayName,
                SettlementType = s.SettlementType,
                IsRural = s.IsRural,
                CentroidLat = s.CentroidLat,
                CentroidLng = s.CentroidLng,
                Population = population,
                MaleCount = s.Pop?.MaleCount,
                FemaleCount = s.Pop?.FemaleCount,
                ChildCount = s.Pop?.ChildCount,
                PopulationYear = s.Pop?.Year,
                PopulationSource = s.Pop?.Source,
                PopulationIsOfficial = s.Pop?.IsOfficial ?? false,
                ActivityCount = activityCount,
                AttendanceCount = attendance,
                UniqueBeneficiaryCount = unique,
                HasUniqueBeneficiaries = hasUnique,
                CoverageRate = rate,
                CoverageLevel = CoverageScale.Level(activityCount, rate),
                MetricLabel = "Kaplama",
                LastActivityDate = agg?.LastActivityDate,
                HeadmanName = s.HeadmanName,
                HeadmanPhone = s.HeadmanPhone
            };
        })
        .OrderBy(x => x.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), false))
        .ToList();

        var result = new SettlementSummaryListDto { Items = items };
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = AppCache.MapTtl
        });
        return result;
    }
}

public sealed class GetSettlementLookupsHandler
    : IRequestHandler<GetSettlementLookupsQuery, IReadOnlyList<SettlementLookupDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    public GetSettlementLookupsHandler(AppDbContext db, ICurrentUserService currentUser, IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<IReadOnlyList<SettlementLookupDto>> Handle(
        GetSettlementLookupsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Yerleşimleri görüntüleme yetkiniz yok.");

        if (string.IsNullOrWhiteSpace(request.Search)
            && _cache.TryGetValue(AppCache.SettlementsAll, out IReadOnlyList<SettlementLookupDto>? cached)
            && cached is not null)
            return cached;

        var q = _db.Settlements.AsNoTracking().Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(s => s.Name.Contains(term) || (s.DisplayName != null && s.DisplayName.Contains(term)));
        }

        var list = await q
            .OrderBy(s => s.Name)
            .Select(s => new SettlementLookupDto
            {
                Id = s.Id,
                OfficialCode = s.OfficialCode,
                Name = s.Name,
                IsRural = s.IsRural,
                HeadmanName = s.HeadmanName,
                HeadmanPhone = s.HeadmanPhone
            })
            .ToListAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Search))
        {
            _cache.Set(AppCache.SettlementsAll, (IReadOnlyList<SettlementLookupDto>)list, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = AppCache.LookupTtl
            });
        }

        return list;
    }
}

public sealed class GetSettlementDetailHandler
    : IRequestHandler<GetSettlementDetailQuery, SettlementSummaryDto>
{
    private readonly ISender _sender;
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetSettlementDetailHandler(ISender sender, AppDbContext db, ICurrentUserService currentUser)
    {
        _sender = sender;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SettlementSummaryDto> Handle(
        GetSettlementDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsView)
            && !_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Yerleşimleri görüntüleme yetkiniz yok.");

        var key = (request.Key ?? string.Empty).Trim();
        if (key.Length == 0)
            throw new NotFoundException("Yerleşim bulunamadı.");

        var list = await _sender.Send(new GetSettlementSummariesQuery(), cancellationToken);
        var hit = list.Items.FirstOrDefault(s =>
            string.Equals(s.OfficialCode, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.SettlementId.ToString(), key, StringComparison.OrdinalIgnoreCase));

        if (hit is null)
            throw new NotFoundException("Yerleşim bulunamadı.");

        var schools = await _db.SettlementSchools.AsNoTracking()
            .Where(x => x.SettlementId == hit.SettlementId)
            .Select(x => new SettlementSchoolDto
            {
                Id = x.Id,
                Name = x.Name,
                SchoolType = x.SchoolType,
                StudentCount = x.StudentCount,
                PrincipalName = x.PrincipalName,
                PrincipalPhone = x.PrincipalPhone
            })
            .ToListAsync(cancellationToken);

        schools = schools
            .OrderBy(x => SchoolTypeRank(x.SchoolType))
            .ThenBy(x => x.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), false))
            .ToList();

        var geometry = await _db.Settlements.AsNoTracking()
            .Where(s => s.Id == hit.SettlementId)
            .Select(s => s.GeometryJson)
            .FirstOrDefaultAsync(cancellationToken);

        var nameKey = FoldPlace(hit.Name);
        var facilityRows = await _db.OrganizationUnits.AsNoTracking()
            .Where(x => x.Type == OrganizationUnitType.Facility
                && x.Status != OrganizationUnitStatus.OutOfUse)
            .Select(x => new
            {
                x.Id,
                x.Name,
                CategoryName = x.FacilityCategory != null ? x.FacilityCategory.Name : null,
                x.Status,
                x.Address,
                x.Phone,
                ManagerName = x.ManagerEmployee != null
                    ? (x.ManagerEmployee.FirstName + " " + x.ManagerEmployee.LastName).Trim()
                    : null,
                x.Capacity,
                x.WorkingHours,
                x.Latitude,
                x.Longitude
            })
            .ToListAsync(cancellationToken);

        var facilities = facilityRows
            .Where(x =>
            {
                if (x.Latitude is double lat && x.Longitude is double lng
                    && SettlementGeoHelper.Contains(geometry, lat, lng))
                    return true;
                if (nameKey.Length < 5)
                    return false;
                var facKey = FoldPlace(x.Name);
                return facKey.StartsWith(nameKey, StringComparison.Ordinal)
                    || facKey.Contains(nameKey, StringComparison.Ordinal);
            })
            .Select(x => new SettlementFacilityDto
            {
                Id = x.Id,
                Name = x.Name,
                CategoryName = x.CategoryName,
                Status = (int)x.Status,
                StatusLabel = OrgLabels.Status(x.Status),
                Address = x.Address,
                Phone = x.Phone,
                ManagerName = string.IsNullOrWhiteSpace(x.ManagerName) ? null : x.ManagerName,
                Capacity = x.Capacity,
                WorkingHours = x.WorkingHours
            })
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .OrderBy(x => x.Status == (int)OrganizationUnitStatus.Active ? 0 : 1)
            .ThenBy(x => x.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), false))
            .ToList();

        var areas = await _db.SettlementAreas.AsNoTracking()
            .Where(x => x.SettlementId == hit.SettlementId)
            .OrderBy(x => x.AreaType)
            .ThenBy(x => x.Name)
            .Select(x => new SettlementAreaDto
            {
                Id = x.Id,
                Name = x.Name,
                AreaType = x.AreaType,
                Note = x.Note,
                Address = x.Address
            })
            .ToListAsync(cancellationToken);

        areas = areas
            .OrderBy(x => AreaTypeRank(x.AreaType))
            .ThenBy(x => x.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), false))
            .ToList();

        return new SettlementSummaryDto
        {
            SettlementId = hit.SettlementId,
            OfficialCode = hit.OfficialCode,
            Name = hit.Name,
            DisplayName = hit.DisplayName,
            SettlementType = hit.SettlementType,
            IsRural = hit.IsRural,
            CentroidLat = hit.CentroidLat,
            CentroidLng = hit.CentroidLng,
            Population = hit.Population,
            MaleCount = hit.MaleCount,
            FemaleCount = hit.FemaleCount,
            ChildCount = hit.ChildCount,
            PopulationYear = hit.PopulationYear,
            PopulationSource = hit.PopulationSource,
            PopulationIsOfficial = hit.PopulationIsOfficial,
            ActivityCount = hit.ActivityCount,
            AttendanceCount = hit.AttendanceCount,
            UniqueBeneficiaryCount = hit.UniqueBeneficiaryCount,
            HasUniqueBeneficiaries = hit.HasUniqueBeneficiaries,
            CoverageRate = hit.CoverageRate,
            CoverageLevel = hit.CoverageLevel,
            MetricLabel = hit.MetricLabel,
            LastActivityDate = hit.LastActivityDate,
            HeadmanName = hit.HeadmanName,
            HeadmanPhone = hit.HeadmanPhone,
            Schools = schools,
            Facilities = facilities,
            Areas = areas
        };
    }

    private static string FoldPlace(string name)
    {
        var s = name.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
        s = s.Replace("Ç", "C", StringComparison.Ordinal)
            .Replace("Ğ", "G", StringComparison.Ordinal)
            .Replace("İ", "I", StringComparison.Ordinal)
            .Replace("Ö", "O", StringComparison.Ordinal)
            .Replace("Ş", "S", StringComparison.Ordinal)
            .Replace("Ü", "U", StringComparison.Ordinal)
            .Replace("Â", "A", StringComparison.Ordinal);
        s = System.Text.RegularExpressions.Regex.Replace(
            s, @"MAHALLESI|MAHALLE|KOYU|KOY", string.Empty, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return System.Text.RegularExpressions.Regex.Replace(s, @"[^A-Z0-9]", string.Empty);
    }

    private static int SchoolTypeRank(string type) => type switch
    {
        "Anaokulu" => 0,
        "İlkokul" => 1,
        "Ortaokul" => 2,
        "Lise" => 3,
        "İmam Hatip" => 4,
        _ => 9
    };

    private static int AreaTypeRank(string type) => type switch
    {
        "Park" => 0,
        "Yeşil alan" => 1,
        "Meydan" => 2,
        "Açık etkinlik alanı" => 3,
        "Spor alanı" => 4,
        "Çocuk oyun alanı" => 5,
        _ => 9
    };
}

public sealed class UpsertSettlementPopulationHandler : IRequestHandler<UpsertSettlementPopulationCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    public UpsertSettlementPopulationHandler(AppDbContext db, ICurrentUserService currentUser, IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task Handle(UpsertSettlementPopulationCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.EventsManage))
            throw new ForbiddenException("Nüfus güncelleme yetkiniz yok.");

        var settlement = await _db.Settlements
            .FirstOrDefaultAsync(x => x.Id == request.SettlementId, cancellationToken)
            ?? throw new NotFoundException("Yerleşim bulunamadı.");

        var row = await _db.SettlementPopulations
            .FirstOrDefaultAsync(
                x => x.SettlementId == request.SettlementId && x.Year == request.Year,
                cancellationToken);

        var actor = _currentUser.UserName ?? "system";
        if (row is null)
        {
            _db.SettlementPopulations.Add(new Domain.Entities.SettlementPopulation
            {
                SettlementId = settlement.Id,
                Year = request.Year,
                Population = request.Population,
                MaleCount = request.MaleCount,
                FemaleCount = request.FemaleCount,
                ChildCount = request.ChildCount,
                Source = request.Source.Trim(),
                SourceReference = string.IsNullOrWhiteSpace(request.SourceReference)
                    ? null
                    : request.SourceReference.Trim(),
                IsOfficial = request.IsOfficial,
                CreatedBy = actor
            });
        }
        else
        {
            row.Population = request.Population;
            row.MaleCount = request.MaleCount;
            row.FemaleCount = request.FemaleCount;
            row.ChildCount = request.ChildCount;
            row.Source = request.Source.Trim();
            row.SourceReference = string.IsNullOrWhiteSpace(request.SourceReference)
                ? null
                : request.SourceReference.Trim();
            row.IsOfficial = request.IsOfficial;
            row.UpdatedBy = actor;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _cache.InvalidateMapSummaries();
        _cache.Remove(AppCache.SettlementsAll);
    }
}
