# Level 3 — Junior+ / Middle boshlang'ich

---

## 1. Darslik

### 3.1 LINQ Queries — ilg'or so'rovlar

Level 2 da oddiy LINQ operatsiyalarini ko'rdik. Endi murakkabroq so'rovlarni o'rganamiz: `GroupBy`, `Join`, `Aggregate` funksiyalar va murakkab filterlash.

```csharp
public class AdvancedQueryService(AppDbContext context)
{
    // GroupBy — guruhlash
    public async Task<List<CategorySummary>> GetCategorySummaryAsync()
    {
        return await context.Products
            .GroupBy(p => p.Category.Name)
            .Select(g => new CategorySummary
            {
                CategoryName = g.Key,
                ProductCount = g.Count(),
                AveragePrice = g.Average(p => p.Price),
                TotalValue = g.Sum(p => p.Price),
                MaxPrice = g.Max(p => p.Price)
            })
            .OrderByDescending(c => c.ProductCount)
            .ToListAsync();
    }

    // Murakkab filterlash — bir nechta shart
    public async Task<List<Product>> SearchProductsAsync(
        string? name, decimal? minPrice, decimal? maxPrice, int? categoryId)
    {
        var query = context.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(p => p.Name.Contains(name));

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    // Let — oraliq hisoblash
    public async Task<List<ProductDiscount>> GetDiscountedAsync()
    {
        return await context.Products
            .Where(p => p.Price > 100)
            .Select(p => new ProductDiscount
            {
                Name = p.Name,
                OriginalPrice = p.Price,
                DiscountedPrice = p.Price * 0.9m,  // 10% chegirma
                Savings = p.Price * 0.1m
            })
            .ToListAsync();
    }
}

// DTO classlar
public class CategorySummary
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal TotalValue { get; set; }
    public decimal MaxPrice { get; set; }
}

public class ProductDiscount
{
    public string Name { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public decimal Savings { get; set; }
}
```

**Muhim:** EF Core har doim ham barcha LINQ operatsiyalarini SQL ga tarjima qila olmaydi. Agar tarjima qilib bo'lmasa, `InvalidOperationException` beradi. Masalan, o'zingiz yozgan C# metodni `Where` ichida ishlatish — client-side evaluation kerak bo'ladi.

---

### 3.2 Lazy Loading vs Eager Loading

**Eager Loading** — bog'langan ma'lumotlarni asosiy so'rov bilan birga yuklash (`Include`).
**Lazy Loading** — bog'langan ma'lumotlarni faqat murojaat qilinganda yuklash (avtomatik qo'shimcha so'rov).

```csharp
// ✅ EAGER LOADING — Include bilan
public async Task<List<Product>> GetProductsWithCategoryAsync()
{
    return await context.Products
        .Include(p => p.Category) // JOIN qiladi
        .ToListAsync();
    // SQL: SELECT p.*, c.* FROM Products p
    //      LEFT JOIN Categories c ON p.CategoryId = c.Id
}

// Ko'p darajali Include
public async Task<List<Category>> GetCategoriesWithProductsAsync()
{
    return await context.Categories
        .Include(c => c.Products)
            .ThenInclude(p => p.OrderItems) // ichma-ich
        .ToListAsync();
}
```

```csharp
// ⚠️ LAZY LOADING — sozlash kerak

// 1-qadam: NuGet paketi qo'shish
// dotnet add package Microsoft.EntityFrameworkCore.Proxies

// 2-qadam: DbContext da yoqish
builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseSqlServer(connectionString)
        .UseLazyLoadingProxies()); // Lazy loading yoqildi

// 3-qadam: Navigation propertylarni virtual qilish
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public virtual Category Category { get; set; } = null!;  // virtual!
}

// Ishlatish — har bir murojaat alohida SQL so'rov yuboradi
var product = await context.Products.FirstAsync();
var categoryName = product.Category.Name; // Bu yerda alohida SELECT chaqiriladi!
```

**⚠️ N+1 Problem (Lazy Loading xavfi):**

```csharp
// ❌ YOMON — N+1 muammo
var products = await context.Products.ToListAsync(); // 1 ta so'rov
foreach (var product in products)
{
    // Har bir iteratsiyada alohida so'rov!
    Console.WriteLine(product.Category.Name); // N ta qo'shimcha so'rov
}
// Jami: 1 + N ta so'rov (100 mahsulot = 101 so'rov!)

// ✅ YAXSHI — Eager Loading bilan
var products = await context.Products
    .Include(p => p.Category)
    .ToListAsync(); // Faqat 1 ta JOIN so'rov
foreach (var product in products)
{
    Console.WriteLine(product.Category.Name); // Qo'shimcha so'rov yo'q
}
```

**Tavsiya:** Lazy Loading dan foydalanmaslik tavsiya etiladi. Eager Loading (`Include`) yoki Projection (`Select`) ishlatish samaraliroq.

---

### 3.3 Fluent API vs Data Annotations

EF Core da entity konfiguratsiyasini ikki usulda qilish mumkin:

**Data Annotations** — entity class ustiga attribute yozish:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; } = null!;
}
```

**Fluent API** — `OnModelCreating` metodida konfiguratsiya:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Price)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Cascade emas!
        });
    }
}
```

**Eng yaxshi amaliyot — alohida Configuration class:**

```csharp
// Configurations/ProductConfiguration.cs
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Price)
            .HasColumnType("decimal(18,2)");

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index
        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.CategoryId);
    }
}

// DbContext da ro'yxatdan o'tkazish
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Assembly dagi barcha IEntityTypeConfiguration larni avtomatik topadi
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

---

### 3.4 OnModelCreating() — batafsil

`OnModelCreating` — DbContext ning virtual metodi bo'lib, ma'lumotlar bazasi sxemasini sozlash uchun ishlatiladi.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // 1. Global query filter (soft delete)
    modelBuilder.Entity<Product>()
        .HasQueryFilter(p => !p.IsDeleted);

    // 2. Default qiymat
    modelBuilder.Entity<Product>()
        .Property(p => p.CreatedAt)
        .HasDefaultValueSql("GETUTCDATE()");

    // 3. Computed column
    modelBuilder.Entity<Product>()
        .Property(p => p.DisplayName)
        .HasComputedColumnSql("[Name] + ' - $' + CAST([Price] AS NVARCHAR)", stored: true);

    // 4. Seed data (boshlang'ich ma'lumotlar)
    modelBuilder.Entity<Category>().HasData(
        new Category { Id = 1, Name = "Electronics" },
        new Category { Id = 2, Name = "Clothing" },
        new Category { Id = 3, Name = "Books" }
    );

    // 5. Alohida konfiguratsiya fayllarini qo'llash
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

**Muhim:** `HasData` bilan qo'shilgan seed data migration ichiga yoziladi va faqat migration apply qilinganda ma'lumotlar bazasiga kiritiladi.

---

### 3.5 Migration yaratish va ishlatish — ilg'or

```bash
# Migration nomini tushunarli qiling
dotnet ef migrations add AddProductIndexAndSeedData

# Ma'lumotlar bazasini yangilash
dotnet ef database update

# Migration ro'yxatini ko'rish
dotnet ef migrations list

# Production uchun SQL skript yaratish (aniq migration oralig'i)
dotnet ef migrations script InitialCreate AddProductIndexAndSeedData \
    --output migration.sql --idempotent

# --idempotent: skript qayta ishlatilsa ham xato bermaydi
```

**Migration da qo'lda o'zgartirish:**

```csharp
// Migration faylini ochib, qo'shimcha SQL yozish mumkin
public partial class AddProductIndexAndSeedData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // EF Core generatsiya qilgan kod
        migrationBuilder.CreateIndex(
            name: "IX_Products_Name",
            table: "Products",
            column: "Name");

        // Qo'lda qo'shilgan SQL
        migrationBuilder.Sql(
            "CREATE VIEW vw_ProductSummary AS " +
            "SELECT c.Name AS Category, COUNT(p.Id) AS ProductCount " +
            "FROM Categories c LEFT JOIN Products p ON c.Id = p.CategoryId " +
            "GROUP BY c.Name");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ProductSummary");
        migrationBuilder.DropIndex("IX_Products_Name", "Products");
    }
}
```

---

### 3.6 One-to-Many Relationship — chuqurroq

Level 2 da asosiy one-to-many ko'rdik. Endi uni Fluent API orqali batafsil konfiguratsiya qilishni o'rganamiz.

```csharp
// Entity lar
public class Author
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Biography { get; set; }

    // Navigation: bir author ning ko'p kitoblari
    public ICollection<Book> Books { get; set; } = [];
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int PublishedYear { get; set; }

    // Foreign Key
    public int AuthorId { get; set; }

    // Navigation: kitob qaysi authorga tegishli
    public Author Author { get; set; } = null!;
}
```

```csharp
// Fluent API konfiguratsiya
public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.HasOne(b => b.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(b => b.AuthorId)
            .OnDelete(DeleteBehavior.Cascade); // Author o'chirilsa, kitoblar ham o'chadi

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.Price)
            .HasColumnType("decimal(18,2)");
    }
}
```

**So'rovlar:**

```csharp
public class BookService(AppDbContext context)
{
    // Author va uning kitoblarini olish
    public async Task<Author?> GetAuthorWithBooksAsync(int authorId)
    {
        return await context.Authors
            .Include(a => a.Books)
            .FirstOrDefaultAsync(a => a.Id == authorId);
    }

    // Kitoblar soni bo'yicha top authorlar
    public async Task<List<AuthorSummary>> GetTopAuthorsAsync(int top = 5)
    {
        return await context.Authors
            .Select(a => new AuthorSummary
            {
                AuthorName = a.FullName,
                BookCount = a.Books.Count,
                AverageBookPrice = a.Books.Average(b => (decimal?)b.Price) ?? 0
            })
            .OrderByDescending(a => a.BookCount)
            .Take(top)
            .ToListAsync();
    }
}
```

---

## 2. O'rganish metodi

### Topshiriq 1: "Kutubxona" loyihasi
Quyidagi entitylarni yarating va Fluent API bilan konfiguratsiya qiling:
- `Author` (Id, FullName, BirthDate)
- `Book` (Id, Title, ISBN, Price, AuthorId)
- `Publisher` (Id, Name, Country)
- Author → Book (One-to-Many)
- IEntityTypeConfiguration fayllarini alohida yarating
- `ApplyConfigurationsFromAssembly` ishlatib ro'yxatga oling

### Topshiriq 2: Dynamic query builder
`SearchBooksAsync` metodi yarating:
- Ixtiyoriy parametrlar: title, authorName, minPrice, maxPrice, publishedYear
- Har bir parametr bo'lsa `IQueryable` ga shart qo'shilsin
- Sahifalash va saralash (sortBy parametri: "title", "price", "year")
- `GroupBy` bilan har bir author bo'yicha kitoblar statistikasini qaytaring

### Topshiriq 3: Lazy vs Eager Loading taqqoslash
- Bitta so'rovni Lazy Loading va Eager Loading bilan yozing
- SQL Profiler yoki `context.Database.Log` bilan nechta so'rov ketganini kuzating
- N+1 muammosini aniqlang va bartaraf qiling

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] `GroupBy`, aggregate funksiyalar (`Sum`, `Average`, `Max`) ishlataman
- [ ] Dinamik so'rov (conditional `Where`) qura olaman
- [ ] Lazy Loading va Eager Loading farqini tushunaman
- [ ] N+1 muammosini aniqlash va hal qilish bilaman
- [ ] Fluent API va Data Annotations farqini bilaman
- [ ] `IEntityTypeConfiguration<T>` bilan alohida konfiguratsiya yoza olaman
- [ ] `OnModelCreating` da seed data, query filter, default value ishlataman
- [ ] Migration da qo'lda SQL yoza olaman
- [ ] One-to-Many munosabatini to'liq konfiguratsiya qila olaman

---

## 3. Solishtirish jadvali: Fluent API vs Data Annotations

| Mezon | Data Annotations | Fluent API |
|---|---|---|
| **Yozilish joyi** | Entity class ustida (attribute) | `OnModelCreating` yoki alohida Configuration class |
| **O'qilishi** | Sodda, entity bilan birga ko'rinadi | Entity "toza" qoladi, konfiguratsiya alohida |
| **Imkoniyatlar** | Cheklangan (`Required`, `MaxLength`, `Key`) | ✅ To'liq (relationships, index, filter, computed columns) |
| **Relationships** | ⚠️ Cheklangan | ✅ To'liq nazorat (`OnDelete`, `HasMany`, etc.) |
| **Query Filter** | ❌ Mumkin emas | ✅ `HasQueryFilter()` |
| **Computed Column** | ❌ Mumkin emas | ✅ `HasComputedColumnSql()` |
| **Seed Data** | ❌ Mumkin emas | ✅ `HasData()` |
| **Index** | `[Index]` (cheklangan) | ✅ To'liq (`HasIndex`, composite, unique, filter) |
| **Clean Architecture** | ⚠️ Domain entity ga bog'liq | ✅ Infrastructure layerda alohida |
| **Tavsiya** | Oddiy loyihalar, tez prototip | ✅ Production loyihalar, katta jamoalar |

---

## 4. Test

### Savollar

**1.** Quyidagi kod qanday muammoga olib kelishi mumkin?
```csharp
var products = await context.Products.ToListAsync();
foreach (var p in products)
{
    Console.WriteLine(p.Category.Name);
}
```
- a) Hech qanday muammo yo'q
- b) N+1 so'rov muammosi (Lazy Loading yoqilgan bo'lsa)
- c) Compile-time xato
- d) Faqat bitta so'rov ketadi

**2.** `ApplyConfigurationsFromAssembly` nima qiladi?
- a) Barcha entity larni avtomatik yaratadi
- b) Assembly dagi barcha `IEntityTypeConfiguration<T>` implementatsiyalarini topib qo'llaydi
- c) Migration avtomatik yaratadi
- d) Ma'lumotlar bazasini yangilaydi

**3.** Fluent API da `OnDelete(DeleteBehavior.Restrict)` qanday ishlaydi?

**4.** Quyidagi LINQ natijasi qanday bo'ladi?
```csharp
var result = await context.Products
    .GroupBy(p => p.CategoryId)
    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
    .Where(x => x.Count > 5)
    .ToListAsync();
```

**5.** `HasQueryFilter` bilan qo'yilgan filterni vaqtincha o'chirish mumkinmi?
- a) Yo'q, umuman o'chirib bo'lmaydi
- b) Ha, `IgnoreQueryFilters()` bilan
- c) Faqat yangi DbContext yaratib
- d) Migration orqali

**6.** `HasData()` bilan qo'shilgan seed data qachon ma'lumotlar bazasiga yoziladi?
- a) `SaveChangesAsync()` chaqirilganda
- b) Migration yaratilganda
- c) `dotnet ef database update` ishga tushirilganda
- d) Dastur ishga tushganda

**7.** Quyidagi kodni optimallashtiring (xato toping va tuzating):
```csharp
var authors = await context.Authors.ToListAsync();
var result = new List<AuthorDto>();
foreach (var author in authors)
{
    result.Add(new AuthorDto
    {
        Name = author.FullName,
        BookCount = context.Books.Count(b => b.AuthorId == author.Id)
    });
}
```

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Natijalar **taxminiy/indikativ**, BenchmarkDotNet, .NET 8, SQL Server 2022. 50 ta Author, har biriga 20 ta Book (jami 1000 Book).

### Lazy Loading vs Eager Loading — Author va Books

| Operatsiya | O'rtacha vaqt (ms) | SQL so'rovlar soni | Izoh |
|---|---|---|---|
| Lazy Loading (50 author + har biriga books) | ~250 | 51 (1 + 50) | N+1 muammo — juda sekin |
| Eager Loading (`Include`) | ~8 | 1 | Bitta JOIN — tez |
| Projection (`Select` DTO ga) | ~5 | 1 | ⚡ Eng tez — faqat kerakli ustunlar |

### Data Annotations vs Fluent API — Startup vaqti

| Yondashuv | Model build vaqti (ms) | Izoh |
|---|---|---|
| Data Annotations (30 entity) | ~45 | Reflection bilan attribute larni o'qiydi |
| Fluent API (30 entity) | ~42 | Deyarli bir xil — amaliy farq yo'q |

### GroupBy — katta dataset

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (KB) | Izoh |
|---|---|---|---|
| Server-side GroupBy (EF Core LINQ) | ~12 | ~50 | SQL da `GROUP BY` — samarali |
| Client-side GroupBy (`.ToList()` keyin `.GroupBy()`) | ~85 | ~1200 | ❌ Barcha ma'lumot xotiraga yuklanadi |

### O'lchash usuli
```csharp
[MemoryDiagnoser]
public class LoadingBenchmark
{
    [Benchmark(Baseline = true)]
    public async Task<List<Author>> EagerLoading()
    {
        await using var ctx = new AppDbContext(_options);
        return await ctx.Authors.Include(a => a.Books).ToListAsync();
    }

    [Benchmark]
    public async Task<List<AuthorDto>> Projection()
    {
        await using var ctx = new AppDbContext(_options);
        return await ctx.Authors
            .Select(a => new AuthorDto
            {
                Name = a.FullName,
                BookCount = a.Books.Count
            })
            .ToListAsync();
    }
}
```
