using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Notifications;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Notifications;

public sealed class UserNotificationService : IUserNotificationService
{
    private readonly AppDbContext _db;

    public UserNotificationService(AppDbContext db) => _db = db;

    public async Task NotifyAsync(
        Guid userId,
        string title,
        string body,
        NotificationSeverity severity = NotificationSeverity.Info,
        string category = "System",
        string? linkUrl = null,
        CancellationToken cancellationToken = default)
    {
        _db.UserNotifications.Add(CreateEntity(userId, title, body, severity, category, linkUrl));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyUsersWithPermissionAsync(
        string permissionCode,
        string title,
        string body,
        NotificationSeverity severity = NotificationSeverity.Info,
        string category = "System",
        string? linkUrl = null,
        CancellationToken cancellationToken = default)
    {
        var userIds = await ResolveUserIdsAsync(permissionCode, cancellationToken);
        if (userIds.Count == 0)
            return;

        foreach (var userId in userIds)
            _db.UserNotifications.Add(CreateEntity(userId, title, body, severity, category, linkUrl));

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyUsersWithPermissionUniqueAsync(
        string permissionCode,
        string title,
        string body,
        string category,
        string linkUrl,
        NotificationSeverity severity = NotificationSeverity.Warning,
        int dedupeDays = 7,
        CancellationToken cancellationToken = default)
    {
        var userIds = await ResolveUserIdsAsync(permissionCode, cancellationToken);
        if (userIds.Count == 0)
            return;

        var since = DateTime.UtcNow.AddDays(-Math.Max(1, dedupeDays));
        var existing = await _db.UserNotifications
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId)
                        && x.Category == category
                        && x.LinkUrl == linkUrl
                        && x.CreatedAtUtc >= since)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var skip = existing.ToHashSet();
        var added = 0;
        foreach (var userId in userIds)
        {
            if (skip.Contains(userId))
                continue;
            _db.UserNotifications.Add(CreateEntity(userId, title, body, severity, category, linkUrl));
            added++;
        }

        if (added > 0)
            await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Guid>> ResolveUserIdsAsync(string permissionCode, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Where(u => u.UserRoles.Any(ur =>
                ur.Role.RolePermissions.Any(rp => rp.Permission.Code == permissionCode)))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);

    private static UserNotification CreateEntity(
        Guid userId,
        string title,
        string body,
        NotificationSeverity severity,
        string category,
        string? linkUrl) =>
        new()
        {
            UserId = userId,
            Title = title.Trim(),
            Body = body.Trim(),
            Severity = severity,
            Category = string.IsNullOrWhiteSpace(category) ? NotificationCategories.System : category.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
            CreatedBy = "system"
        };
}

/// <summary>
/// Şartnamedeki örnek uyarıları tarar: eksik veri, sertifika, tesis kadro, pasif tesis,
/// birim sorumlusu, hatırlatmalı not, geçici görev bitişi.
/// </summary>
public sealed class NotificationScanService : INotificationScanService
{
    private const byte IncompleteThreshold = 80;
    private const int CertificateSoonDays = 90;
    private const int NoteReminderDays = 7;
    private const int TemporaryEndDays = 14;

    private readonly AppDbContext _db;
    private readonly IUserNotificationService _notifications;

    public NotificationScanService(AppDbContext db, IUserNotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<NotificationScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var summary = new List<string>();
        var created = 0;
        var skipped = 0;

        async Task Emit(
            string permission,
            string title,
            string body,
            string category,
            string linkUrl,
            NotificationSeverity severity,
            string summaryLine)
        {
            var before = await CountRecentAsync(category, linkUrl, cancellationToken);
            await _notifications.NotifyUsersWithPermissionUniqueAsync(
                permission, title, body, category, linkUrl, severity, 7, cancellationToken);
            var after = await CountRecentAsync(category, linkUrl, cancellationToken);
            var delta = Math.Max(0, after - before);
            if (delta > 0)
            {
                created += delta;
                summary.Add(summaryLine);
            }
            else
            {
                skipped++;
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1) Zorunlu bilgileri eksik personel
        var incomplete = await _db.Employees.AsNoTracking()
            .CountAsync(x => x.Status == EmployeeStatus.Active
                             && x.ProfileCompletionPercent < IncompleteThreshold, cancellationToken);
        if (incomplete > 0)
        {
            await Emit(
                PermissionCodes.DataQualityView,
                "Zorunlu personel bilgileri eksik",
                $"{incomplete} aktif personelin bilgi tamamlama oranı %{IncompleteThreshold} altında. Veri eksikleri ekranından tamamlayın.",
                NotificationCategories.DataQuality,
                "/data-quality?kind=missing",
                NotificationSeverity.Warning,
                $"{incomplete} personelde eksik zorunlu bilgi");
        }

        // 2) Sertifika süresi dolmak üzere / dolmuş
        var soonLimit = today.AddDays(CertificateSoonDays);
        var certSoon = await _db.EmployeeCertificates.AsNoTracking()
            .CountAsync(x => x.ExpiresOn != null
                             && x.ExpiresOn >= today
                             && x.ExpiresOn <= soonLimit, cancellationToken);
        var certExpired = await _db.EmployeeCertificates.AsNoTracking()
            .CountAsync(x => x.ExpiresOn != null && x.ExpiresOn < today, cancellationToken);

        if (certSoon > 0)
        {
            await Emit(
                PermissionCodes.EmployeesView,
                "Sertifika geçerlilik süresi dolmak üzere",
                $"{certSoon} sertifikanın süresi {CertificateSoonDays} gün içinde dolacak. Sertifikalar ekranından kontrol edin.",
                NotificationCategories.Certificates,
                "/certificates",
                NotificationSeverity.Warning,
                $"{certSoon} sertifika süresi yaklaşıyor");
        }

        if (certExpired > 0)
        {
            await Emit(
                PermissionCodes.EmployeesView,
                "Süresi dolmuş sertifikalar var",
                $"{certExpired} sertifikanın geçerlilik süresi bitmiş. Güncelleme veya yenileme gerekebilir.",
                NotificationCategories.Certificates,
                "/certificates",
                NotificationSeverity.Danger,
                $"{certExpired} sertifika süresi dolmuş");
        }

        // 3) Tesis ideal kadronun altında
        var facilities = await _db.OrganizationUnits.AsNoTracking()
            .Where(x => x.Type == OrganizationUnitType.Facility
                        && x.Status == OrganizationUnitStatus.Active
                        && x.IdealStaffCount != null
                        && x.IdealStaffCount > 0)
            .Select(x => new { x.Id, x.Name, Ideal = x.IdealStaffCount!.Value })
            .ToListAsync(cancellationToken);

        if (facilities.Count > 0)
        {
            var facilityIds = facilities.Select(f => f.Id).ToList();
            var activeCounts = await _db.Employees.AsNoTracking()
                .Where(e => e.Status == EmployeeStatus.Active
                            && e.FacilityId != null
                            && facilityIds.Contains(e.FacilityId.Value))
                .GroupBy(e => e.FacilityId!.Value)
                .Select(g => new { FacilityId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.FacilityId, x => x.Count, cancellationToken);

            var under = facilities
                .Select(f => new
                {
                    f.Name,
                    f.Ideal,
                    Actual = activeCounts.GetValueOrDefault(f.Id)
                })
                .Where(x => x.Actual < x.Ideal)
                .ToList();

            if (under.Count > 0)
            {
                var sample = string.Join(", ", under.Take(3).Select(u => $"{u.Name} ({u.Actual}/{u.Ideal})"));
                await Emit(
                    PermissionCodes.OrganizationView,
                    "Tesis kadrosu ideal sayının altında",
                    $"{under.Count} tesiste aktif personel ideal kadronun altında: {sample}.",
                    NotificationCategories.Staffing,
                    "/facilities",
                    NotificationSeverity.Warning,
                    $"{under.Count} tesiste kadro açığı");
            }
        }

        // 4) Pasif tesiste aktif personel
        var passiveFacilityStaff = await _db.Employees.AsNoTracking()
            .CountAsync(e => e.Status == EmployeeStatus.Active
                             && e.Facility != null
                             && e.Facility.Status != OrganizationUnitStatus.Active, cancellationToken);
        if (passiveFacilityStaff > 0)
        {
            await Emit(
                PermissionCodes.DataQualityView,
                "Pasif tesiste aktif personel görünüyor",
                $"{passiveFacilityStaff} aktif personel pasif veya kapalı tesise bağlı. Veri tutarsızlığı ekranından düzeltin.",
                NotificationCategories.DataQuality,
                "/data-quality?kind=inconsistency",
                NotificationSeverity.Danger,
                $"{passiveFacilityStaff} aktif personel pasif tesiste");
        }

        // 5) Birim sorumlusu tanımlanmamış
        var unitsWithoutManager = await _db.OrganizationUnits.AsNoTracking()
            .CountAsync(x => x.Type != OrganizationUnitType.Facility
                             && x.Status == OrganizationUnitStatus.Active
                             && x.ManagerEmployeeId == null, cancellationToken);
        if (unitsWithoutManager > 0)
        {
            await Emit(
                PermissionCodes.OrganizationView,
                "Birim sorumlusu tanımlanmamış",
                $"{unitsWithoutManager} aktif birimde sorumlu (yönetici) atanmamış.",
                NotificationCategories.Organization,
                "/units",
                NotificationSeverity.Warning,
                $"{unitsWithoutManager} birimde sorumlu yok");
        }

        // 6) Hatırlatma tarihli not yaklaşıyor
        var noteLimit = today.AddDays(NoteReminderDays);
        var dueNotes = await _db.EmployeeNotes.AsNoTracking()
            .Where(n => n.ReminderDate != null
                        && n.ReminderDate >= today
                        && n.ReminderDate <= noteLimit)
            .OrderBy(n => n.ReminderDate)
            .Take(20)
            .Select(n => new { n.EmployeeId, n.Title, n.ReminderDate, Emp = n.Employee.FirstName + " " + n.Employee.LastName })
            .ToListAsync(cancellationToken);

        if (dueNotes.Count > 0)
        {
            var first = dueNotes[0];
            await Emit(
                PermissionCodes.EmployeesView,
                "Hatırlatma tarihli personel notu yaklaşıyor",
                dueNotes.Count == 1
                    ? $"“{first.Title}” notunun hatırlatması {first.ReminderDate:dd.MM.yyyy} ({first.Emp})."
                    : $"{dueNotes.Count} personel notunun hatırlatma tarihi {NoteReminderDays} gün içinde. Örn: {first.Emp} — {first.Title}.",
                NotificationCategories.Notes,
                $"/employees/{first.EmployeeId}",
                NotificationSeverity.Info,
                $"{dueNotes.Count} not hatırlatması yaklaşıyor");
        }

        // 7) Geçici görevlendirme süresi bitmek üzere
        var tempLimit = today.AddDays(TemporaryEndDays);
        var tempEnding = await _db.EmployeeMovements.AsNoTracking()
            .CountAsync(m => m.MovementType == MovementType.TemporaryAssignment
                             && m.EndDate != null
                             && m.EndDate >= today
                             && m.EndDate <= tempLimit, cancellationToken);
        if (tempEnding > 0)
        {
            await Emit(
                PermissionCodes.EmployeesView,
                "Geçici görevlendirme süresi bitmek üzere",
                $"{tempEnding} geçici görevlendirmenin bitiş tarihi {TemporaryEndDays} gün içinde.",
                NotificationCategories.Assignments,
                "/employees",
                NotificationSeverity.Warning,
                $"{tempEnding} geçici görev bitişi yaklaşıyor");
        }

        // Yarın başlayan yayınlanmış etkinlikler
        var tomorrowStart = DateTime.UtcNow.Date.AddDays(1);
        var tomorrowEnd = tomorrowStart.AddDays(1);
        var eventsTomorrow = await _db.Events.AsNoTracking()
            .Where(e => e.Status == EventStatus.Published
                        && e.StartAtUtc >= tomorrowStart
                        && e.StartAtUtc < tomorrowEnd)
            .OrderBy(e => e.StartAtUtc)
            .Take(8)
            .Select(e => new { e.Id, e.Title, e.StartAtUtc })
            .ToListAsync(cancellationToken);

        foreach (var ev in eventsTomorrow)
        {
            await Emit(
                PermissionCodes.EventsView,
                "Yarın etkinlik var",
                $"{ev.Title} — {ev.StartAtUtc.ToLocalTime():g}",
                NotificationCategories.Events,
                $"/events/{ev.Id}",
                NotificationSeverity.Info,
                $"Yarın: {ev.Title}");
        }

        return new NotificationScanResult
        {
            CreatedCount = created,
            SkippedDuplicateCount = skipped,
            Summary = summary
        };
    }

    private Task<int> CountRecentAsync(string category, string linkUrl, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-7);
        return _db.UserNotifications.CountAsync(
            x => x.Category == category && x.LinkUrl == linkUrl && x.CreatedAtUtc >= since, ct);
    }
}

public sealed class RunNotificationScanHandler
    : IRequestHandler<RunNotificationScanCommand, NotificationScanResult>
{
    private readonly INotificationScanService _scan;
    private readonly ICurrentUserService _currentUser;

    public RunNotificationScanHandler(INotificationScanService scan, ICurrentUserService currentUser)
    {
        _scan = scan;
        _currentUser = currentUser;
    }

    public async Task<NotificationScanResult> Handle(
        RunNotificationScanCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Bildirim taraması için Notifications.View gerekir.");

        // Tarama operasyonel veri üretir; en azından DataQuality veya Organization yetkisi olsun.
        if (!_currentUser.HasPermission(PermissionCodes.DataQualityView)
            && !_currentUser.HasPermission(PermissionCodes.OrganizationView)
            && !_currentUser.HasPermission(PermissionCodes.SettingsManage))
        {
            throw new ForbiddenException("Bildirim taraması için ek yetki gerekir.");
        }

        return await _scan.ScanAsync(cancellationToken);
    }
}

public sealed class GetMyNotificationsHandler
    : IRequestHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyNotificationsHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<NotificationDto>> Handle(
        GetMyNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        EnsureCanView();
        var userId = RequireUserId();

        var query = _db.UserNotifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (request.UnreadOnly)
            query = query.Where(x => !x.IsRead);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(x => x.Category == request.Category);

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(request.Take)
            .Select(x => new NotificationDto
            {
                Id = x.Id,
                Title = x.Title,
                Body = x.Body,
                Severity = x.Severity,
                Category = x.Category,
                LinkUrl = x.LinkUrl,
                IsRead = x.IsRead,
                CreatedAtUtc = x.CreatedAtUtc,
                ReadAtUtc = x.ReadAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    private void EnsureCanView()
    {
        if (!_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Bildirimler için Notifications.View gerekir.");
    }

    private Guid RequireUserId() =>
        _currentUser.UserId ?? throw new ForbiddenException("Oturum gerekli.");
}

public sealed class GetUnreadNotificationCountHandler
    : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUnreadNotificationCountHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(
        GetUnreadNotificationCountQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.NotificationsView)
            || _currentUser.UserId is null)
            return 0;

        return await _db.UserNotifications.CountAsync(
            x => x.UserId == _currentUser.UserId && !x.IsRead,
            cancellationToken);
    }
}

public sealed class MarkNotificationReadHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Bildirimler için Notifications.View gerekir.");

        var userId = _currentUser.UserId
            ?? throw new ForbiddenException("Oturum gerekli.");

        var item = await _db.UserNotifications
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Bildirim bulunamadı.");

        if (item.IsRead)
            return;

        item.IsRead = true;
        item.ReadAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedBy = _currentUser.UserName ?? "system";
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class MarkAllNotificationsReadHandler
    : IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MarkAllNotificationsReadHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionCodes.NotificationsView))
            throw new ForbiddenException("Bildirimler için Notifications.View gerekir.");

        var userId = _currentUser.UserId
            ?? throw new ForbiddenException("Oturum gerekli.");

        var unread = await _db.UserNotifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var actor = _currentUser.UserName ?? "system";
        foreach (var item in unread)
        {
            item.IsRead = true;
            item.ReadAtUtc = now;
            item.UpdatedAtUtc = now;
            item.UpdatedBy = actor;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Uygulama açıkken periyodik bildirim taraması (6 saatte bir).</summary>
public sealed class NotificationScanHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationScanHostedService> _logger;

    public NotificationScanHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationScanHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // İlk tarama: uygulama ayağa kalkınca kısa gecikmeyle
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scan = scope.ServiceProvider.GetRequiredService<INotificationScanService>();
                var result = await scan.ScanAsync(stoppingToken);
                if (result.CreatedCount > 0)
                {
                    _logger.LogInformation(
                        "Bildirim taraması: {Created} yeni, {Skipped} atlandı. {Summary}",
                        result.CreatedCount,
                        result.SkippedDuplicateCount,
                        string.Join("; ", result.Summary));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Bildirim taraması başarısız.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
