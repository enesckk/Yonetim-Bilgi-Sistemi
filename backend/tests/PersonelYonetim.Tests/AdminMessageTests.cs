using Microsoft.Data.Sqlite;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Interfaces;
using PersonelYonetim.Application.Features.Messages;
using PersonelYonetim.Domain.Authorization;
using PersonelYonetim.Domain.Entities;
using PersonelYonetim.Infrastructure.Messages;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Tests;

public class AdminMessageTests
{
    [Fact]
    public async Task Both_directions_are_one_conversation_and_old_inactive_users_remain_visible()
    {
        await using var f = await Fixture.Create();
        f.B.IsActive = false; f.B.IsDeleted = true; await f.Db.SaveChangesAsync();
        var handler = new GetAdminConversationsHandler(f.Db, new User(true));
        var page = await handler.Handle(new(), default);
        Assert.Equal(2, page.TotalCount);
        var pair = Assert.Single(page.Items, x => x.MessageCount == 56);
        Assert.Equal(f.B.Id, pair.LastSenderUserId);
        Assert.Equal("reply", pair.LastBody);
        var search = await handler.Handle(new("archived.user"), default);
        Assert.Single(search.Items); Assert.Equal(56, search.Items[0].MessageCount);
        Assert.Single((await handler.Handle(new("İBRAHİM"), default)).Items);
        Assert.Single((await handler.Handle(new("ışık"), default)).Items);
        Assert.Empty((await handler.Handle(new("missing"), default)).Items);
        Assert.Empty((await handler.Handle(new(Page: 2), default)).Items);
    }

    [Fact]
    public async Task Admin_thread_is_paginated_in_time_order_and_does_not_mark_any_message_read()
    {
        await using var f = await Fixture.Create();
        var handler = new GetAdminConversationHandler(f.Db, new User(true));
        var first = await handler.Handle(new(f.A.Id, f.B.Id), default);
        var older = await handler.Handle(new(f.B.Id, f.A.Id, 2), default);
        Assert.Equal(56, first.TotalCount); Assert.Equal(50, first.Items.Count); Assert.Equal(6, older.Items.Count);
        Assert.Equal("reply", first.Items.Last().Body);
        Assert.True(older.Items.Last().SentAtUtc < first.Items.First().SentAtUtc);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(older.Items.Select(x => x.Id)));
        Assert.All(first.Items, m => Assert.False(m.Mine));
        Assert.Equal(1, await f.Db.DirectMessages.CountAsync(m => m.ReadAtUtc != null));
        Assert.Empty((await handler.Handle(new(f.B.Id, f.C.Id), default)).Items);
    }

    [Fact]
    public async Task Non_admin_is_denied_even_with_all_permissions_and_cannot_open_archive_files()
    {
        await using var f = await Fixture.Create();
        var director = new User(false);
        await Assert.ThrowsAsync<ForbiddenException>(() => new GetAdminConversationsHandler(f.Db, director).Handle(new(), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new GetAdminConversationHandler(f.Db, director).Handle(new(f.A.Id, f.B.Id), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new OpenAdminMessageAttachmentHandler(f.Db, director, new Files()).Handle(new(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Conversation_pages_keep_all_pairs_without_duplicates()
    {
        await using var f = await Fixture.Create();
        for (var i = 0; i < 23; i++) {
            var person = new AppUser { UserName = "extra" + i, Email = "extra" + i + "@test.local", DisplayName = "Extra " + i };
            f.Db.Users.Add(person);
            f.Db.DirectMessages.Add(new() { SenderUserId = f.A.Id, RecipientUserId = person.Id, Body = "extra", CreatedAtUtc = DateTime.UtcNow.AddMinutes(i) });
        }
        await f.Db.SaveChangesAsync();
        var handler = new GetAdminConversationsHandler(f.Db, new User(true));
        var first = await handler.Handle(new(), default); var second = await handler.Handle(new(Page: 2), default);
        Assert.Equal(25, first.TotalCount); Assert.Equal(20, first.Items.Count); Assert.Equal(5, second.Items.Count);
        Assert.Empty(first.Items.Select(x => (x.FirstUser.Id, x.SecondUser.Id)).Intersect(second.Items.Select(x => (x.FirstUser.Id, x.SecondUser.Id))));
        Assert.True(first.Items.Last().LastAtUtc >= second.Items.First().LastAtUtc);
    }

    [Fact]
    public async Task Admin_can_open_another_users_attachment_without_changing_read_state()
    {
        await using var f = await Fixture.Create();
        var msg = await f.Db.DirectMessages.FirstAsync();
        msg.AttachmentPath = "private/test.png"; msg.AttachmentFileName = "test.png"; msg.ReadAtUtc = null;
        await f.Db.SaveChangesAsync();
        var result = await new OpenAdminMessageAttachmentHandler(f.Db, new User(true), new Files()).Handle(new(msg.Id), default);
        await using var stream = result.Stream;
        Assert.Equal("image/png", result.ContentType); Assert.Equal("test.png", result.DownloadFileName);
        Assert.Null((await f.Db.DirectMessages.SingleAsync(x => x.Id == msg.Id)).ReadAtUtc);
        await Assert.ThrowsAsync<NotFoundException>(() => new OpenAdminMessageAttachmentHandler(f.Db, new User(true), new Files()).Handle(new(Guid.NewGuid()), default));
    }

    [LocalPostgresFact]
    public async Task Archive_queries_work_on_production_database_provider()
    {
        await using var f = await Fixture.Create(postgres: true);
        var list = await new GetAdminConversationsHandler(f.Db, new User(true)).Handle(new("archived.user"), default);
        Assert.Single(list.Items); Assert.Equal(56, list.Items[0].MessageCount);
        Assert.Single((await new GetAdminConversationsHandler(f.Db, new User(true)).Handle(new("İBRAHİM"), default)).Items);
        var rows = await new GetAdminConversationHandler(f.Db, new User(true)).Handle(new(f.A.Id, f.B.Id), default);
        Assert.Equal(50, rows.Items.Count); Assert.Equal("reply", rows.Items.Last().Body);
        Assert.Equal(1, await f.Db.DirectMessages.CountAsync(x => x.ReadAtUtc != null));
    }

    private sealed class User(bool admin) : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public string UserName => "reviewer";
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => admin && role == RoleCodes.SystemAdmin;
        public IReadOnlyCollection<string> Permissions => PermissionCatalog.All.Select(x => x.Code).ToArray();
        public bool HasPermission(string code) => true;
    }
    private sealed class Files : IFileStorageService
    {
        public Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType, string relativeFolder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteIfExistsAsync(string? path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredFileOpenResult?> OpenReadAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult<StoredFileOpenResult?>(new() { Stream = new MemoryStream([1, 2, 3]), ContentType = "image/png", DownloadFileName = "test.png" });
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public AppDbContext Db { get; private set; } = null!;
        public AppUser A { get; } = new() { UserName = "first", Email = "first@test.local", DisplayName = "First Person" };
        public AppUser B { get; } = new() { UserName = "archived.user", Email = "second@test.local", DisplayName = "İbrahim Işık" };
        public AppUser C { get; } = new() { UserName = "third", Email = "third@test.local", DisplayName = "Third Person" };
        private bool postgres;
        public static async Task<Fixture> Create(bool postgres = false)
        {
            var f = new Fixture { postgres = postgres };
            var builder = new DbContextOptionsBuilder<AppDbContext>();
            if (postgres) {
                var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("YBS_TEST_POSTGRES_CONNECTION")) { Database = "ybs_message_test_" + Guid.NewGuid().ToString("N") };
                builder.UseNpgsql(cs.ConnectionString);
            } else { await f.connection.OpenAsync(); builder.UseSqlite(f.connection); }
            f.Db = new(builder.Options);
            await f.Db.Database.EnsureCreatedAsync(); f.Db.AddRange(f.A, f.B, f.C);
            var start = DateTime.UtcNow.AddDays(-1);
            for (var i = 0; i < 55; i++) f.Db.DirectMessages.Add(new() { SenderUserId = f.A.Id, RecipientUserId = f.B.Id, Body = "message " + i, CreatedAtUtc = start.AddMinutes(i) });
            f.Db.DirectMessages.Add(new() { SenderUserId = f.B.Id, RecipientUserId = f.A.Id, Body = "reply", CreatedAtUtc = start.AddMinutes(55) });
            f.Db.DirectMessages.Add(new() { SenderUserId = f.A.Id, RecipientUserId = f.C.Id, Body = "separate", CreatedAtUtc = start.AddMinutes(56), ReadAtUtc = start.AddMinutes(57) });
            await f.Db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { if (postgres) await Db.Database.EnsureDeletedAsync(); await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
