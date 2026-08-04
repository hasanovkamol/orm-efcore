# Level 8 — Senior+ (Scale & Performance)

---

## 1. Darslik

### 8.1 Bulk Insert / Update / Delete

EF Core ning standart `AddRange` + `SaveChanges` usuli katta hajmdagi ma'lumotlar uchun sekin. **EFCore.BulkExtensions** yoki **EF Core 7+** ning yangi metodlari bu muammoni hal qiladi.

```csharp
// ❌ Oddiy usul — 100,000 yozuv uchun juda sekin
public async Task SlowInsertAsync(List<Product> products)
{
    context.Products.AddRange(products);
    await context.SaveChangesAsync();
    // ~30-60 sekund (100K yozuv uchun)
    // Har biri uchun INSERT generatsiya qiladi
}

// ✅ EF Core 7+ — ExecuteUpdateAsync / ExecuteDeleteAsync (DDL)
public async Task BulkUpdateNativeAsync(decimal percentage)
{
    await context.Products
        .Where(p => p.CategoryId == 5)
        .ExecuteUpdateAsync(s =>
            s.SetProperty(p => p.Price, p => p.Price * (1 + percentage / 100))
             .SetProperty(p => p.UpdatedAt, DateTime.UtcNow));
    // ~50ms (100K yozuv uchun) — bitta SQL UPDATE
}

public async Task BulkDeleteNativeAsync()
{
    await context.Products
        .Where(p => p.IsDeleted && p.DeletedAt < DateTime.UtcNow.AddMonths(-6))
        .ExecuteDeleteAsync();
    // ~30ms — bitta SQL DELETE
}
```

```csharp
// ✅ EFCore.BulkExtensions (3rd party — eng tez)
// dotnet add package EFCore.BulkExtensions

public class BulkService(AppDbContext context)
{
    // Bulk Insert — SqlBulkCopy orqali
    public async Task BulkInsertAsync(List<Product> products)
    {
        await context.BulkInsertAsync(products);
        // ~1-2 sekund (100K yozuv uchun)
    }

    // Bulk Update
    public async Task BulkUpdateAsync(List<Product> products)
    {
        foreach (var p in products)
            p.Price *= 1.1m;

        await context.BulkUpdateAsync(products);
    }

    // Bulk Insert or Update (Upsert)
    public async Task BulkUpsertAsync(List<Product> products)
    {
        await context.BulkInsertOrUpdateAsync(products);
        // MERGE statement ishlatadi
    }

    // Bulk Delete
    public async Task BulkDeleteAsync(List<Product> products)
    {
        await context.BulkDeleteAsync(products);
    }

    // Konfiguratsiya bilan
    public async Task BulkInsertConfiguredAsync(List<Product> products)
    {
        await context.BulkInsertAsync(products, config =>
        {
            config.BatchSize = 5000;        // Har bir batch hajmi
            config.SetOutputIdentity = true; // Id larni qaytarish
            config.PreserveInsertOrder = true;
        });
    }
}
```

---

### 8.2 Multi-Tenancy

**Multi-tenancy** — bitta ilova bir nechta tashkilot (tenant) ga xizmat qilishi. Ma'lumotlar izolyatsiyasining 3 ta usuli:

```csharp
// 1-usul: Database-per-tenant (har bir tenant uchun alohida DB)
public class TenantDbContextFactory(
    ITenantService tenantService,
    IConfiguration configuration)
{
    public AppDbContext CreateContext()
    {
        var tenant = tenantService.GetCurrentTenant();
        var connectionString = configuration
            .GetConnectionString($"Tenant_{tenant.Id}");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}

// 2-usul: Schema-per-tenant (bitta DB, har bir tenant uchun alohida schema)
public class MultiTenantDbContext : DbContext
{
    private readonly string _schema;

    public MultiTenantDbContext(DbContextOptions options, ITenantService tenantService)
        : base(options)
    {
        _schema = tenantService.GetCurrentTenant().SchemaName;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema); // tenant_1, tenant_2, ...
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MultiTenantDbContext).Assembly);
    }
}
```

```csharp
// 3-usul: Row-level (bitta jadval, TenantId ustuni bilan filter)
// Eng ko'p ishlatiladigan usul

public interface ITenantService
{
    int GetCurrentTenantId();
}

public class TenantService(IHttpContextAccessor httpContextAccessor) : ITenantService
{
    public int GetCurrentTenantId()
    {
        // Header, claim, yoki subdomain dan olish
        var tenantClaim = httpContextAccessor.HttpContext?
            .User.FindFirst("tenant_id")?.Value;

        return int.TryParse(tenantClaim, out var id) ? id : throw new UnauthorizedAccessException();
    }
}

// Multi-tenant entity
public interface IMultiTenant
{
    int TenantId { get; set; }
}

public class Product : IMultiTenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TenantId { get; set; } // Tenant filter
}

// DbContext — global query filter
public class MultiTenantDbContext(
    DbContextOptions<MultiTenantDbContext> options,
    ITenantService tenantService) : DbContext(options)
{
    private readonly int _tenantId = tenantService.GetCurrentTenantId();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Barcha IMultiTenant entitylarga avtomatik filter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(IMultiTenant.TenantId));
                // _tenantId ni closure orqali olish
                var tenantIdExpr = Expression.Constant(_tenantId);
                var condition = Expression.Equal(property, tenantIdExpr);
                var lambda = Expression.Lambda(condition, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MultiTenantDbContext).Assembly);
    }

    // SaveChanges da TenantId ni avtomatik qo'shish
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<IMultiTenant>()
            .Where(e => e.State == EntityState.Added))
        {
            entry.Entity.TenantId = _tenantId;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

---

### 8.3 Sharding

**Sharding** — ma'lumotlarni bir nechta ma'lumotlar bazasiga taqsimlash (horizontal partitioning).

```csharp
// Sharding strategiyasi — hash-based
public class ShardingService(IConfiguration configuration)
{
    private const int ShardCount = 4;

    // Qaysi shard ga yozish/o'qishni aniqlash
    public string GetConnectionString(int entityId)
    {
        var shardIndex = entityId % ShardCount; // 0, 1, 2, 3
        return configuration.GetConnectionString($"Shard_{shardIndex}")!;
    }

    public AppDbContext GetShardContext(int entityId)
    {
        var connectionString = GetConnectionString(entityId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AppDbContext(options);
    }

    // Barcha shardlardan so'rov
    public async Task<List<Product>> SearchAllShardsAsync(string name)
    {
        var tasks = Enumerable.Range(0, ShardCount).Select(async i =>
        {
            var connStr = configuration.GetConnectionString($"Shard_{i}")!;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connStr)
                .Options;

            await using var ctx = new AppDbContext(options);
            return await ctx.Products
                .AsNoTracking()
                .Where(p => p.Name.Contains(name))
                .ToListAsync();
        });

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }
}
```

---

### 8.4 Parallel Query muammolari

EF Core ning `DbContext` i **thread-safe emas**. Parallel operatsiyalar uchun `IDbContextFactory` ishlatish shart.

```csharp
public class ParallelQueryService(IDbContextFactory<AppDbContext> contextFactory)
{
    // ❌ XATO — bitta DbContext ni parallel ishlatish
    public async Task DangerousParallelAsync()
    {
        // Bu CRASH qiladi!
        // await using var context = ...;
        // await Task.WhenAll(
        //     context.Products.ToListAsync(),
        //     context.Categories.ToListAsync());
    }

    // ✅ TO'G'RI — har bir task uchun alohida DbContext
    public async Task SafeParallelAsync()
    {
        var productsTask = Task.Run(async () =>
        {
            await using var ctx = await contextFactory.CreateDbContextAsync();
            return await ctx.Products.AsNoTracking().ToListAsync();
        });

        var categoriesTask = Task.Run(async () =>
        {
            await using var ctx = await contextFactory.CreateDbContextAsync();
            return await ctx.Categories.AsNoTracking().ToListAsync();
        });

        var (products, categories) = (await productsTask, await categoriesTask);
    }

    // Chunked parallel processing
    public async Task ProcessInParallelAsync(List<int> ids, int parallelism = 4)
    {
        var semaphore = new SemaphoreSlim(parallelism);

        var tasks = ids.Select(async id =>
        {
            await semaphore.WaitAsync();
            try
            {
                await using var ctx = await contextFactory.CreateDbContextAsync();
                var product = await ctx.Products.FindAsync(id);
                if (product is not null)
                {
                    product.LastProcessed = DateTime.UtcNow;
                    await ctx.SaveChangesAsync();
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
```

---

### 8.5 Query Caching va CompiledQuery

```csharp
public class CachedQueryService(AppDbContext context, IMemoryCache cache)
{
    // 1. Application-level cache
    public async Task<List<Category>> GetCategoriesCachedAsync()
    {
        return await cache.GetOrCreateAsync("all_categories", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await context.Categories.AsNoTracking().ToListAsync();
        }) ?? [];
    }

    // 2. CompiledQuery — LINQ → SQL tarjima qilishni cache qiladi
    private static readonly Func<AppDbContext, int, Task<Product?>>
        GetProductById = EF.CompileAsyncQuery(
            (AppDbContext ctx, int id) =>
                ctx.Products.FirstOrDefault(p => p.Id == id));

    private static readonly Func<AppDbContext, decimal, IAsyncEnumerable<Product>>
        GetExpensiveProducts = EF.CompileAsyncQuery(
            (AppDbContext ctx, decimal minPrice) =>
                ctx.Products
                    .Where(p => p.Price > minPrice)
                    .OrderByDescending(p => p.Price));

    public async Task<Product?> GetByIdCompiledAsync(int id)
    {
        return await GetProductById(context, id);
    }

    public async Task<List<Product>> GetExpensiveCompiledAsync(decimal minPrice)
    {
        var result = new List<Product>();
        await foreach (var product in GetExpensiveProducts(context, minPrice))
        {
            result.Add(product);
        }
        return result;
    }
}
```

---

### 8.6 Performance Profiling vositalari

```csharp
// 1. MiniProfiler integratsiya
// dotnet add package MiniProfiler.EntityFrameworkCore

// Program.cs
builder.Services.AddMiniProfiler(options =>
{
    options.RouteBasePath = "/profiler";
}).AddEntityFramework(); // EF Core so'rovlarini kuzatish

// 2. Custom Diagnostic Listener
public class EfCoreDiagnosticListener : IObserver<DiagnosticListener>
{
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == DbLoggerCategory.Name)
        {
            listener.Subscribe(new EfCoreEventObserver());
        }
    }

    public void OnError(Exception error) { }
    public void OnCompleted() { }
}

public class EfCoreEventObserver : IObserver<KeyValuePair<string, object?>>
{
    public void OnNext(KeyValuePair<string, object?> pair)
    {
        if (pair.Key == RelationalEventId.CommandExecuted.Name
            && pair.Value is CommandExecutedEventData data)
        {
            if (data.Duration.TotalMilliseconds > 100) // Sekin so'rovlar
            {
                Console.WriteLine($"⚠️ SLOW QUERY ({data.Duration.TotalMilliseconds}ms):");
                Console.WriteLine(data.Command.CommandText);
            }
        }
    }

    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
```

---

## 2. O'rganish metodi

### Topshiriq 1: Bulk operatsiyalar benchmark
- 100,000 ta mahsulotni quyidagi usullar bilan insert qiling va vaqtini o'lchang:
  1. `AddRange` + `SaveChanges`
  2. `BulkInsertAsync` (EFCore.BulkExtensions)
  3. Raw SQL (`SqlBulkCopy`)
- Natijalarni jadvalda solishtiring

### Topshiriq 2: Multi-tenant tizim
- Row-level multi-tenancy yarating (`TenantId` filter)
- `IMultiTenant` interfeysi va global query filter
- `SaveChangesAsync` override — avtomatik `TenantId` qo'shish
- 3 ta tenant yaratib, ma'lumotlar izolyatsiyasini tekshiring

### Topshiriq 3: Parallel processing
- `IDbContextFactory` bilan 10,000 ta yozuvni parallel (parallelism=8) yangilang
- `SemaphoreSlim` bilan concurrency ni cheklang
- Sequential vs Parallel vaqtni solishtiring

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] Bulk Insert/Update/Delete usullarini bilaman
- [ ] `ExecuteUpdateAsync` va `BulkExtensions` farqini tushunaman
- [ ] Multi-tenancy ning 3 ta usulini bilaman (DB, Schema, Row-level)
- [ ] Row-level multi-tenancy ni global query filter bilan yarata olaman
- [ ] Sharding asoslarini tushunaman
- [ ] `DbContext` thread-safe emasligini bilaman va `IDbContextFactory` ishlataman
- [ ] `CompiledQuery` va application-level caching farqini tushunaman
- [ ] Performance profiling vositalarini ishlataman

---

## 3. Solishtirish jadvali: Multi-tenancy strategiyalari

| Mezon | Database-per-tenant | Schema-per-tenant | Row-level (TenantId) |
|---|---|---|---|
| **Ma'lumot izolyatsiyasi** | ⭐⭐⭐ Eng yuqori | ⭐⭐ Yaxshi | ⭐ Query filter orqali |
| **Infra murakkabligi** | ⚠️ Ko'p DB boshqarish | ⚠️ Schema boshqarish | ✅ Bitta DB |
| **Migration** | ⚠️ Har bir DB ga alohida | ⚠️ Schema bilan murakkab | ✅ Bitta migration |
| **Performance** | ✅ Har bir DB kichik | ✅ Schema-level partition | ⚠️ Index muhim |
| **Xarajat** | ⚠️ Ko'p DB = ko'p xarajat | ⚠️ O'rtacha | ✅ Minimal |
| **Scaling** | ✅ Har bir DB alohida scale | ⚠️ O'rtacha | ⚠️ Vertical scale |
| **Cross-tenant query** | ❌ Qiyin | ⚠️ Mumkin | ✅ Oson (`IgnoreQueryFilters`) |
| **Qachon ishlatiladi** | Enterprise, compliance | O'rtacha loyihalar | SaaS, ko'p tenant |

---

## 4. Test

### Savollar

**1.** 100,000 ta yozuvni eng tez insert qilish usuli qaysi?
- a) `AddRange` + `SaveChangesAsync`
- b) Loop da `Add` + `SaveChanges`
- c) `BulkInsertAsync` (SqlBulkCopy)
- d) Raw SQL INSERT loop

**2.** Row-level multi-tenancy da ma'lumot izolyatsiyasini ta'minlash uchun nima ishlatiladi?

**3.** Quyidagi kod nima uchun xavfli?
```csharp
await Task.WhenAll(
    context.Products.ToListAsync(),
    context.Categories.ToListAsync());
```
- a) SQL Injection xavfi
- b) DbContext thread-safe emas — race condition
- c) Xotira yetishmasligi
- d) Hech qanday xavf yo'q

**4.** `CompiledQuery` nima samaradorlik beradi?
- a) SQL so'rovni cache qiladi
- b) LINQ → Expression Tree → SQL tarjimani cache qiladi
- c) Ma'lumotlarni cache qiladi
- d) Connection ni cache qiladi

**5.** Sharding da eng katta muammo nima?
- a) Tezlik
- b) Cross-shard query va JOIN
- c) Xotira
- d) SQL yozish

**6.** `SemaphoreSlim` parallel operatsiyalarda nima uchun ishlatiladi?

**7.** Multi-tenancy loyihada yangi tenant qo'shilganda, qaysi strategiya eng kam ish talab qiladi?
- a) Database-per-tenant
- b) Schema-per-tenant
- c) Row-level (TenantId)
- d) Hammasi bir xil

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Natijalar **taxminiy/indikativ**, BenchmarkDotNet, .NET 8, SQL Server 2022.

### INSERT — 100,000 ta yozuv

| Operatsiya | O'rtacha vaqt (s) | Xotira sarfi (MB) | Izoh |
|---|---|---|---|
| `AddRange` + `SaveChanges` | ~45 | ~250 | Change Tracker og'irligi |
| `AddRange` + `SaveChanges` (batch 1000) | ~20 | ~50 | ChangeTracker.Clear() har batch da |
| `BulkInsertAsync` (EFCore.BulkExtensions) | ~2 | ~80 | ⚡ SqlBulkCopy — 20x tez |
| Raw SQL `SqlBulkCopy` | ~1.5 | ~60 | ⚡⚡ Eng tez |

### CompiledQuery vs oddiy LINQ — 10,000 ta so'rov (takroriy)

| Operatsiya | O'rtacha vaqt (ms) | Izoh |
|---|---|---|
| Oddiy LINQ (har safar tarjima) | ~3500 | Har bir so'rov uchun expression tree parse |
| CompiledQuery (cache) | ~2000 | ⚡ ~40% tez — tarjima cache dan olinadi |

### Parallel vs Sequential — 10,000 ta UPDATE

| Operatsiya | O'rtacha vaqt (s) | Izoh |
|---|---|---|
| Sequential (bitta DbContext) | ~30 | Bitta-bitta bajarish |
| Parallel (4 task, Factory) | ~9 | ⚡ ~3.3x tez |
| Parallel (8 task, Factory) | ~6 | ⚡ ~5x tez |
| `ExecuteUpdateAsync` (bitta SQL) | ~0.05 | ⚡⚡ 600x tez — agar imkoni bo'lsa |
