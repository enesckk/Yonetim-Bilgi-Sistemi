using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Domain.Enums;
using PersonelYonetim.Infrastructure.Notifications;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Tests;

public class EventPhotoReminderTests
{
    private sealed class Notifications : IUserNotificationService
    {
        public List<string> Titles { get; } = [];
        public Task NotifyAsync(Guid userId, string title, string body, NotificationSeverity severity = NotificationSeverity.Info, string category = "System", string? linkUrl = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyUsersWithPermissionAsync(string permissionCode, string title, string body, NotificationSeverity severity = NotificationSeverity.Info, string category = "System", string? linkUrl = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyUsersWithPermissionUniqueAsync(string permissionCode, string title, string body, string category, string linkUrl, NotificationSeverity severity = NotificationSeverity.Warning, int dedupeDays = 7, CancellationToken cancellationToken = default)
        { Titles.Add(title); return Task.CompletedTask; }
    }

    [Theory]
    [InlineData(EventStatus.Published, -1, false, true)]
    [InlineData(EventStatus.Published, 1, false, false)]
    [InlineData(EventStatus.Cancelled, -1, false, false)]
    [InlineData(EventStatus.Draft, -1, false, false)]
    [InlineData(EventStatus.Completed, -1, true, false)]
    public async Task Only_finished_events_without_photos_need_a_reminder(EventStatus status, int endOffset, bool hasPhoto, bool expected)
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        await db.Database.EnsureCreatedAsync();
        var ev = new Event { Title = "Festival", Status = status, StartAtUtc = DateTime.UtcNow.AddDays(-2), EndAtUtc = DateTime.UtcNow.AddDays(endOffset) };
        db.Events.Add(ev);
        db.EventMedia.Add(new EventMedia { EventId = ev.Id, Kind = hasPhoto ? "photo" : "poster" });
        await db.SaveChangesAsync();
        var notifications = new Notifications();
        await new NotificationScanService(db, notifications).ScanAsync();
        Assert.Equal(expected, notifications.Titles.Contains("Etkinlik görsellerini yükleyin"));
    }
}
