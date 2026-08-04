# Level 5 — Middle+ (Performance & Indexing)

---

## 1. Darslik

### 5.1 EF Core da Transaction — ilg'or senaryolar

Level 4 da oddiy tranzaksiyalarni ko'rdik. Endi distributed scenario va `TransactionScope` bilan ishlashni o'rganamiz.

```csharp
public class AdvancedTransactionService(AppDbContext context, ILogger<AdvancedTransactionService> logger)
{
    // TransactionScope — bir nechta DbContext yoki boshqa resurslarni birlashtirish
    public async Task TransferFundsAsync(int fromAccountId, int toAccountId, decimal amount)
    {
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled); // Async uchun shart!

        var fromAccount = await context.Accounts.FindAsync(fromAccountId)
            ?? throw new InvalidOperationException("Source account not found");

        var toAccount = await context.Accounts.FindAsync(toAccountId)
            ?? throw new InvalidOperationException("Target account not found");

        if (fromAccount.Balance < amount)
            throw new InvalidOperationException("Insufficient funds");

        fromAccount.Balance -= amount;
        toAccount.Balance += amount;

        // Audit log
        context.AuditLogs.Add(new AuditLog
        {
            Action = "Transfer",
            Details = $"From {fromAccountId} to {toAccountId}: {amount:C}",
            Timestamp = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        scope.Complete(); // Tranzaksiyani tasdiqlash
    }

    // Isolation Level larni tushunish
    // ReadUncommitted — eng tez, lekin "dirty read" mumkin
    // ReadCommitted — default, commit qilinganlarni o'qiydi
    // RepeatableRead — o'qilgan qatorlar lock qilinadi
    // Serializable — eng xavfsiz, lekin eng sekin (full table lock)
    // Snapshot — versiyalash orqali, locklar kam

    public async Task<decimal> GetBalanceSnapshotAsync(int accountId)
    {
        await using var transaction = await context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Snapshot);

        var balance = await context.Accounts
            .Where(a => a.Id == accountId)
            .Select(a => a.Balance)
            .FirstOrDefaultAsync();

        await transaction.CommitAsync();
        return balance;
    }
}
```

---

### 5.2 ChangeTracker — chuqur tushunish

`ChangeTracker` EF Core ning qalbi — u qaysi obyektlar qo'shilgan, o'zgartirilgan yoki o'chirilganini kuzatadi.

```csharp
public class AuditableDbContext : DbContext
{
    // SaveChanges dan oldin avtomatik audit ma'lumotlarini qo'shish
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<IAuditable>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = GetCurrentUser();
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = GetCurrentUser();
                    // CreatedAt ni o'zgartirilmasin
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private string GetCurrentUser() => "system"; // IHttpContextAccessor dan olish mumkin
}

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    string CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
}
```

**ChangeTracker dan o'zgarishlar tarixini olish:**

```csharp
public class ChangeTrackingService(AppDbContext context)
{
    public List<ChangeLog> GetPendingChanges()
    {
        var changes = new List<ChangeLog>();

        foreach (var entry in context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added
                     or EntityState.Modified
                     or EntityState.Deleted))
        {
            var change = new ChangeLog
            {
                EntityName = entry.Entity.GetType().Name,
                State = entry.State.ToString(),
                Changes = []
            };

            foreach (var prop in entry.Properties)
            {
                if (entry.State == EntityState.Modified && prop.IsModified)
                {
                    change.Changes.Add(new PropertyChange
                    {
                        PropertyName = prop.Metadata.Name,
                        OldValue = prop.OriginalValue?.ToString(),
                        NewValue = prop.CurrentValue?.ToString()
                    });
                }
            }
            changes.Add(change);
        }

        return changes;
    }

    // ChangeTracker ni tozalash — xotirani bo'shatish
    public void ClearTracker()
    {
        context.ChangeTracker.Clear();
        // Barcha tracked entitylar detach qilinadi
    }
}
```

---

### 5.3 Value Object va Complex Types (EF Core 8)

**Value Object** — identifikatori (Id) bo'lmagan, faqat qiymatlari bilan aniqlangan obyekt. EF Core 8 da `ComplexType` sifatida qo'llab-quvvatlanadi.

```csharp
// Value Object — Address
[ComplexType]
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

// Value Object — Money
[ComplexType]
public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}

// Entity da ishlatish
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Value Object — alohida jadval emas, shu jadval ichida ustunlar sifatida
    public Address ShippingAddress { get; set; } = new();
    public Address BillingAddress { get; set; } = new();
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Money Price { get; set; } = new();
}
```

```csharp
// Fluent API konfiguratsiya (agar [ComplexType] attribute ishlatilmasa)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Customer>(b =>
    {
        b.ComplexProperty(c => c.ShippingAddress, a =>
        {
            a.Property(x => x.Street).HasMaxLength(200).HasColumnName("ShippingStreet");
            a.Property(x => x.City).HasMaxLength(100).HasColumnName("ShippingCity");
            a.Property(x => x.ZipCode).HasMaxLength(20).HasColumnName("ShippingZip");
            a.Property(x => x.Country).HasMaxLength(50).HasColumnName("ShippingCountry");
        });

        b.ComplexProperty(c => c.BillingAddress, a =>
        {
            a.Property(x => x.Street).HasMaxLength(200).HasColumnName("BillingStreet");
            a.Property(x => x.City).HasMaxLength(100).HasColumnName("BillingCity");
        });
    });
}

// Ma'lumotlar bazasida: Customers jadvali
// Id | Name | ShippingStreet | ShippingCity | ShippingZip | ShippingCountry | BillingStreet | BillingCity | ...
```

**Owned Types (EF Core 5+) — eski usul:**

```csharp
// Owned Type — alohida jadvalda yoki shu jadval ichida
modelBuilder.Entity<Customer>()
    .OwnsOne(c => c.ShippingAddress, a =>
    {
        a.Property(x => x.Street).HasMaxLength(200);
    });
```

**Farq:** `ComplexType` (EF Core 8) — har doim shu jadval ichida, null bo'la olmaydi. `OwnedType` — alohida jadvalda ham bo'lishi mumkin, null bo'lishi mumkin.

---

### 5.4 Raw SQL — xavf va foyda

```csharp
public class RawSqlService(AppDbContext context)
{
    // ✅ XAVFSIZ — parametrli so'rov
    public async Task<List<Product>> SafeQueryAsync(string category, decimal minPrice)
    {
        return await context.Products
            .FromSqlInterpolated(
                $"SELECT * FROM Products WHERE CategoryName = {category} AND Price > {minPrice}")
            .ToListAsync();
        // EF Core avtomatik SqlParameter yaratadi
    }

    // ❌ XAVFLI — SQL Injection!
    public async Task<List<Product>> UnsafeQueryAsync(string userInput)
    {
        // HECH QACHON bunday qilmang!
        return await context.Products
            .FromSqlRaw($"SELECT * FROM Products WHERE Name = '{userInput}'")
            .ToListAsync();
        // userInput = "'; DROP TABLE Products; --" bo'lsa?!
    }

    // ✅ XAVFSIZ — FromSqlRaw bilan parametr
    public async Task<List<Product>> SafeRawQueryAsync(string name)
    {
        return await context.Products
            .FromSqlRaw("SELECT * FROM Products WHERE Name = {0}", name)
            .ToListAsync();
        // {0} avtomatik parameterga aylanadi
    }

    // EF Core 8 — SqlQueryRaw (entity bo'lmagan natija)
    public async Task<List<DailySales>> GetDailySalesAsync(DateTime date)
    {
        return await context.Database
            .SqlQueryRaw<DailySales>(
                """
                SELECT
                    CAST(OrderDate AS DATE) AS SaleDate,
                    COUNT(*) AS OrderCount,
                    SUM(TotalPrice) AS TotalRevenue
                FROM Orders
                WHERE CAST(OrderDate AS DATE) = {0}
                GROUP BY CAST(OrderDate AS DATE)
                """,
                date)
            .ToListAsync();
    }
}
```

---

### 5.5 DbContext Lifetime — Scoped vs Singleton vs Transient

**DbContext** — DI konteynerida qanday register qilinishi juda muhim.

```csharp
// ✅ SCOPED — default va tavsiya etilgan
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
// Har bir HTTP request uchun bitta DbContext yaratiladi
// Request tugagandan keyin Dispose bo'ladi

// ❌ SINGLETON — HECH QACHON ishlatmang (oddiy holatda)
builder.Services.AddSingleton<AppDbContext>(); // XATO!
// Muammolar:
// 1. DbContext thread-safe emas
// 2. Change Tracker o'sib ketadi — xotira leak
// 3. Concurrency muammolari

// ⚠️ TRANSIENT — har bir inject uchun yangi instance
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString),
    ServiceLifetime.Transient);
// Har bir inject qilish yangi DbContext yaratadi
// Afzalligi: parallel operatsiyalar uchun
// Kamchiligi: bitta request da bir nechta DbContext = bitta tranzaksiya yo'q

// ✅ DbContext Factory — parallel operatsiyalar uchun eng yaxshi
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
```

**DbContext Factory ishlatish:**

```csharp
public class ParallelService(IDbContextFactory<AppDbContext> contextFactory)
{
    // Har bir parallel operatsiya uchun alohida DbContext
    public async Task ProcessParallelAsync(List<int> productIds)
    {
        var tasks = productIds.Select(async id =>
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var product = await context.Products.FindAsync(id);
            if (product is not null)
            {
                product.LastChecked = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        });

        await Task.WhenAll(tasks);
    }
}
```

---

## 2. O'rganish metodi

### Topshiriq 1: Auditable Entity tizimi
- `IAuditable` interfeysi yarating (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`)
- `SaveChangesAsync` ni override qilib, audit ma'lumotlarini avtomatik to'ldiring
- `ChangeTracker` dan o'zgarishlar tarixini (Change Log) oluvchi servis yarating

### Topshiriq 2: Value Object qo'llash
- `Address` Value Object yarating va `Customer` entity da ishlatib, migration qiling
- `Money` Value Object yarating (`Amount` + `Currency`) va `Product.Price` ni almashtiring
- So'rov yozing: "Toshkent shahridagi barcha customerlar"

### Topshiriq 3: DbContext Lifetime tajriba
- Scoped va Transient DbContext ni parallel operatsiyalarda solishtiring
- `IDbContextFactory` bilan 100 ta productni parallel yangilang
- Har birining vaqtini o'lchang va taqqoslang

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] `TransactionScope` va Isolation Level larni tushunaman
- [ ] `ChangeTracker` ni override qilib audit tizimini yarata olaman
- [ ] `ComplexType` (Value Object) ni entity da ishlataman
- [ ] `ComplexType` vs `OwnedType` farqini bilaman
- [ ] Raw SQL ning xavfli va xavfsiz usullarini ajrata olaman
- [ ] DbContext Lifetime (Scoped/Transient) to'g'ri tanlay olaman
- [ ] `IDbContextFactory` ni parallel operatsiyalar uchun ishlataman

---

## 3. Solishtirish jadvali: DbContext Lifetime

| Mezon | Scoped | Transient | Singleton | Factory |
|---|---|---|---|---|
| **Instance soni** | 1 / HTTP request | Har inject uchun yangi | 1 ta umumiy | Kerak bo'lganda yaratiladi |
| **Thread-safety** | ✅ (1 request = 1 thread) | ✅ | ❌ Xavfli | ✅ |
| **Change Tracking** | Request davomida ishlaydi | Har bir instance alohida | ⚠️ O'sib ketadi (leak) | Har bir instance alohida |
| **Tranzaksiya** | ✅ 1 request = 1 tranzaksiya | ⚠️ Har biri alohida | ❌ Muammo | Qo'lda boshqariladi |
| **Parallel ops** | ❌ Thread-safe emas | ✅ | ❌ | ✅ Eng yaxshi |
| **Xotira** | O'rtacha | Ko'proq (ko'p instance) | ⚠️ O'sib ketadi | Boshqariladigan |
| **Qachon ishlatiladi** | ✅ Default tanlov | Kam hollarda | ❌ Ishlatmang | Background tasklar, parallel |
| **DI register** | `AddDbContext<T>()` | `AddDbContext<T>(..., Transient)` | ❌ | `AddDbContextFactory<T>()` |

---

## 4. Test

### Savollar

**1.** `TransactionScope` da `TransactionScopeAsyncFlowOption.Enabled` nima uchun kerak?
- a) Performance uchun
- b) Async/await bilan to'g'ri ishlashi uchun
- c) SQL Server uchun
- d) Logging uchun

**2.** Quyidagi kod nima muammo keltirib chiqarishi mumkin?
```csharp
builder.Services.AddSingleton<AppDbContext>();
```

**3.** `ComplexType` va `OwnedType` ning asosiy farqi nimada?
- a) Farqi yo'q
- b) `ComplexType` null bo'la olmaydi va alohida jadvalda bo'lmaydi
- c) `OwnedType` tezroq
- d) `ComplexType` faqat string uchun

**4.** Quyidagi kod xavfsizmi?
```csharp
var input = "Electronics";
await context.Products
    .FromSqlInterpolated($"SELECT * FROM Products WHERE Category = {input}")
    .ToListAsync();
```
- a) Yo'q, SQL Injection xavfi bor
- b) Ha, `FromSqlInterpolated` avtomatik parametrlaydi
- c) Faqat raqamlar uchun xavfsiz
- d) Faqat stored procedure bilan xavfsiz

**5.** `ChangeTracker.Clear()` qachon ishlatiladi?

**6.** `IDbContextFactory` ning oddiy `AddDbContext` dan afzalligi nimada?
- a) Tezroq ishlaydi
- b) Parallel va background operatsiyalarda thread-safe DbContext yaratish
- c) Kamroq xotira sarflaydi
- d) SQL Injection dan himoya qiladi

**7.** `SaveChangesAsync` ni override qilishning real loyihadagi foydasiga misol keltiring.

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Natijalar **taxminiy/indikativ**, BenchmarkDotNet, .NET 8, SQL Server 2022.

### Isolation Level ta'siri — 1000 ta yozuvni o'qish (parallel 10 so'rov)

| Isolation Level | O'rtacha vaqt (ms) | Lock miqdori | Izoh |
|---|---|---|---|
| ReadUncommitted | ~5 | Yo'q | ⚡ Eng tez, dirty read xavfi |
| ReadCommitted | ~12 | Row-level | ✅ Default |
| RepeatableRead | ~25 | Row lock (uzoq) | O'qilgan qatorlar lock |
| Serializable | ~80 | Table lock | ❌ Eng sekin |
| Snapshot | ~10 | Versioning | ✅ Tez va xavfsiz |

### DbContext Lifetime — 1000 ta CRUD (sequential)

| Lifetime | O'rtacha vaqt (ms) | Xotira peak (MB) | Izoh |
|---|---|---|---|
| Scoped (1 DbContext) | ~400 | ~25 | Change Tracker o'sib boradi |
| Factory (har biriga yangi) | ~450 | ~8 | Xotira deyarli o'smaydi |
| Scoped + Clear() har 100 ta | ~410 | ~12 | O'rtacha variant |

### Value Object vs alohida jadval — 5000 customer o'qish

| Yondashuv | O'rtacha vaqt (ms) | Izoh |
|---|---|---|
| ComplexType (shu jadvalda) | ~8 | ⚡ JOIN yo'q |
| Owned Type (alohida jadval) | ~15 | JOIN kerak |
| Alohida entity + Include | ~18 | Include overhead |
