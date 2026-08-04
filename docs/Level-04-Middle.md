# Level 4 — Middle

---

## 1. Darslik

### 4.1 Transactions — tranzaksiyalar

EF Core da `SaveChangesAsync()` o'zi bitta tranzaksiya yaratadi, lekin ba'zan bir nechta `SaveChanges` ni bitta tranzaksiyaga birlashtirish kerak bo'ladi — masalan, bir nechta jadvalga yozish va ularning barchasi muvaffaqiyatli bo'lishini ta'minlash.

```csharp
public class OrderService(AppDbContext context)
{
    // Oddiy tranzaksiya — IDbContextTransaction
    public async Task CreateOrderAsync(int productId, int quantity)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // 1. Mahsulot zaxirasini kamaytirish
            var product = await context.Products.FindAsync(productId)
                ?? throw new InvalidOperationException("Product not found");

            if (product.Stock < quantity)
                throw new InvalidOperationException("Insufficient stock");

            product.Stock -= quantity;
            await context.SaveChangesAsync();

            // 2. Buyurtma yaratish
            var order = new Order
            {
                ProductId = productId,
                Quantity = quantity,
                TotalPrice = product.Price * quantity,
                OrderDate = DateTime.UtcNow
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            // 3. Hammasini tasdiqlash
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Execution Strategy bilan (retry logic uchun)
    public async Task CreateOrderWithRetryAsync(int productId, int quantity)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                // ... amallar ...
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}
```

**Savepoint** (EF Core 7+) — tranzaksiya ichida oraliq nuqta qo'yish:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

// Birinchi amal
context.Products.Add(new Product { Name = "A", Price = 100 });
await context.SaveChangesAsync();

// Savepoint qo'yish
await transaction.CreateSavepointAsync("AfterProductAdded");

try
{
    // Ikkinchi amal — xato bo'lishi mumkin
    context.Orders.Add(new Order { ProductId = 999, Quantity = 1 }); // FK xato!
    await context.SaveChangesAsync();
}
catch
{
    // Faqat ikkinchi amalni bekor qilish, birinchisi saqlanadi
    await transaction.RollbackToSavepointAsync("AfterProductAdded");
}

await transaction.CommitAsync();
```

---

### 4.2 Stored Procedures bilan ishlash

EF Core da Stored Procedure larni chaqirishning bir necha usuli bor:

```csharp
public class ReportService(AppDbContext context)
{
    // 1. FromSqlRaw — SELECT qaytaruvchi SP
    public async Task<List<Product>> GetTopProductsAsync(int count)
    {
        return await context.Products
            .FromSqlRaw("EXEC sp_GetTopProducts @Count = {0}", count)
            .ToListAsync();
    }

    // 2. FromSqlInterpolated — SQL Injection dan himoyalangan
    public async Task<List<Product>> GetProductsByCategoryAsync(int categoryId)
    {
        return await context.Products
            .FromSqlInterpolated(
                $"EXEC sp_GetProductsByCategory @CategoryId = {categoryId}")
            .ToListAsync();
    }

    // 3. ExecuteSqlRawAsync — natija qaytarmaydigan SP
    public async Task<int> UpdatePricesAsync(decimal percentage)
    {
        return await context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC sp_UpdateAllPrices @Percentage = {percentage}");
    }

    // 4. SqlQueryRaw — entity bo'lmagan natija (EF Core 8+)
    public async Task<List<SalesReport>> GetSalesReportAsync(DateTime from, DateTime to)
    {
        return await context.Database
            .SqlQueryRaw<SalesReport>(
                "EXEC sp_SalesReport @From = {0}, @To = {1}", from, to)
            .ToListAsync();
    }
}

// SP natijasi uchun DTO
public class SalesReport
{
    public string ProductName { get; set; } = string.Empty;
    public int TotalSold { get; set; }
    public decimal TotalRevenue { get; set; }
}
```

**⚠️ Muhim:** `FromSqlRaw` natijasi ustida LINQ operatsiyalari qo'llash mumkin (`Where`, `OrderBy`), lekin bu faqat server-side evaluation bo'lgandagina ishlaydi.

---

### 4.3 Eager / Lazy / Explicit Loading farqi

Level 3 da Eager va Lazy Loading ni ko'rdik. Endi uchinchi usul — **Explicit Loading** ni ham qo'shamiz.

```csharp
public class ProductLoader(AppDbContext context)
{
    // EXPLICIT LOADING — kerak bo'lganda qo'lda yuklash
    public async Task<Product?> GetWithExplicitLoadAsync(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null) return null;

        // Kerak bo'lganda alohida yuklash
        await context.Entry(product)
            .Reference(p => p.Category)  // bitta bog'lanish uchun
            .LoadAsync();

        await context.Entry(product)
            .Collection(p => p.OrderItems) // ko'p bog'lanish uchun
            .LoadAsync();

        return product;
    }

    // Explicit Loading da filterlash mumkin
    public async Task<Product?> GetWithFilteredOrdersAsync(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null) return null;

        await context.Entry(product)
            .Collection(p => p.OrderItems)
            .Query()
            .Where(oi => oi.Quantity > 5) // faqat keraklilarini yuklash
            .LoadAsync();

        return product;
    }
}
```

---

### 4.4 AsNoTracking() va Change Tracking

**Change Tracking** — EF Core ning avtomatik o'zgarishlarni kuzatish tizimi. Har bir `FindAsync`, `ToListAsync` chaqirilganda, qaytarilgan obyektlar Change Tracker ga qo'shiladi.

```csharp
public class ProductQueryService(AppDbContext context)
{
    // ❌ Tracking bilan — yangilamasangiz ortiqcha xotira sarflanadi
    public async Task<List<Product>> GetAllTrackedAsync()
    {
        return await context.Products.ToListAsync();
        // Har bir product Change Tracker da kuzatiladi
        // context.ChangeTracker.Entries().Count() == N
    }

    // ✅ AsNoTracking — faqat o'qish uchun
    public async Task<List<Product>> GetAllReadOnlyAsync()
    {
        return await context.Products
            .AsNoTracking()
            .ToListAsync();
        // Change Tracker da hech narsa yo'q — tezroq va kam xotira
    }

    // ✅ AsNoTrackingWithIdentityResolution — dublikat obyektlarni birlashtirish
    public async Task<List<Product>> GetWithCategoriesReadOnlyAsync()
    {
        return await context.Products
            .AsNoTrackingWithIdentityResolution()
            .Include(p => p.Category)
            .ToListAsync();
        // Agar 10 product bir xil Category ga tegishli bo'lsa,
        // 10 ta alohida Category obyekt emas, bitta Category ishlatiladi
    }

    // DbContext darajasida default NoTracking qilish
    // Program.cs:
    // builder.Services.AddDbContext<AppDbContext>(options =>
    //     options.UseSqlServer(connectionString)
    //            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
}
```

**Change Tracker holatlarini ko'rish:**

```csharp
public void InspectChangeTracker()
{
    var entries = context.ChangeTracker.Entries();

    foreach (var entry in entries)
    {
        Console.WriteLine($"Entity: {entry.Entity.GetType().Name}");
        Console.WriteLine($"State: {entry.State}"); // Added, Modified, Deleted, Unchanged, Detached

        if (entry.State == EntityState.Modified)
        {
            foreach (var prop in entry.Properties.Where(p => p.IsModified))
            {
                Console.WriteLine(
                    $"  {prop.Metadata.Name}: {prop.OriginalValue} -> {prop.CurrentValue}");
            }
        }
    }
}
```

---

### 4.5 Navigation Property vs Foreign Key

```csharp
public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }

    // Foreign Key — ma'lumotlar bazasidagi jismoniy ustun
    public int ProductId { get; set; }

    // Navigation Property — C# dagi mantiqiy bog'lanish
    public Product Product { get; set; } = null!;
}
```

**Qachon qaysi birini ishlatish:**

```csharp
public class OrderService(AppDbContext context)
{
    // ✅ Foreign Key orqali — tezroq (Product ni yuklash shart emas)
    public async Task CreateOrderFkAsync(int productId, int qty)
    {
        var order = new Order
        {
            ProductId = productId, // FK to'g'ridan-to'g'ri
            Quantity = qty,
            OrderDate = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
    }

    // ⚠️ Navigation Property orqali — qo'shimcha so'rov ketadi
    public async Task CreateOrderNavAsync(int productId, int qty)
    {
        var product = await context.Products.FindAsync(productId); // qo'shimcha SELECT!
        var order = new Order
        {
            Product = product!, // Navigation property orqali
            Quantity = qty,
            OrderDate = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
    }
}
```

---

### 4.6 Query Performance yaxshilash — NoTracking / Raw SQL / Projection

```csharp
public class OptimizedQueryService(AppDbContext context)
{
    // 1. Projection (DTO) — faqat kerakli ustunlarni olish
    public async Task<List<ProductListDto>> GetProductListAsync()
    {
        return await context.Products
            .AsNoTracking()
            .Select(p => new ProductListDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                CategoryName = p.Category.Name // Include shart emas!
            })
            .ToListAsync();
        // Faqat 4 ta ustun olinadi, Change Tracking yo'q
    }

    // 2. Raw SQL — murakkab so'rovlar uchun
    public async Task<List<Product>> GetWithRawSqlAsync(decimal minPrice)
    {
        return await context.Products
            .FromSqlInterpolated(
                $"""
                SELECT p.* FROM Products p
                INNER JOIN Categories c ON p.CategoryId = c.Id
                WHERE p.Price > {minPrice}
                AND c.IsActive = 1
                ORDER BY p.Price DESC
                """)
            .AsNoTracking()
            .ToListAsync();
    }

    // 3. Compiled Query — takroriy so'rovlar uchun (static)
    private static readonly Func<AppDbContext, decimal, IAsyncEnumerable<Product>>
        GetExpensiveProducts = EF.CompileAsyncQuery(
            (AppDbContext ctx, decimal minPrice) =>
                ctx.Products.Where(p => p.Price > minPrice));

    public async Task<List<Product>> GetExpensiveAsync(decimal minPrice)
    {
        var result = new List<Product>();
        await foreach (var product in GetExpensiveProducts(context, minPrice))
        {
            result.Add(product);
        }
        return result;
    }
}

public class ProductListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}
```

---

### 4.7 Include() — chuqurroq va Filtered Include

```csharp
public class IncludeService(AppDbContext context)
{
    // Ko'p darajali Include
    public async Task<List<Category>> GetFullHierarchyAsync()
    {
        return await context.Categories
            .Include(c => c.Products)
                .ThenInclude(p => p.OrderItems)
                    .ThenInclude(oi => oi.Order)
            .AsNoTracking()
            .ToListAsync();
    }

    // Filtered Include (EF Core 5+) — bog'langan ma'lumotlarni filterlash
    public async Task<List<Category>> GetCategoriesWithActiveProductsAsync()
    {
        return await context.Categories
            .Include(c => c.Products.Where(p => p.IsActive && p.Price > 0))
            .AsNoTracking()
            .ToListAsync();
        // Faqat aktiv va narxi > 0 mahsulotlar yuklanadi
    }

    // Bir nechta bog'lanishni Include qilish
    public async Task<Product?> GetProductFullAsync(int id)
    {
        return await context.Products
            .Include(p => p.Category)
            .Include(p => p.OrderItems)
            .Include(p => p.Reviews)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}
```

---

### 4.8 One-to-Many va Many-to-Many

Level 3 da One-to-Many ni ko'rdik. Endi **Many-to-Many** munosabatini o'rganamiz.

```csharp
// EF Core 7+ — Skip Navigation (oraliq jadval kerak emas)
public class Student
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    public ICollection<Course> Courses { get; set; } = [];
}

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public ICollection<Student> Students { get; set; } = [];
}
// EF Core avtomatik "CourseStudent" oraliq jadvalini yaratadi
```

```csharp
// Oraliq jadval bilan (qo'shimcha ma'lumot kerak bo'lsa)
public class Student
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public ICollection<StudentCourse> StudentCourses { get; set; } = [];
}

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<StudentCourse> StudentCourses { get; set; } = [];
}

// Oraliq jadval — qo'shimcha ma'lumotlar bilan
public class StudentCourse
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public DateTime EnrolledAt { get; set; }
    public decimal? Grade { get; set; }
}

// Fluent API konfiguratsiya
public class StudentCourseConfiguration : IEntityTypeConfiguration<StudentCourse>
{
    public void Configure(EntityTypeBuilder<StudentCourse> builder)
    {
        builder.HasKey(sc => new { sc.StudentId, sc.CourseId }); // Composite PK

        builder.HasOne(sc => sc.Student)
            .WithMany(s => s.StudentCourses)
            .HasForeignKey(sc => sc.StudentId);

        builder.HasOne(sc => sc.Course)
            .WithMany(c => c.StudentCourses)
            .HasForeignKey(sc => sc.CourseId);
    }
}
```

**Many-to-Many so'rovlari:**

```csharp
public class EnrollmentService(AppDbContext context)
{
    // Talabani kursga ro'yxatga olish
    public async Task EnrollAsync(int studentId, int courseId)
    {
        var enrollment = new StudentCourse
        {
            StudentId = studentId,
            CourseId = courseId,
            EnrolledAt = DateTime.UtcNow
        };
        context.StudentCourses.Add(enrollment);
        await context.SaveChangesAsync();
    }

    // Talabaning barcha kurslari
    public async Task<List<Course>> GetStudentCoursesAsync(int studentId)
    {
        return await context.StudentCourses
            .Where(sc => sc.StudentId == studentId)
            .Select(sc => sc.Course)
            .AsNoTracking()
            .ToListAsync();
    }

    // Kursda nechta talaba bor
    public async Task<int> GetCourseStudentCountAsync(int courseId)
    {
        return await context.StudentCourses
            .CountAsync(sc => sc.CourseId == courseId);
    }
}
```

---

## 2. O'rganish metodi

### Topshiriq 1: "E-Commerce" tranzaksiya tizimi
Buyurtma yaratish tizimini qurib, quyidagilarni amalga oshiring:
- `Order` va `OrderItem` entitylarini yarating
- Buyurtma yaratish: mahsulot zaxirasini kamaytirish + order yaratish — bitta tranzaksiyada
- Agar zaxira yetarli bo'lmasa — rollback
- Savepoint bilan murakkab tranzaksiya yozing (3+ amal)

### Topshiriq 2: Performance optimizatsiya
Mavjud so'rovlarni optimallashtiring:
- Barcha `ToListAsync()` larni `AsNoTracking()` bilan yozing (faqat o'qish uchun)
- 3 ta Projection (DTO) so'rov yarating — faqat kerakli ustunlarni oling
- `Include` o'rniga `Select` ishlatib, bir xil natijani kamroq xotira bilan oling
- Filtered Include bilan faqat aktiv ma'lumotlarni yuklang

### Topshiriq 3: Many-to-Many loyiha
"Talabalar va Kurslar" tizimini yarating:
- `Student`, `Course`, `StudentCourse` (oraliq jadval, `EnrolledAt`, `Grade` bilan)
- Talabani kursga yozish, kursdan chiqarish
- Talabaning barcha kurslarini olish
- Kursda eng yuqori baho olgan 3 talabani olish

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync` ishlataman
- [ ] Savepoint bilan ishlashni bilaman
- [ ] Stored Procedure larni `FromSqlInterpolated` bilan chaqira olaman
- [ ] Eager / Lazy / Explicit Loading farqini tushunaman va to'g'ri tanlash bilaman
- [ ] `AsNoTracking()` qachon ishlatishni bilaman
- [ ] Change Tracker holatlarini (`Added`, `Modified`, `Deleted`) tushunaman
- [ ] Projection (DTO) bilan query optimizatsiya qilaman
- [ ] Many-to-Many munosabatini (oraliq jadval bilan) yarata olaman

---

## 3. Solishtirish jadvali: Eager Loading vs Lazy Loading vs Explicit Loading

| Mezon | Eager Loading | Lazy Loading | Explicit Loading |
|---|---|---|---|
| **Sintaksis** | `.Include(x => x.Nav)` | `virtual` property | `Entry().Reference().LoadAsync()` |
| **Qachon yuklanadi** | Asosiy so'rov bilan birga | Propertyga murojaat qilinganda | `LoadAsync()` chaqirilganda |
| **SQL so'rovlar soni** | 1 (JOIN) | 1 + N (N+1 xavfi) | 1 + kerakli miqdor |
| **Nazorat** | Oldindan belgilash | Avtomatik | ✅ To'liq nazorat |
| **Performance** | ✅ Yaxshi (agar kerak bo'lsa) | ⚠️ N+1 xavfi | ✅ Yaxshi (selektiv) |
| **NuGet paket** | Kerak emas | `EFCore.Proxies` kerak | Kerak emas |
| **Filterlash** | Filtered Include (EF 5+) | ❌ Mumkin emas | ✅ `.Query().Where()` |
| **Tavsiya** | ✅ Default tanlov | ⚠️ Ehtiyotkorlik bilan | Shartli yuklash kerak bo'lganda |

---

## 4. Test

### Savollar

**1.** Tranzaksiya ichida xatolik yuz berganda nima bo'ladi (agar `try-catch` ichida `RollbackAsync` chaqirilsa)?
- a) Faqat oxirgi amal bekor qilinadi
- b) Tranzaksiya boshidan barcha amallar bekor qilinadi
- c) Ma'lumotlar bazasi qayta ishga tushadi
- d) Hech narsa bo'lmaydi

**2.** Quyidagi kod xavfsizmi? Nima uchun?
```csharp
var name = "'; DROP TABLE Products; --";
var products = await context.Products
    .FromSqlRaw($"SELECT * FROM Products WHERE Name = '{name}'")
    .ToListAsync();
```

**3.** `AsNoTracking()` qachon ishlatish kerak?
- a) Har doim
- b) Faqat o'qish (read-only) so'rovlarida
- c) Faqat yozish (write) operatsiyalarida
- d) Faqat Stored Procedure larda

**4.** Quyidagi kod nechta SQL so'rov generatsiya qiladi?
```csharp
var product = await context.Products.FindAsync(1);
await context.Entry(product!).Reference(p => p.Category).LoadAsync();
await context.Entry(product!).Collection(p => p.Reviews).LoadAsync();
```
- a) 1 ta
- b) 2 ta
- c) 3 ta
- d) 0 ta

**5.** Many-to-Many munosabatda oraliq jadval qachon kerak?
- a) Har doim
- b) Oraliq jadvalda qo'shimcha ma'lumot (masalan, sana, baho) kerak bo'lganda
- c) Hech qachon, EF Core avtomatik yaratadi
- d) Faqat SQL Server da

**6.** `FromSqlInterpolated` va `FromSqlRaw` ning farqi nimada?

**7.** Quyidagi kodda performance muammo bor. Uni toping va tuzating:
```csharp
var categories = await context.Categories.ToListAsync();
foreach (var cat in categories)
{
    var products = await context.Products
        .Where(p => p.CategoryId == cat.Id)
        .ToListAsync();
    cat.ProductCount = products.Count;
}
```

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Natijalar **taxminiy/indikativ**, BenchmarkDotNet, .NET 8, SQL Server 2022.

### AsNoTracking ta'siri — 10,000 ta yozuv o'qish

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (MB) | Izoh |
|---|---|---|---|
| `ToListAsync()` (Tracking) | ~35 | ~12 | Change Tracker ishlaydi |
| `AsNoTracking().ToListAsync()` | ~18 | ~6 | ⚡ ~2x tez, ~2x kam xotira |
| `AsNoTrackingWithIdentityResolution()` | ~22 | ~7 | O'rtacha — dublikatlar birlashtiriladi |

### Projection vs Include — 5,000 product + category

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (MB) | Izoh |
|---|---|---|---|
| `Include(p => p.Category).ToListAsync()` | ~25 | ~8 | Barcha ustunlar yuklanadi |
| `Select(p => new DTO {...}).ToListAsync()` | ~10 | ~2.5 | ⚡ ~2.5x tez |

### Stored Procedure vs LINQ — murakkab so'rov

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (KB) | Izoh |
|---|---|---|---|
| LINQ (3x Include + Where + OrderBy) | ~45 | ~5000 | EF Core SQL generatsiya qiladi |
| Stored Procedure (optimallashtirilgan) | ~12 | ~800 | ⚡ DBA tomonidan optimized |
| Raw SQL (FromSqlInterpolated) | ~15 | ~900 | SP ga yaqin, lekin kod ichida |

### Transaction overhead

| Operatsiya | O'rtacha vaqt (ms) | Izoh |
|---|---|---|
| 3x SaveChanges (tranzaksiyasiz) | ~15 | Har biri alohida tranzaksiya |
| 3x SaveChanges (bitta tranzaksiyada) | ~12 | Biroz tezroq — bitta connection |
| 1x SaveChanges (barchasini birga) | ~6 | ⚡ Eng tez — bitta round-trip |
