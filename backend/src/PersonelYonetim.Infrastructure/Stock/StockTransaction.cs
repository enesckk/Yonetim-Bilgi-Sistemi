using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Npgsql;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Infrastructure.Persistence;

namespace PersonelYonetim.Infrastructure.Stock;

internal static class StockTransaction
{
    // The same isolation is used by stock movements, catalog changes and custody.
    // Concurrent assignments / outbound movements cannot reserve the same stock.
    public static async Task<T> RunAsync<T>(AppDbContext db, Func<Task<T>> action, CancellationToken ct)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var result = await action();
                await tx.CommitAsync(ct);
                return result;
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" }
            or SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Aynı kayıt başka bir işlemle oluşturulmuş. Listeyi yenileyip kontrol edin.");
        }
    }
}
