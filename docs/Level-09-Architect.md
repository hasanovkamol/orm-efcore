# Level 9 — Architect (Enterprise darajasi)

---

## 1. Darslik

### 9.1 Complex Data Modeling

Enterprise loyihalarda ma'lumot modeli oddiy CRUD dan ancha murakkab bo'ladi: polimorfizm (TPH, TPT, TPC), temporal tables, va hierarchical data.

```csharp
// 1. Table-Per-Hierarchy (TPH) — bitta jadval, discriminator ustuni
public abstract class Payment
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
}

public class CreditCardPayment : Payment
{
    public string CardNumber { get; set; } = string.Empty; // oxirgi 4 raqam
    public string CardHolderName { get; set; } = string.Empty;
}

public class BankTransferPayment : Payment
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

public class CashPayment : Payment
{
    public string ReceivedBy { get; set; } = string.Empty;
}

// Konfiguratsiya
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // TPH — bitta "Payments" jadvali, "PaymentType" discriminator
        builder.HasDiscriminator<string>("PaymentType")
            .HasValue<CreditCardPayment>("CreditCard")
            .HasValue<BankTransferPayment>("BankTransfer")
            .HasValue<CashPayment>("Cash");

        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
    }
}
```

```csharp
// 2. Table-Per-Type (TPT) — har bir tip uchun alohida jadval
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Payment>().ToTable("Payments");
    modelBuilder.Entity<CreditCardPayment>().ToTable("CreditCardPayments");
    modelBuilder.Entity<BankTransferPayment>().ToTable("BankTransferPayments");
    modelBuilder.Entity<CashPayment>().ToTable("CashPayments");
}

// 3. Table-Per-Concrete-Type (TPC) — EF Core 7+
// Har bir concrete class alohida jadvalda, base class jadvali yo'q
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Payment>().UseTpcMappingStrategy();
    modelBuilder.Entity<CreditCardPayment>().ToTable("CreditCardPayments");
    modelBuilder.Entity<BankTransferPayment>().ToTable("BankTransferPayments");
}
```

**Polimorf so'rovlar:**

```csharp
public class PaymentService(AppDbContext context)
{
    // Barcha to'lovlar (turidan qat'i nazar)
    public async Task<List<Payment>> GetAllPaymentsAsync() =>
        await context.Payments.AsNoTracking().ToListAsync();

    // Faqat kredit karta to'lovlari
    public async Task<List<CreditCardPayment>> GetCardPaymentsAsync() =>
        await context.Payments
            .OfType<CreditCardPayment>()
            .AsNoTracking()
            .ToListAsync();

    // Turi bo'yicha statistika
    public async Task<Dictionary<string, decimal>> GetPaymentSummaryAsync() =>
        await context.Payments
            .GroupBy(p => EF.Property<string>(p, "PaymentType"))
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Sum(p => p.Amount));
}
```

**Temporal Tables (SQL Server):**

```csharp
// EF Core 6+ — Temporal table qo'llab-quvvatlash
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", b => b.IsTemporal(t =>
        {
            t.HasPeriodStart("ValidFrom");
            t.HasPeriodEnd("ValidTo");
            t.UseHistoryTable("ProductsHistory");
        }));
    }
}

// Temporal so'rovlar — tarixiy ma'lumotlarni ko'rish
public class TemporalQueryService(AppDbContext context)
{
    // Ma'lum vaqtdagi holatni ko'rish
    public async Task<List<Product>> GetProductsAtTimeAsync(DateTime pointInTime)
    {
        return await context.Products
            .TemporalAsOf(pointInTime)
            .AsNoTracking()
            .ToListAsync();
    }

    // O'zgarishlar tarixini ko'rish
    public async Task<List<Product>> GetProductHistoryAsync(int productId)
    {
        return await context.Products
            .TemporalAll()
            .Where(p => p.Id == productId)
            .OrderBy(p => EF.Property<DateTime>(p, "ValidFrom"))
            .AsNoTracking()
            .ToListAsync();
    }

    // Ma'lum oraliqda o'zgarishlar
    public async Task<List<Product>> GetProductChangesAsync(
        DateTime from, DateTime to)
    {
        return await context.Products
            .TemporalBetween(from, to)
            .AsNoTracking()
            .ToListAsync();
    }
}
```

---

### 9.2 Data Warehousing va EF Core

Data Warehouse scenariyolarida EF Core cheklangan, lekin ba'zi hollarda foydali.

```csharp
// Star Schema — Fact va Dimension jadvallar
public class SalesFact
{
    public long Id { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal Amount { get; set; }
    public int Quantity { get; set; }

    // Dimension FK lar
    public int ProductDimensionId { get; set; }
    public ProductDimension Product { get; set; } = null!;

    public int CustomerDimensionId { get; set; }
    public CustomerDimension Customer { get; set; } = null!;

    public int TimeDimensionId { get; set; }
    public TimeDimension Time { get; set; } = null!;
}

public class ProductDimension
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
}

// Warehouse so'rovlari — read-only, NoTracking
public class WarehouseQueryService(AppDbContext context)
{
    public async Task<List<MonthlySalesReport>> GetMonthlySalesAsync(int year)
    {
        return await context.SalesFacts
            .AsNoTracking()
            .Where(s => s.SaleDate.Year == year)
            .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
            .Select(g => new MonthlySalesReport
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalSales = g.Sum(s => s.Amount),
                TotalQuantity = g.Sum(s => s.Quantity),
                OrderCount = g.Count()
            })
            .OrderBy(r => r.Month)
            .ToListAsync();
    }

    // Murakkab analytics uchun Raw SQL tavsiya etiladi
    public async Task<List<CategoryTrend>> GetCategoryTrendsAsync()
    {
        return await context.Database
            .SqlQueryRaw<CategoryTrend>(
                """
                SELECT
                    pd.Category,
                    td.MonthName,
                    SUM(sf.Amount) AS TotalSales,
                    LAG(SUM(sf.Amount)) OVER (
                        PARTITION BY pd.Category ORDER BY td.MonthId
                    ) AS PreviousMonthSales
                FROM SalesFacts sf
                JOIN ProductDimensions pd ON sf.ProductDimensionId = pd.Id
                JOIN TimeDimensions td ON sf.TimeDimensionId = td.Id
                GROUP BY pd.Category, td.MonthName, td.MonthId
                ORDER BY pd.Category, td.MonthId
                """)
            .ToListAsync();
    }
}
```

---

### 9.3 Distributed Transactions va alternativalar

Distributed transaction — bir nechta ma'lumotlar bazasi yoki tizim orasida atomik amal. Microservicelar dunyosida buning alternativlari ko'proq ishlatiladi.

```csharp
// 1. Saga Pattern — compensating transactions
public class OrderSaga(
    IOrderService orderService,
    IPaymentService paymentService,
    IInventoryService inventoryService,
    ILogger<OrderSaga> logger)
{
    public async Task<bool> ExecuteAsync(CreateOrderRequest request)
    {
        int? orderId = null;
        string? paymentId = null;

        try
        {
            // Step 1: Buyurtma yaratish
            orderId = await orderService.CreateAsync(request);
            logger.LogInformation("Order {OrderId} created", orderId);

            // Step 2: To'lov qilish
            paymentId = await paymentService.ChargeAsync(request.CustomerId, request.TotalAmount);
            logger.LogInformation("Payment {PaymentId} charged", paymentId);

            // Step 3: Zaxiradan kamaytirish
            await inventoryService.ReserveAsync(request.Items);
            logger.LogInformation("Inventory reserved");

            // Step 4: Buyurtmani tasdiqlash
            await orderService.ConfirmAsync(orderId.Value);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saga failed, compensating...");

            // Compensating transactions (teskari tartibda)
            if (paymentId is not null)
                await paymentService.RefundAsync(paymentId);

            if (orderId is not null)
                await orderService.CancelAsync(orderId.Value);

            return false;
        }
    }
}

// 2. Outbox Pattern — event consistency
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty; // JSON
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class OutboxDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}

// Event ni saqlash — bitta tranzaksiyada entity va event birga
public class OrderServiceWithOutbox(AppDbContext context)
{
    public async Task CreateOrderAsync(Order order)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        context.Orders.Add(order);

        // Outbox ga event qo'shish — bitta tranzaksiyada
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "OrderCreated",
            Payload = JsonSerializer.Serialize(new { order.Id, order.TotalAmount }),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}

// Background worker — outbox dan eventlarni yuborish
public class OutboxProcessor(
    IDbContextFactory<AppDbContext> contextFactory,
    IMessageBus messageBus) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var context = await contextFactory.CreateDbContextAsync(stoppingToken);

            var messages = await context.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreatedAt)
                .Take(50)
                .ToListAsync(stoppingToken);

            foreach (var message in messages)
            {
                await messageBus.PublishAsync(message.EventType, message.Payload);
                message.ProcessedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

---

### 9.4 Audit Log yuritish

Enterprise tizimlar uchun barcha o'zgarishlarning batafsil tarixi zarur.

```csharp
public class AuditLog
{
    public long Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Insert, Update, Delete
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? AffectedColumns { get; set; } // JSON
    public string UserId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}

public class AuditableDbContext(
    DbContextOptions options,
    IHttpContextAccessor httpContextAccessor) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = OnBeforeSaveChanges();
        var result = await base.SaveChangesAsync(cancellationToken);
        await OnAfterSaveChangesAsync(auditEntries);
        return result;
    }

    private List<AuditEntry> OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var entries = new List<AuditEntry>();
        var userId = httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
        var ipAddress = httpContextAccessor.HttpContext?.Connection
            .RemoteIpAddress?.ToString();

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog // Audit log ning o'zini auditlamaslik
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var auditEntry = new AuditEntry
            {
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                UserId = userId,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            foreach (var property in entry.Properties)
            {
                var propertyName = property.Metadata.Name;

                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified when property.IsModified:
                        auditEntry.AffectedColumns.Add(propertyName);
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;
                }
            }

            entries.Add(auditEntry);
        }

        return entries;
    }

    private async Task OnAfterSaveChangesAsync(List<AuditEntry> auditEntries)
    {
        if (auditEntries.Count == 0) return;

        foreach (var entry in auditEntries)
        {
            AuditLogs.Add(new AuditLog
            {
                EntityName = entry.EntityName,
                EntityId = JsonSerializer.Serialize(entry.KeyValues),
                Action = entry.Action,
                OldValues = entry.OldValues.Count > 0 ? JsonSerializer.Serialize(entry.OldValues) : null,
                NewValues = entry.NewValues.Count > 0 ? JsonSerializer.Serialize(entry.NewValues) : null,
                AffectedColumns = entry.AffectedColumns.Count > 0 ? JsonSerializer.Serialize(entry.AffectedColumns) : null,
                UserId = entry.UserId,
                IpAddress = entry.IpAddress,
                Timestamp = entry.Timestamp
            });
        }

        await base.SaveChangesAsync();
    }
}

// Helper class
public class AuditEntry
{
    public string EntityName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object?> KeyValues { get; } = [];
    public Dictionary<string, object?> OldValues { get; } = [];
    public Dictionary<string, object?> NewValues { get; } = [];
    public List<string> AffectedColumns { get; } = [];
}
```

---

### 9.5 Soft Delete

```csharp
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}

// Extension method — soft delete uchun
public static class SoftDeleteExtensions
{
    public static void AddSoftDeleteFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var condition = Expression.Equal(property, Expression.Constant(false));
            var lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}

// SaveChanges da intercept qilish — Remove ni Soft Delete ga o'girish
public class SoftDeleteInterceptor(
    IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var userId = httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

        foreach (var entry in eventData.Context.ChangeTracker.Entries<ISoftDelete>()
            .Where(e => e.State == EntityState.Deleted))
        {
            // Delete ni Modified ga o'zgartirish
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
            entry.Entity.DeletedBy = userId;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

---

### 9.6 Open-Source Extensionlar

```csharp
// 1. EFCore.BulkExtensions — bulk operatsiyalar (Level 8 da ko'rdik)
// dotnet add package EFCore.BulkExtensions

// 2. Z.EntityFramework.Plus (EF Plus) — advanced features
// dotnet add package Z.EntityFramework.Plus.EFCore

public class EfPlusService(AppDbContext context)
{
    // Batch Delete — bitta SQL da
    public async Task BatchDeleteAsync(int categoryId)
    {
        await context.Products
            .Where(p => p.CategoryId == categoryId)
            .DeleteFromQueryAsync(); // Z.EF.Plus
    }

    // Batch Update
    public async Task BatchUpdateAsync(decimal percentage)
    {
        await context.Products
            .Where(p => p.Price < 100)
            .UpdateFromQueryAsync(p => new Product
            {
                Price = p.Price * (1 + percentage / 100)
            });
    }

    // Query Cache
    public async Task<List<Category>> GetCachedCategoriesAsync()
    {
        return await context.Categories
            .AsNoTracking()
            .FromCacheAsync(); // Avtomatik cache
    }

    // Query Future — bir nechta so'rovni bitta round-trip da
    public async Task<(List<Product> products, int count)> GetWithCountAsync()
    {
        var productsTask = context.Products
            .AsNoTracking()
            .Future(); // Hali bajarmaydi

        var countTask = context.Products
            .DeferredCount()
            .FutureValue(); // Hali bajarmaydi

        // Ikkalasi bitta round-trip da bajariladi
        var products = await productsTask.ToListAsync();
        var count = await countTask.ValueAsync();

        return (products, count);
    }
}

// 3. Audit.NET — professional audit tizimi
// dotnet add package Audit.EntityFramework.Core

// 4. EFCore.NamingConventions — naming conventions
// dotnet add package EFCore.NamingConventions
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
        .UseSnakeCaseNamingConvention()); // product_name, category_id
```

---

### 9.7 Real loyihadagi muammo va yechim — Case Study

**Case Study: E-Commerce platformasi — 50 mln yozuv, 1000+ req/s**

```csharp
// Muammo 1: Sekin dashboard so'rovlari (5+ sekund)
// Sabab: 5 ta jadvalga JOIN + GROUP BY + ORDER BY
// Yechim: Materialized View + Cache

// Migration da Materialized View yaratish
public partial class AddDashboardView : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE VIEW vw_DashboardSummary WITH SCHEMABINDING AS
            SELECT
                p.CategoryId,
                COUNT_BIG(*) AS OrderCount,
                SUM(oi.Quantity) AS TotalQuantity,
                SUM(oi.UnitPrice * oi.Quantity) AS TotalRevenue
            FROM dbo.OrderItems oi
            INNER JOIN dbo.Products p ON oi.ProductId = p.Id
            GROUP BY p.CategoryId;

            CREATE UNIQUE CLUSTERED INDEX IX_DashboardSummary
            ON vw_DashboardSummary(CategoryId);
            """);
    }
}

// Muammo 2: Memory leak — DbContext Scoped, lekin background task da ishlatilmoqda
// Yechim: IDbContextFactory
public class BackgroundOrderProcessor(
    IDbContextFactory<AppDbContext> contextFactory,
    ILogger<BackgroundOrderProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var context = await contextFactory.CreateDbContextAsync(stoppingToken);

            var pendingOrders = await context.Orders
                .Where(o => o.Status == OrderStatus.Pending)
                .Take(100)
                .ToListAsync(stoppingToken);

            foreach (var order in pendingOrders)
            {
                order.Status = OrderStatus.Processing;
            }

            await context.SaveChangesAsync(stoppingToken);

            logger.LogInformation("Processed {Count} orders", pendingOrders.Count);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

// Muammo 3: Deadlock — concurrent update
// Yechim: Optimistic Concurrency
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }

    [Timestamp] // Concurrency token
    public byte[] RowVersion { get; set; } = [];
}

public class ConcurrencyService(AppDbContext context)
{
    public async Task<bool> UpdateStockAsync(int productId, int quantityChange)
    {
        const int maxRetries = 3;

        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                var product = await context.Products.FindAsync(productId);
                if (product is null) return false;

                product.Stock += quantityChange;
                await context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Boshqa process o'zgartirgan — qayta o'qib urinish
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync(); // DB dan qayta o'qish
                }
            }
        }

        return false;
    }
}
```

---

## 2. O'rganish metodi

### Topshiriq 1: Enterprise data model
- Polimorf `Payment` modelini (TPH) yarating: CreditCard, BankTransfer, Cash
- Temporal Table yoqing va tarixiy so'rovlar yozing
- Concurrency token (`[Timestamp]`) bilan optimistic concurrency yarating

### Topshiriq 2: Audit va Soft Delete tizimi
- `AuditableDbContext` yarating — barcha o'zgarishlar `AuditLogs` ga yozilsin
- `SoftDeleteInterceptor` — `Remove` → `IsDeleted = true`
- Admin panel uchun audit loglarni ko'rish API si

### Topshiriq 3: Case Study loyiha
- Outbox Pattern ni implement qiling
- Saga Pattern bilan Order → Payment → Inventory jarayonini yarating
- Materialized View yaratib, dashboard so'rovlarini 10x tezlashtiring

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] TPH, TPT, TPC farqlarini bilaman va to'g'ri tanlash mumkin
- [ ] Temporal Tables bilan tarixiy so'rovlar yoza olaman
- [ ] Star Schema (Fact/Dimension) modelini tushunaman
- [ ] Saga Pattern va Outbox Pattern ni implement qila olaman
- [ ] Distributed transaction muammolari va alternativalarini bilaman
- [ ] To'liq audit log tizimini yarata olaman
- [ ] Soft Delete ni interceptor bilan yarata olaman
- [ ] Optimistic Concurrency (`[Timestamp]`) ishlataman
- [ ] EF Plus, BulkExtensions kabi extensionlarni bilaman
- [ ] Real loyiha muammolarini (deadlock, memory leak, slow query) hal qila olaman

---

## 3. Solishtirish jadvali: TPH vs TPT vs TPC

| Mezon | TPH (Table-Per-Hierarchy) | TPT (Table-Per-Type) | TPC (Table-Per-Concrete) |
|---|---|---|---|
| **Jadvallar soni** | 1 (discriminator bilan) | N (har bir tip uchun) | N (faqat concrete) |
| **NULL ustunlar** | ⚠️ Ko'p (boshqa tip ustunlari) | ✅ Yo'q | ✅ Yo'q |
| **SELECT performance** | ⚡ Eng tez (bitta jadval) | ⚠️ JOIN kerak | ✅ Yaxshi (bitta jadval) |
| **INSERT performance** | ⚡ Tez | ⚠️ Sekin (2+ jadvalga) | ✅ Tez |
| **Polimorf so'rov** | ✅ Oson | ⚠️ UNION/JOIN | ⚠️ UNION ALL |
| **Data integrity** | ⚠️ NULL bo'lishi mumkin | ✅ Yaxshi | ✅ Yaxshi |
| **Disk hajmi** | ⚠️ NULL ustunlar | ✅ Samarali | ✅ Samarali |
| **EF Core versiya** | Barcha | Barcha | 7+ |
| **Tavsiya** | ✅ Default tanlov | Katta tip farqlari | Ko'p concrete, kam polimorf |

---

## 4. Test

### Savollar

**1.** TPH (Table-Per-Hierarchy) da discriminator nima?
- a) Primary Key
- b) Entity turi ni aniqlaydigan ustun
- c) Foreign Key
- d) Index

**2.** Temporal Table ning asosiy foyda nimada?

**3.** Saga Pattern nima uchun ishlatiladi?
- a) SQL so'rovlarni tezlashtirish
- b) Distributed tizimda atomik operatsiyalarni compensating transaction orqali ta'minlash
- c) Ma'lumotlar bazasini yaratish
- d) Caching

**4.** Quyidagi kodda `DbUpdateConcurrencyException` qachon yuz beradi?
```csharp
var product = await context.Products.FindAsync(1);
product.Stock -= 5;
await context.SaveChangesAsync();
```
- a) Hech qachon
- b) `Product` da `[Timestamp]` bo'lsa va boshqa process ham o'zgartirgan bo'lsa
- c) Stock manfiy bo'lganda
- d) Internet uzilganda

**5.** Outbox Pattern ning asosiy maqsadi nima?

**6.** Soft Delete da `Remove()` o'rniga nima bo'lishi kerak?
- a) `Delete` SQL
- b) `IsDeleted = true` qilib, `EntityState.Modified` ga o'tkazish
- c) Jadvaldan jismonan o'chirish
- d) Alohida jadvalga ko'chirish

**7.** Real loyihada 50 mln yozuvli jadvalda sekin so'rovni tezlashtirish uchun 3 ta strategiyani sanang.

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Natijalar **taxminiy/indikativ**, BenchmarkDotNet, .NET 8, SQL Server 2022.

### TPH vs TPT vs TPC — 100,000 yozuv (3 tip, har biri ~33K)

| Operatsiya | TPH (ms) | TPT (ms) | TPC (ms) | Izoh |
|---|---|---|---|---|
| SELECT barcha (polimorf) | ~25 | ~80 | ~45 | TPH bitta jadval ⚡ |
| SELECT faqat bitta tip | ~10 | ~15 | ~8 | TPC toza jadval ⚡ |
| INSERT 1000 ta | ~30 | ~60 | ~30 | TPT 2 jadvalga yozadi |
| UPDATE 1000 ta | ~35 | ~70 | ~35 | TPT JOIN kerak |

### Temporal Table overhead

| Operatsiya | Oddiy jadval (ms) | Temporal jadval (ms) | Izoh |
|---|---|---|---|
| INSERT 10,000 | ~200 | ~220 | +10% — history jadvalga yozish |
| UPDATE 10,000 | ~250 | ~300 | +20% — old va new saqlash |
| SELECT 10,000 | ~15 | ~15 | Farq yo'q — asosiy jadvaldan o'qiydi |
| TemporalAsOf query | — | ~20 | Tarixiy so'rov |

### Audit Log overhead

| Operatsiya | Auditsiz (ms) | Audit bilan (ms) | Izoh |
|---|---|---|---|
| SaveChanges (1 entity) | ~3 | ~6 | +3ms — audit entry yaratish |
| SaveChanges (50 entity) | ~15 | ~40 | +25ms — 50 ta property tekshirish |
| SaveChanges (500 entity) | ~80 | ~250 | ⚠️ Ko'p entity — sezilarli overhead |

### Concurrency — parallel 10 update (bitta yozuvga)

| Strategiya | Muvaffaqiyatli update | Xato/Retry | Izoh |
|---|---|---|---|
| Lock yo'q | 10 (lekin lost update!) | 0 | ❌ Ma'lumot yo'qolishi |
| Optimistic (RowVersion) | 1 (birinchisi) + 9 retry | 9 exception | ✅ To'g'ri natija |
| Pessimistic (UPDLOCK) | 10 (ketma-ket) | 0 | ✅ Lekin sekin |
