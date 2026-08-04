using System.Data;
using Dapper;
using EfCoreMastery.Domain.Entities;

namespace EfCoreMastery.Infrastructure.Repositories;

public class DapperProductRepository(IDbConnection connection)
{
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        const string sql = "SELECT Id, Name, SKU, Price, Stock, CategoryId FROM Products WHERE IsDeleted = 0";
        return await connection.QueryAsync<Product>(sql);
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        const string sql = "SELECT Id, Name, SKU, Price, Stock, CategoryId FROM Products WHERE Id = @Id AND IsDeleted = 0";
        return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { Id = id });
    }

    public async Task<int> CreateAsync(Product product)
    {
        const string sql = @"
            INSERT INTO Products (Name, SKU, Price, Stock, CategoryId, TenantId, CreatedAt, IsDeleted)
            VALUES (@Name, @SKU, @Price, @Stock, @CategoryId, @TenantId, GETUTCDATE(), 0);
            SELECT CAST(SCOPE_IDENTITY() as int);";
        return await connection.ExecuteScalarAsync<int>(sql, product);
    }
}
