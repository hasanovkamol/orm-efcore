using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using EFCore.BulkExtensions;
using EfCoreMastery.Domain.Entities;
using EfCoreMastery.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMastery.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Running EF Core Mastery Benchmarks...");
        var summary = BenchmarkRunner.Run<EfCoreBenchmarks>();
    }
}

[MemoryDiagnoser]
public class EfCoreBenchmarks
{
    private AppDbContext _context = null!;

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BenchmarkDb_{Guid.NewGuid()}")
            .Options;

        _context = new AppDbContext(options);

        // Seed data
        var category = new Category { Name = "Benchmark Category" };
        _context.Categories.Add(category);
        _context.SaveChanges();

        var products = Enumerable.Range(1, 1000).Select(i => new Product
        {
            Name = $"Product {i}",
            SKU = $"SKU-{i}",
            Price = i * 5,
            CategoryId = category.Id,
            TenantId = 1
        }).ToList();

        _context.Products.AddRange(products);
        _context.SaveChanges();
    }

    [Benchmark(Baseline = true)]
    public async Task<List<Product>> SelectWithTracking()
    {
        return await _context.Products.ToListAsync();
    }

    [Benchmark]
    public async Task<List<Product>> SelectAsNoTracking()
    {
        return await _context.Products.AsNoTracking().ToListAsync();
    }

    [Benchmark]
    public async Task<List<ProductDtoBenchmark>> SelectProjection()
    {
        return await _context.Products
            .AsNoTracking()
            .Select(p => new ProductDtoBenchmark
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price
            })
            .ToListAsync();
    }
}

public class ProductDtoBenchmark
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
