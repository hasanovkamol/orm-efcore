# Level 1 — Kirish (Junior boshlang'ich)

---

## 1. Darslik

### 1.1 ORM nima va uning vazifasi

**ORM (Object-Relational Mapping)** — bu dasturlash tilining obyektlarini (classlar) ma'lumotlar bazasidagi jadvallar bilan avtomatik bog'laydigan texnologiya. Ya'ni, siz SQL yozmasdan, C# obyektlari orqali ma'lumotlar bazasi bilan ishlay olasiz.

**Nima uchun kerak?**
- SQL yozishni kamaytiradi
- Kodni o'qishni osonlashtiradi
- Turli ma'lumotlar bazalariga o'tishni soddalashtiradi
- Compile-time da xatoliklarni aniqlaydi (strongly-typed)

**Misol — ORMsiz va ORMli yondashuv:**

```csharp
// ❌ ADO.NET bilan (to'g'ridan-to'g'ri SQL)
var command = new SqlCommand("SELECT * FROM Products WHERE Price > 100", connection);
var reader = command.ExecuteReader();
while (reader.Read())
{
    var name = reader["Name"].ToString();
}

// ✅ EF Core (ORM) bilan
var products = dbContext.Products
    .Where(p => p.Price > 100)
    .ToList();
```

**Real loyiha analogiyasi:** ORM — bu tarjimon. Siz C# tilida gaplashasiz, ORM esa buni SQL tiliga tarjima qiladi. ADO.NET bilan ishlash — xorijiy tilda o'zingiz yozishga o'xshaydi, ORM esa avtomatik tarjimonlik qiladi.

---

### 1.2 ADO.NET nima

**ADO.NET** — .NET platformasidagi eng past darajadagi (low-level) ma'lumotlar bazasiga ulanish texnologiyasi. Barcha ORM'lar (EF Core, Dapper) ichida ADO.NET dan foydalanadi.

**Asosiy komponentlar:**
- `SqlConnection` — ma'lumotlar bazasiga ulanish
- `SqlCommand` — SQL buyrug'ini bajarish
- `SqlDataReader` — natijalarni o'qish
- `SqlParameter` — parametrli so'rovlar (SQL Injection dan himoya)

```csharp
// ADO.NET bilan CRUD
await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

// INSERT
await using var insertCmd = new SqlCommand(
    "INSERT INTO Products (Name, Price) VALUES (@name, @price)", connection);
insertCmd.Parameters.AddWithValue("@name", "Laptop");
insertCmd.Parameters.AddWithValue("@price", 5000);
await insertCmd.ExecuteNonQueryAsync();

// SELECT
await using var selectCmd = new SqlCommand("SELECT Id, Name, Price FROM Products", connection);
await using var reader = await selectCmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine($"Id: {reader.GetInt32(0)}, Name: {reader.GetString(1)}");
}
```

---

### 1.3 Dapper nima

**Dapper** — bu micro-ORM bo'lib, ADO.NET ustiga yengil qatlam (wrapper) sifatida ishlaydi. SQL yozishni talab qiladi, lekin natijani avtomatik ravishda C# obyektlariga mapping qiladi.

```csharp
// Dapper bilan ishlash
await using var connection = new SqlConnection(connectionString);

// SELECT
var products = await connection.QueryAsync<Product>(
    "SELECT Id, Name, Price FROM Products WHERE Price > @price",
    new { price = 100 });

// INSERT
await connection.ExecuteAsync(
    "INSERT INTO Products (Name, Price) VALUES (@Name, @Price)",
    new Product { Name = "Monitor", Price = 3000 });

// Single record
var product = await connection.QueryFirstOrDefaultAsync<Product>(
    "SELECT * FROM Products WHERE Id = @id",
    new { id = 1 });
```

**Dapper xususiyatlari:**
- SQL ni o'zingiz yozasiz — to'liq nazorat
- ADO.NET ga nisbatan 3-5 baravar kam kod
- EF Core ga nisbatan tezroq (chunki Change Tracking yo'q)
- Oddiy va yengil — o'rganish oson

---

### 1.4 Entity Framework Core nima va nima uchun kerak

**Entity Framework Core (EF Core)** — Microsoftning rasmiy ORM frameworki. .NET ilovalarida ma'lumotlar bazasi bilan ishlashning eng keng tarqalgan usuli.

**Nima uchun EF Core kerak?**
- SQL yozish shart emas — LINQ orqali so'rovlar
- Change Tracking — o'zgarishlarni avtomatik kuzatadi
- Migration — ma'lumotlar bazasi sxemasini kod orqali boshqarish
- Turli provayderlar — SQL Server, PostgreSQL, SQLite va boshqalar

```csharp
// EF Core o'rnatish (terminal)
// dotnet add package Microsoft.EntityFrameworkCore.SqlServer
// dotnet add package Microsoft.EntityFrameworkCore.Tools
```

---

### 1.5 DbContext va DbSet\<T\>

**DbContext** — EF Core ning markazi. U ma'lumotlar bazasiga ulanishni, so'rovlarni va o'zgarishlarni boshqaradi.

**DbSet\<T\>** — ma'lumotlar bazasidagi bitta jadvalning C# dagi "ko'rinishi". Har bir DbSet bitta jadvalga mos keladi.

```csharp
// Entity (jadval modeli)
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}

// DbContext
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
}
```

**DI (Dependency Injection) orqali ro'yxatdan o'tkazish:**

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MyShopDb;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

**Real loyiha analogiyasi:** `DbContext` — ma'lumotlar bazasi bilan aloqa qiluvchi "menejer". `DbSet<Product>` — bu menejerning "Products" jadvali bo'yicha bo'limi.

---

### 1.6 Code First yondashuvi

**Code First** — avval C# klasslari (entity) yoziladi, keyin EF Core ulardan ma'lumotlar bazasi jadvallarini yaratadi.

```csharp
// 1-qadam: Entity yaratish
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// 2-qadam: DbContext ga qo'shish
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
}

// 3-qadam: Migration yaratish (terminal)
// dotnet ef migrations add InitialCreate
// dotnet ef database update
```

Bu yondashuvda siz ma'lumotlar bazasini "kod orqali boshqarasiz" — jadval strukturasi, ustunlar, munosabatlar hammasi C# da yoziladi.

---

### 1.7 CRUD operatsiyalar (EF Core bilan)

**CRUD** — Create, Read, Update, Delete — ma'lumotlar bazasidagi 4 ta asosiy amal.

```csharp
public class ProductService(AppDbContext context)
{
    // CREATE — yangi yozuv qo'shish
    public async Task<Product> CreateAsync(string name, decimal price)
    {
        var product = new Product
        {
            Name = name,
            Price = price,
            CreatedAt = DateTime.UtcNow
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    // READ — o'qish
    public async Task<List<Product>> GetAllAsync()
    {
        return await context.Products.ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await context.Products.FindAsync(id);
    }

    // UPDATE — yangilash
    public async Task UpdatePriceAsync(int id, decimal newPrice)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null) return;

        product.Price = newPrice;
        await context.SaveChangesAsync(); // EF Core o'zgarishni avtomatik aniqlaydi
    }

    // DELETE — o'chirish
    public async Task DeleteAsync(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null) return;

        context.Products.Remove(product);
        await context.SaveChangesAsync();
    }
}
```

---

### 1.8 LINQ asoslari

**LINQ (Language Integrated Query)** — C# ichida kolleksiyalar va ma'lumotlar bazasi so'rovlarini yozish uchun til. EF Core LINQ ni SQL ga tarjima qiladi.

```csharp
// Asosiy LINQ operatsiyalari
public class ProductQueryService(AppDbContext context)
{
    // Filterlash
    public async Task<List<Product>> GetExpensiveAsync()
    {
        return await context.Products
            .Where(p => p.Price > 1000)
            .ToListAsync();
    }

    // Birinchi elementni olish
    public async Task<Product?> GetFirstCheapAsync()
    {
        return await context.Products
            .FirstOrDefaultAsync(p => p.Price < 100);
    }

    // Saralash (sorting)
    public async Task<List<Product>> GetSortedAsync()
    {
        return await context.Products
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    // Soni
    public async Task<int> GetCountAsync()
    {
        return await context.Products.CountAsync();
    }

    // Borligini tekshirish
    public async Task<bool> ExistsAsync(string name)
    {
        return await context.Products
            .AnyAsync(p => p.Name == name);
    }
}
```

**LINQ ning SQL ga tarjimasi:**
```csharp
// C# LINQ
context.Products.Where(p => p.Price > 1000).OrderBy(p => p.Name).ToList();

// SQL ga aylanadi:
// SELECT [p].[Id], [p].[Name], [p].[Price], [p].[CreatedAt]
// FROM [Products] AS [p]
// WHERE [p].[Price] > 1000
// ORDER BY [p].[Name]
```

---

## 2. O'rganish metodi

### Topshiriq 1: "Kitob do'koni" loyihasi
Yangi .NET 8 Web API loyiha yarating va quyidagi entity larni Code First bilan yarating:
- `Book` (Id, Title, Author, Price, PublishedYear)
- `AppDbContext` ni sozlang
- Migration yaratib, ma'lumotlar bazasini hosil qiling
- CRUD amallarini `BookController` da yozing

### Topshiriq 2: ADO.NET vs EF Core solishtirish
Bitta jadvalga (masalan `Students`) 10 ta yozuv qo'shish va o'qishni ikki usulda yozing:
1. ADO.NET bilan (SqlConnection, SqlCommand)
2. EF Core bilan (DbContext, LINQ)

Natijani solishtiring: qancha qator kod yozildi, qaysi biri osonroq?

### Topshiriq 3: Dapper bilan aralash
Dapper orqali yuqoridagi `Products` jadvalidan:
- Barcha mahsulotlarni oling
- Narxi 500 dan yuqori mahsulotlarni filterlang
- Yangi mahsulot qo'shing

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] ORM nima ekanini tushuntira olaman
- [ ] ADO.NET, Dapper, EF Core farqini bilaman
- [ ] `DbContext` va `DbSet<T>` nima ekanini bilaman
- [ ] Code First yondashuvida entity va migration yarata olaman
- [ ] CRUD operatsiyalarini EF Core bilan yoza olaman
- [ ] Oddiy LINQ so'rovlarini (`Where`, `FirstOrDefault`, `ToList`) qo'llay olaman

---

## 3. Solishtirish jadvali: ADO.NET vs Dapper vs EF Core

| Mezon | ADO.NET | Dapper | EF Core |
|---|---|---|---|
| **Darajasi** | Low-level | Micro-ORM | Full ORM |
| **SQL yozish** | To'liq qo'lda | To'liq qo'lda | LINQ (avtomatik) |
| **Mapping** | Qo'lda (`reader.GetString()`) | Avtomatik | Avtomatik |
| **Change Tracking** | ❌ Yo'q | ❌ Yo'q | ✅ Bor |
| **Migration** | ❌ Yo'q | ❌ Yo'q | ✅ Bor |
| **Performance** | ⚡ Eng tez | ⚡ Juda tez | 🔵 O'rtacha |
| **Kod hajmi** | 📄 Ko'p | 📄 O'rtacha | 📄 Kam |
| **O'rganish qiyinligi** | Qiyin | Oson | O'rtacha |
| **Qachon ishlatiladi** | Max performance kerak | Oddiy so'rovlar, tezlik kerak | Katta loyihalar, CRUD ko'p |
| **NuGet paketi** | `System.Data.SqlClient` | `Dapper` | `Microsoft.EntityFrameworkCore` |

---

## 4. Test

### Savollar

**1.** ORM ning asosiy vazifasi nima?
- a) Ma'lumotlar bazasi yaratish
- b) C# obyektlari va jadvallar o'rtasida mapping qilish
- c) SQL server o'rnatish
- d) HTML sahifalar yaratish

**2.** Quyidagi kodda `DbSet<Product>` nima vazifa bajaradi?
```csharp
public class AppDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();
}
```
- a) Yangi ma'lumotlar bazasi yaratadi
- b) `Products` jadvalini C# da ifodalaydi
- c) SQL so'rov yozadi
- d) Faylga yozadi

**3.** Dapper va EF Core ning asosiy farqi nimada?

**4.** Quyidagi kod nima qiladi?
```csharp
var result = await context.Products
    .Where(p => p.Price > 500)
    .OrderBy(p => p.Name)
    .ToListAsync();
```

**5.** Code First yondashuvida migration yaratish buyrug'i qaysi?
- a) `dotnet ef create migration`
- b) `dotnet ef migrations add MigrationName`
- c) `dotnet ef update`
- d) `dotnet ef generate`

**6.** `SaveChangesAsync()` metodini chaqirmasangiz nima bo'ladi?
- a) Ma'lumotlar avtomatik saqlanadi
- b) Dastur xato beradi
- c) O'zgarishlar ma'lumotlar bazasiga yozilmaydi
- d) Server qayta ishga tushadi

**7.** ADO.NET da SQL Injection dan himoya qilish uchun nima ishlatiladi?
- a) String concatenation
- b) `SqlParameter`
- c) `StringBuilder`
- d) `Convert.ToString()`

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Quyidagi natijalar **taxminiy/indikativ** bo'lib, BenchmarkDotNet yordamida .NET 8, SQL Server 2022, 1000 ta yozuv ustida o'lchangan. Aniq natijalar muhit va konfiguratsiyaga bog'liq.

### SELECT — 1000 ta yozuvni o'qish

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (KB) | Izoh |
|---|---|---|---|
| ADO.NET (DataReader) | ~0.8 | ~120 | Eng tez, lekin ko'p kod |
| Dapper (Query\<T\>) | ~1.1 | ~150 | ADO.NET ga yaqin tezlik |
| EF Core (ToListAsync) | ~3.5 | ~450 | Change Tracking tufayli sekinroq |

### INSERT — 100 ta yozuv qo'shish

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (KB) | Izoh |
|---|---|---|---|
| ADO.NET (ExecuteNonQuery loop) | ~15 | ~50 | Har bir yozuv uchun alohida buyruq |
| Dapper (Execute) | ~18 | ~60 | ADO.NET ga o'xshash |
| EF Core (AddRange + SaveChanges) | ~45 | ~300 | Change Tracking + batch |

### O'lchash usuli
```csharp
// BenchmarkDotNet bilan o'lchash namunasi
[MemoryDiagnoser]
public class OrmBenchmark
{
    [Benchmark]
    public async Task<List<Product>> EfCoreSelect()
    {
        await using var context = new AppDbContext(_options);
        return await context.Products.ToListAsync();
    }

    [Benchmark]
    public async Task<IEnumerable<Product>> DapperSelect()
    {
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<Product>("SELECT * FROM Products");
    }
}
// Ishga tushirish: dotnet run -c Release
```
