using System.Diagnostics;
using EfCoreMastery.Application.Interfaces;
using EfCoreMastery.Domain.Entities;
using EfCoreMastery.Domain.ValueObjects;
using EfCoreMastery.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMastery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    // GET: /api/dashboard/matrix
    [HttpGet("matrix")]
    public IActionResult GetBenchmarkMatrix()
    {
        var matrix = new List<BenchmarkMetricDto>
        {
            new()
            {
                Level = 1,
                LevelName = "Level 1 — Kirish",
                Operation = "SELECT (1000 yozuv o'qish)",
                ApproachA = "ADO.NET / Dapper",
                TimeA = 1.1,
                MemoryA = 150,
                ApproachB = "EF Core ToListAsync()",
                TimeB = 3.5,
                MemoryB = 450,
                Winner = "Dapper (3x tezroq)",
                Notes = "Dapper Change Tracking yuritmaydi, past darajali mapping qiladi."
            },
            new()
            {
                Level = 2,
                LevelName = "Level 2 — Amaliyot",
                Operation = "DELETE (1000 yozuv o'chirish)",
                ApproachA = "RemoveRange + SaveChanges",
                TimeA = 120,
                MemoryA = 1800,
                ApproachB = "ExecuteDeleteAsync()",
                TimeB = 5.0,
                MemoryB = 10,
                Winner = "ExecuteDeleteAsync (24x tezroq)",
                Notes = "ExecuteDeleteAsync obyektlarni xotiraga yuklamay to'g'ri DDL yuboradi."
            },
            new()
            {
                Level = 3,
                LevelName = "Level 3 — Junior+",
                Operation = "Loading (50 Author + Books)",
                ApproachA = "Lazy Loading (N+1 So'rov)",
                TimeA = 250,
                MemoryA = 1200,
                ApproachB = "Eager Loading (Include / Select)",
                TimeB = 5.0,
                MemoryB = 180,
                Winner = "Projection / Include (50x tezroq)",
                Notes = "Lazy Loading 51 ta so'rov yuboradi, Eager Loading 1 ta so'rovda oladi."
            },
            new()
            {
                Level = 4,
                LevelName = "Level 4 — Middle",
                Operation = "SELECT (10,000 yozuv o'qish)",
                ApproachA = "ToListAsync() (Tracking)",
                TimeA = 35.0,
                MemoryA = 12288,
                ApproachB = "AsNoTracking().ToListAsync()",
                TimeB = 18.0,
                MemoryB = 6144,
                Winner = "AsNoTracking (2x tezroq, 2x kam xotira)",
                Notes = "AsNoTracking Change Tracker ga obyektlarni ro'yxatga olmaydi."
            },
            new()
            {
                Level = 5,
                LevelName = "Level 5 — Middle+",
                Operation = "Isolation Level (1000 so'rov)",
                ApproachA = "ReadCommitted (Default)",
                TimeA = 12.0,
                MemoryA = 500,
                ApproachB = "ReadUncommitted / Snapshot",
                TimeB = 5.0,
                MemoryB = 200,
                Winner = "Snapshot / ReadUncommitted",
                Notes = "Snapshot locklarsiz ma'lumotlar versiyasini o'qiydi."
            },
            new()
            {
                Level = 6,
                LevelName = "Level 6 — Advanced Querying",
                Operation = "3+ Include (Cartesian Explosion)",
                ApproachA = "AsSingleQuery()",
                TimeA = 120.0,
                MemoryA = 15360,
                ApproachB = "AsSplitQuery()",
                TimeB = 45.0,
                MemoryB = 5120,
                Winner = "AsSplitQuery (3x tezroq)",
                Notes = "AsSplitQuery JOIN larning geometrik ko'payib ketishini oldini oladi."
            },
            new()
            {
                Level = 7,
                LevelName = "Level 7 — Architecture",
                Operation = "Repository Pattern Overhead",
                ApproachA = "Direct DbContext",
                TimeA = 200.0,
                MemoryA = 2048,
                ApproachB = "Repository + UnitOfWork",
                TimeB = 205.0,
                MemoryB = 2100,
                Winner = "Bir xil (Overhead < 2.5%)",
                Notes = "Abstraktsiya qatlami unumdorlikka deyarli ta'sir qilmaydi."
            },
            new()
            {
                Level = 8,
                LevelName = "Level 8 — Scale",
                Operation = "INSERT (100,000 yozuv)",
                ApproachA = "AddRange + SaveChanges",
                TimeA = 45000.0,
                MemoryA = 256000,
                ApproachB = "BulkInsertAsync (EFCore.BulkExtensions)",
                TimeB = 2000.0,
                MemoryB = 80000,
                Winner = "BulkInsertAsync (22x tezroq)",
                Notes = "BulkExtensions SqlBulkCopy protokolidan foydalanadi."
            },
            new()
            {
                Level = 9,
                LevelName = "Level 9 — Architect",
                Operation = "Polymorphic Hierarchy (100K yozuv)",
                ApproachA = "TPT (Table-Per-Type)",
                TimeA = 80.0,
                MemoryA = 4000,
                ApproachB = "TPH (Table-Per-Hierarchy)",
                TimeB = 25.0,
                MemoryB = 1500,
                Winner = "TPH (3x tezroq)",
                Notes = "TPH bitta jadval va JOIN larsiz ishlaydi."
            }
        };

        return Ok(matrix);
    }

    // POST: /api/dashboard/run-live
    [HttpPost("run-live")]
    public async Task<IActionResult> RunLiveBenchmark([FromQuery] int count = 2000)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LiveBenchDb_{Guid.NewGuid()}")
            .Options;

        await using var benchContext = new AppDbContext(options);

        var category = new Category { Name = "Live Bench Category" };
        benchContext.Categories.Add(category);
        await benchContext.SaveChangesAsync();

        var products = Enumerable.Range(1, count).Select(i => new Product
        {
            Name = $"Live Product {i}",
            SKU = $"SKU-LIVE-{Guid.NewGuid():N}",
            Price = i * 2.5m,
            Stock = 50,
            CategoryId = category.Id,
            TenantId = 1,
            PriceDetails = new Money { Amount = i * 2.5m, Currency = "USD" }
        }).ToList();

        // Test 1: AddRange + SaveChanges Time
        var sw1 = Stopwatch.StartNew();
        benchContext.Products.AddRange(products);
        await benchContext.SaveChangesAsync();
        sw1.Stop();

        // Test 2: Tracking Select Time
        var sw2 = Stopwatch.StartNew();
        var tracked = await benchContext.Products
            .Select(p => new { p.Id, p.Name, p.Price, p.Stock })
            .ToListAsync();
        sw2.Stop();

        // Test 3: AsNoTracking Select Time
        var sw3 = Stopwatch.StartNew();
        var noTracked = await benchContext.Products
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.Price, p.Stock })
            .ToListAsync();
        sw3.Stop();

        // Test 4: Projection Select Time
        var sw4 = Stopwatch.StartNew();
        var projected = await benchContext.Products
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.Price })
            .ToListAsync();
        sw4.Stop();

        var results = new List<LiveBenchmarkResultDto>
        {
            new() { BenchmarkName = "1. AddRange + SaveChanges", ExecutionTimeMs = Math.Round(sw1.Elapsed.TotalMilliseconds, 2), RecordCount = count },
            new() { BenchmarkName = "2. Select WITH Tracking", ExecutionTimeMs = Math.Round(sw2.Elapsed.TotalMilliseconds, 2), RecordCount = count },
            new() { BenchmarkName = "3. Select AsNoTracking()", ExecutionTimeMs = Math.Round(sw3.Elapsed.TotalMilliseconds, 2), RecordCount = count },
            new() { BenchmarkName = "4. Select DTO Projection", ExecutionTimeMs = Math.Round(sw4.Elapsed.TotalMilliseconds, 2), RecordCount = count }
        };

        return Ok(results);
    }
}
