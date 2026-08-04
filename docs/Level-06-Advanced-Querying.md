# Level 6 — Middle-Senior (Advanced Querying)

---

## 1. Darslik

### 6.1 Query Splitting

EF Core da `Include` ishlatganda ko'p jadvallarni JOIN qilish "Cartesian Explosion" muammosiga olib kelishi mumkin — ya'ni natija to'plami geometrik ravishda o'sadi.

```csharp
// ❌ MUAMMO — Cartesian Explosion
// Agar Category da 10 Product, har bir Product da 5 Review bo'lsa:
// 10 x 5 = 50 qator qaytadi (dublikatlar bilan)
var categories = await context.Categories
    .Include(c => c.Products)
        .ThenInclude(p => p.Reviews)
    .ToListAsync();
// SQL: SELECT ... FROM Categories c
//      LEFT JOIN Products p ON c.Id = p.CategoryId
//      LEFT JOIN Reviews r ON p.Id = r.ProductId
// Natija: 50+ qator, ko'p dublikat

// ✅ YECHIM — AsSplitQuery
var categories = await context.Categories
    .Include(c => c.Products)
        .ThenInclude(p => p.Reviews)
    .AsSplitQuery() // Alohida so'rovlarga bo'ladi
    .ToListAsync();
// SQL 1: SELECT ... FROM Categories
// SQL 2: SELECT ... FROM Products WHERE CategoryId IN (...)
// SQL 3: SELECT ... FROM Reviews WHERE ProductId IN (...)
// Har bir so'rov kichik va samarali
```

**Global darajada split query qilish:**

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// Alohida so'rovda single query ga qaytish
var data = await context.Categories
    .Include(c => c.Products)
    .AsSingleQuery() // Global split bo'lsa ham, bu yerda single
    .ToListAsync();
```

**Qachon Split Query ishlatish kerak:**

```csharp
public class QuerySplitService(AppDbContext context)
{
    // ✅ Split — ko'p Include bo'lganda
    public async Task<List<Order>> GetOrdersFullAsync()
    {
        return await context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Category)
            .Include(o => o.ShippingInfo)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }

    // ❌ Split kerak EMAS — bitta Include bo'lganda
    public async Task<List<Product>> GetProductsWithCategoryAsync()
    {
        return await context.Products
            .Include(p => p.Category) // Bitta JOIN — muammo yo'q
            .AsNoTracking()
            .ToListAsync();
    }
}
```

---

### 6.2 Index yaratish va EF Core orqali berish

Ma'lumotlar bazasi performance ning eng muhim qismi — to'g'ri indexlar.

```csharp
// Fluent API orqali index yaratish
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // Oddiy index
        builder.HasIndex(p => p.Name);

        // Unique index
        builder.HasIndex(p => p.SKU)
            .IsUnique();

        // Composite (murakkab) index — bir nechta ustun
        builder.HasIndex(p => new { p.CategoryId, p.Price })
            .HasDatabaseName("IX_Products_Category_Price");

        // Filtered index — faqat ma'lum shartdagi yozuvlar uchun
        builder.HasIndex(p => p.Name)
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_Products_Name_Active");

        // Descending index (EF Core 7+)
        builder.HasIndex(p => p.Price)
            .IsDescending();

        // Include columns (covering index)
        builder.HasIndex(p => p.CategoryId)
            .IncludeProperties(p => new { p.Name, p.Price })
            .HasDatabaseName("IX_Products_Category_Include");
        // SQL: CREATE INDEX ... ON Products(CategoryId) INCLUDE (Name, Price)
    }
}
```

**Data Annotation bilan index:**

```csharp
[Index(nameof(Name))]
[Index(nameof(SKU), IsUnique = true)]
[Index(nameof(CategoryId), nameof(Price), Name = "IX_Products_Category_Price")]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public bool IsActive { get; set; }
}
```

**Index strategiyalari:**

```csharp
// Migration orqali qo'lda index qo'shish
public partial class AddPerformanceIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Full-text index (EF Core bu ni qo'llab-quvvatlamaydi)
        migrationBuilder.Sql(
            """
            CREATE FULLTEXT CATALOG ftCatalog AS DEFAULT;
            CREATE FULLTEXT INDEX ON Products(Name, Description)
                KEY INDEX PK_Products ON ftCatalog
                WITH CHANGE_TRACKING AUTO;
            """);

        // Columnstore index (analytics uchun)
        migrationBuilder.Sql(
            """
            CREATE NONCLUSTERED COLUMNSTORE INDEX IX_Orders_Columnstore
            ON Orders (OrderDate, TotalPrice, Quantity);
            """);
    }
}
```

---

### 6.3 Cascade Delete boshqaruvi

Cascade delete — parent entity o'chirilganda child entitylar ham avtomatik o'chirilishi.

```csharp
public class RelationshipConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // Cascade — parent o'chsa, child ham o'chadi (default One-to-Many)
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict — child bor bo'lsa, parent o'chirishga ruxsat bermaydi
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull — parent o'chsa, FK ni null qiladi (FK nullable bo'lishi shart)
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .IsRequired(false) // nullable FK
            .OnDelete(DeleteBehavior.SetNull);

        // NoAction — DB darajasida hech narsa qilmaydi (dasturchi javobgar)
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
```

**Real loyiha misoli — Cascade delete muammosi va yechimi:**

```csharp
public class SafeDeleteService(AppDbContext context)
{
    // ❌ Cascade — bexosdan barcha mahsulotlar o'chishi mumkin
    public async Task DeleteCategoryUnsafeAsync(int categoryId)
    {
        var category = await context.Categories.FindAsync(categoryId);
        if (category is null) return;

        context.Categories.Remove(category);
        await context.SaveChangesAsync();
        // Agar Cascade bo'lsa — barcha productlar ham o'chadi!
    }

    // ✅ Xavfsiz o'chirish — avval tekshirish
    public async Task<bool> DeleteCategorySafeAsync(int categoryId)
    {
        var hasProducts = await context.Products
            .AnyAsync(p => p.CategoryId == categoryId);

        if (hasProducts)
            return false; // O'chirishga ruxsat yo'q

        var category = await context.Categories.FindAsync(categoryId);
        if (category is null) return false;

        context.Categories.Remove(category);
        await context.SaveChangesAsync();
        return true;
    }

    // ✅ Cascade o'rniga — avval childlarni alohida boshqarish
    public async Task DeleteCategoryWithProductsAsync(int categoryId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        // 1. Avval mahsulotlarni boshqa kategoriyaga o'tkazish yoki o'chirish
        await context.Products
            .Where(p => p.CategoryId == categoryId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(p => p.CategoryId, (int?)null));

        // 2. Keyin kategoriyani o'chirish
        await context.Categories
            .Where(c => c.Id == categoryId)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }
}
```

---

### 6.4 EF Core Performance Optimizatsiya — real loyiha tajribasi

**1. So'rovlarni tahlil qilish (Query Logging):**

```csharp
// Program.cs — SQL loglarni ko'rish
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
        .LogTo(Console.WriteLine, LogLevel.Information) // SQL ni console ga chiqarish
        .EnableSensitiveDataLogging()  // Parametr qiymatlarini ko'rsatish (dev uchun)
        .EnableDetailedErrors());       // Batafsil xato xabarlari

// Yoki ILoggerFactory bilan
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
        .UseLoggerFactory(LoggerFactory.Create(b =>
            b.AddFilter((category, level) =>
                category == DbLoggerCategory.Database.Command.Name
                && level == LogLevel.Information)
            .AddConsole())));
```

**2. Global query filter — loyiha miqyosida:**

```csharp
public class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Barcha ISoftDelete entity larga avtomatik filter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                var condition = Expression.Equal(property, Expression.Constant(false));
                var lambda = Expression.Lambda(condition, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
```

**3. Batch operatsiyalar — ko'p ma'lumot bilan ishlash:**

```csharp
public class BatchService(AppDbContext context)
{
    // ✅ Katta dataset bilan chunk-lardan ishlash
    public async Task ProcessLargeDatasetAsync()
    {
        const int batchSize = 500;
        var totalCount = await context.Products.CountAsync();

        for (int skip = 0; skip < totalCount; skip += batchSize)
        {
            var batch = await context.Products
                .OrderBy(p => p.Id)
                .Skip(skip)
                .Take(batchSize)
                .ToListAsync();

            foreach (var product in batch)
            {
                product.LastProcessed = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear(); // Xotirani tozalash
        }
    }
}
```

---

## 2. O'rganish metodi

### Topshiriq 1: Query Splitting amaliyoti
- 3+ darajali Include bilan so'rov yozing (Order → Customer → Address + OrderItems → Product → Category)
- `AsSingleQuery` va `AsSplitQuery` natijalarini SQL log orqali solishtiring
- Cartesian Explosion ni ko'ring va Split Query bilan bartaraf qiling

### Topshiriq 2: Index strategiyasi
- Loyihaga 5+ index qo'shing (oddiy, unique, composite, filtered, include)
- Migration yarating va SQL skriptini ko'ring
- `EXPLAIN` yoki `SET STATISTICS IO ON` bilan index ta'sirini o'lchang

### Topshiriq 3: Soft Delete tizimi
- `ISoftDelete` interfeysi va global query filter yarating
- `SoftDeleteService` yarating — `Remove` o'rniga `IsDeleted = true` qilsin
- Admin uchun `IgnoreQueryFilters()` bilan o'chirilganlarni ko'rish imkoniyati

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] Query Splitting va Cartesian Explosion muammosini tushunaman
- [ ] `AsSplitQuery` / `AsSingleQuery` farqini bilaman
- [ ] EF Core orqali turli xil indexlar yarata olaman (unique, composite, filtered, include)
- [ ] Cascade delete holatlarini (`Cascade`, `Restrict`, `SetNull`, `NoAction`) tushunaman
- [ ] SQL logging ni yoqib, generatsiya bo'lgan so'rovlarni tahlil qila olaman
- [ ] Global query filter yarata olaman
- [ ] Katta dataset bilan batch ishlashni bilaman

---

## 3. Solishtirish jadvali: AsSingleQuery vs AsSplitQuery

| Mezon | Single Query | Split Query |
|---|---|---|
| **SQL so'rovlar soni** | 1 ta (katta JOIN) | N ta (alohida so'rovlar) |
| **Cartesian Explosion** | ⚠️ Mumkin | ✅ Yo'q |
| **Network round-trip** | 1 ta | N ta |
| **Data consistency** | ✅ Bir lahzadagi holat | ⚠️ So'rovlar orasida data o'zgarishi mumkin |
| **Xotira sarfi** | ⚠️ Dublikat qatorlar | ✅ Kamroq |
| **Qachon ishlatiladi** | 1-2 Include | 3+ Include, ko'p collection |
| **Performance** | 1-2 jadval uchun ✅ yaxshi | 3+ jadval uchun ✅ yaxshi |
| **Default** | ✅ EF Core default | `UseQuerySplittingBehavior` bilan |

---

## 4. Test

### Savollar

**1.** Cartesian Explosion nima?
- a) Ma'lumotlar bazasi portlashi
- b) JOIN natijasida dublikat qatorlar geometrik o'sishi
- c) EF Core xatosi
- d) SQL Injection turi

**2.** Quyidagi index qaysi so'rovga foydali?
```csharp
builder.HasIndex(p => new { p.CategoryId, p.Price }).IsDescending(false, true);
```

**3.** `DeleteBehavior.SetNull` qachon ishlatish mumkin?
- a) Har doim
- b) Faqat Foreign Key nullable bo'lganda
- c) Faqat Primary Key da
- d) Faqat Many-to-Many da

**4.** Global query filter ni vaqtincha o'chirish uchun nima ishlatiladi?

**5.** Quyidagi kod nima muammo yaratishi mumkin?
```csharp
var orders = await context.Orders
    .Include(o => o.Customer)
    .Include(o => o.OrderItems)
        .ThenInclude(oi => oi.Product)
            .ThenInclude(p => p.Reviews)
    .Include(o => o.ShippingInfo)
    .ToListAsync();
```

**6.** Filtered index qanday holatda foydali?

**7.** Loyihadagi barcha `ISoftDelete` entity larga avtomatik query filter qo'yish uchun qanday yondashuv ishlatiladi?
- a) Har bir entity uchun alohida `HasQueryFilter` yozish
- b) `OnModelCreating` da reflection bilan avtomatik qo'llash
- c) Data Annotation bilan
- d) Migration da

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Natijalar **taxminiy/indikativ**, BenchmarkDotNet, .NET 8, SQL Server 2022.

### Single Query vs Split Query — 3 darajali Include (100 Order, har birida 5 Item, har Item da 3 Review)

| Operatsiya | O'rtacha vaqt (ms) | Qaytgan qatorlar | Xotira (MB) | Izoh |
|---|---|---|---|---|
| AsSingleQuery | ~120 | ~1500 (dublikatlar) | ~15 | Cartesian Explosion |
| AsSplitQuery | ~45 | ~600 (haqiqiy) | ~5 | ⚡ 3x tez |

### Index ta'siri — 100,000 yozuvli jadvalda SELECT

| Operatsiya | Indexsiz (ms) | Indexli (ms) | Yaxshilanish |
|---|---|---|---|
| `WHERE Name = 'X'` | ~85 | ~1 | ⚡ 85x |
| `WHERE CategoryId = 5 AND Price > 100` (composite) | ~65 | ~2 | ⚡ 32x |
| `WHERE IsActive = 1 AND Name LIKE 'A%'` (filtered) | ~50 | ~3 | ⚡ 17x |
| `ORDER BY Price DESC OFFSET 0 FETCH 20` | ~120 | ~4 | ⚡ 30x |

### Cascade Delete vs Manual Delete — 1 Category + 100 Product

| Operatsiya | O'rtacha vaqt (ms) | SQL so'rovlar | Izoh |
|---|---|---|---|
| Cascade Delete (DB level) | ~8 | 1 (DELETE Category) | DB avtomatik qiladi |
| Manual Delete (EF Core loop) | ~150 | 101 (100 product + 1 category) | N+1 muammo |
| ExecuteDeleteAsync + ExecuteDeleteAsync | ~6 | 2 | ⚡ Eng samarali |
