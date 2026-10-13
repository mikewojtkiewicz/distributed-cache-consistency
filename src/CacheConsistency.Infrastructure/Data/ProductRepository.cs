using CacheConsistency.Core.Abstractions;
using CacheConsistency.Core.Models;
using Npgsql;

namespace CacheConsistency.Infrastructure.Data;

public sealed class ProductRepository : IProductRepository
{
    private readonly string _connString;

    public ProductRepository(string connString) => _connString = connString;

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql =
            "SELECT id, name, stock, price_cents, version FROM products WHERE id = @id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<Product?> TryUpdateStockAsync(
        int id, int newStock, long expectedVersion, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
            UPDATE products
               SET stock = @stock
             WHERE id = @id
               AND version = @expected
         RETURNING id, name, stock, price_cents, version";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("stock", newStock);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("expected", expectedVersion);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<(Product product, int retries)> AdjustStockAsync(
        int id, int delta, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var current = await GetByIdAsync(id, ct)
                          ?? throw new InvalidOperationException($"product {id} not found");

            var updated = await TryUpdateStockAsync(
                id, current.Stock + delta, current.Version, ct);

            if (updated is not null)
                return (updated, attempt);

            await Task.Delay(Random.Shared.Next(2, 15), ct);
        }
    }

    private static Product Map(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Name = r.GetString(1),
        Stock = r.GetInt32(2),
        PriceCents = r.GetInt32(3),
        Version = r.GetInt64(4),
    };
}
