# Level 2 — Amaliyot (Junior)

---

## 1. Darslik

### 2.1 EF Core bilan obyekt qo'shish — chuqurroq

Level 1 da oddiy `Add` va `SaveChangesAsync` ni ko'rdik. Endi bir nechta yozuvni birdan qo'shish, `AddRange`, va `SaveChangesAsync` ning ichki ishlash mexanizmini o'rganamiz.

```csharp
public class ProductService(AppDbContext context)
{
    // Bitta obyekt qo'shish
    public async Task<Product> AddSingleAsync(Product product)
    {
        var entry = context.Products.Add(product);
        // entry.State == EntityState.Added bo'ladi
        await context.SaveChangesAsync();
        // SaveChanges dan keyin product.Id avtomatik to'ldiriladi (DB generated)
        return product;
    }

    // Bir nechta obyekt qo'shish
    public async Task AddMultipleAsync(List<Product> products)
    {
        context.Products.AddRange(products);
        await context.SaveChangesAsync();
        // Barcha productlarning Id si to'ldiriladi
    }
}
```

**Muhim nuqta:** `Add()` chaqirilganda, obyekt darhol ma'lumotlar bazasiga yozilmaydi. U `DbContext` ning "change tracker" iga `Added` holatida qo'shiladi. Faqat `SaveChangesAsync()` chaqirilganda barcha o'zgarishlar bitta transactionda ma'lumotlar bazasiga yuboriladi.

---

### 2.2 Ma'lumotlarni yangilash va o'chirish

```csharp
public class ProductService(AppDbContext context)
{
    // UPDATE — qidirib, keyin o'zgartirish
    public async Task<bool> UpdateAsync(int id, string newName, decimal newPrice)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null) return false;

        product.Name = newName;
        product.Price = newPrice;
        // EF Core o'zgarishni avtomatik aniqlaydi (Change Tracking)
        await context.SaveChangesAsync();
        return true;
    }

    // DELETE — qidirib, keyin o'chirish
    public async Task<bool> DeleteAsync(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null) return false;

        context.Products.Remove(product);
        await context.SaveChangesAsync();
        return true;
    }

    // .NET 8 / EF Core 8: ExecuteDeleteAsync — yangi usul (obyekt yuklash shart emas)
    public async Task<int> DeleteByPriceAsync(decimal maxPrice)
    {
        return await context.Products
            .Where(p => p.Price < maxPrice)
            .ExecuteDeleteAsync();
        // SQL: DELETE FROM Products WHERE Price < @maxPrice
        // SaveChanges chaqirish shart emas!
    }

    // EF Core 8: ExecuteUpdateAsync
    public async Task<int> IncreasePricesAsync(decimal percentage)
    {
        return await context.Products
            .Where(p => p.Price < 100)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(p => p.Price, p => p.Price * (1 + percentage / 100)));
        // SaveChanges chaqirish shart emas!
    }
}
```

**`ExecuteUpdateAsync` / `ExecuteDeleteAsync` (EF Core 7+):** Bu metodlar juda foydali, chunki:
- Obyektni oldindan yuklash (`FindAsync`) shart emas
- To'g'ridan-to'g'ri SQL generatsiya qilinadi
- `SaveChangesAsync()` chaqirish kerak emas
- Change Tracker ishlamaydi — ya'ni tezroq

---

### 2.3 SaveChanges() — chuqurroq tushuntirish

`SaveChangesAsync()` — bu DbContext dagi barcha o'zgarishlarni bitta **transaction** ichida ma'lumotlar bazasiga yozadigan metod.

```csharp
// Bir nechta o'zgarishni bitta SaveChanges da saqlash
public async Task ComplexOperationAsync()
{
    // 1. Yangi mahsulot qo'shish
    var product = new Product { Name = "Keyboard", Price = 200 };
    context.Products.Add(product);

    // 2. Mavjud mahsulotni yangilash
    var existing = await context.Products.FindAsync(1);
    if (existing is not null)
        existing.Price = 999;

    // 3. Boshqa mahsulotni o'chirish
    var toDelete = await context.Products.FindAsync(5);
    if (toDelete is not null)
        context.Products.Remove(toDelete);

    // Hammasi bitta transactionda saqlanadi
    var affectedRows = await context.SaveChangesAsync();
    // affectedRows — nechta qator o'zgarganini qaytaradi
}
```

**Xatolik bo'lsa nima bo'ladi?** Agar `SaveChangesAsync` da xatolik yuz bersa (masalan, unique constraint buzilsa), barcha o'zgarishlar **rollback** qilinadi — ya'ni hech narsa saqlanmaydi.

---

### 2.4 Code First vs Database First

| | Code First | Database First |
|---|---|---|
| **Jarayon** | C# → Migration → DB | DB → Scaffold → C# |
| **Boshlanishi** | Entity class yozish | Ma'lumotlar bazasi yaratish |
| **Migration** | `dotnet ef migrations add` | Yo'q (scaffold qiladi) |
| **Qachon ishlatiladi** | Yangi loyihalar | Mavjud DB bilan ishlash |

```bash
# Code First — migration yaratish va qo'llash
dotnet ef migrations add AddCategoryTable
dotnet ef database update

# Database First — mavjud DB dan class yaratish (scaffold)
dotnet ef dbcontext scaffold "Server=.;Database=MyDb;Trusted_Connection=true" \
    Microsoft.EntityFrameworkCore.SqlServer \
    --output-dir Models \
    --context AppDbContext
```

---

### 2.5 LINQ — asosiy operatorlar chuqurroq

```csharp
public class ProductQueryService(AppDbContext context)
{
    // Where — filterlash
    public async Task<List<Product>> GetByPriceRangeAsync(decimal min, decimal max)
    {
        return await context.Products
            .Where(p => p.Price >= min && p.Price <= max)
            .ToListAsync();
    }

    // FirstOrDefault — birinchi yoki null
    public async Task<Product?> GetByNameAsync(string name)
    {
        return await context.Products
            .FirstOrDefaultAsync(p => p.Name == name);
    }

    // Single — faqat bitta bo'lishi kerak (aks holda exception)
    public async Task<Product> GetExactlyOneAsync(int id)
    {
        return await context.Products
            .SingleAsync(p => p.Id == id);
        // Agar 0 ta yoki 2+ ta topilsa — InvalidOperationException!
    }

    // Select — faqat kerakli ustunlarni olish (Projection)
    public async Task<List<string>> GetProductNamesAsync()
    {
        return await context.Products
            .Select(p => p.Name)
            .ToListAsync();
        // SQL: SELECT [p].[Name] FROM [Products] AS [p]
        // Barcha ustunlarni emas, faqat Name ni oladi — samaraliroq!
    }

    // Skip/Take — sahifalash (pagination)
    public async Task<List<Product>> GetPagedAsync(int page, int pageSize)
    {
        return await context.Products
            .OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
```

---

### 2.6 Migration nima va qanday yaratiladi

**Migration** — ma'lumotlar bazasi sxemasini (jadvallar, ustunlar, indekslar) versiya boshqaruvi (version control) bilan boshqarish tizimi.

```bash
# 1. Dastlabki migration
dotnet ef migrations add InitialCreate

# 2. Ma'lumotlar bazasiga qo'llash
dotnet ef database update

# 3. Yangi entity qo'shilganda
dotnet ef migrations add AddOrderTable

# 4. Migrationni bekor qilish (oxirgisini o'chirish)
dotnet ef migrations remove

# 5. Ma'lumotlar bazasini ma'lum migrationgacha qaytarish
dotnet ef database update InitialCreate

# 6. SQL skriptini ko'rish (production uchun)
dotnet ef migrations script
```

**Migration fayllarining tuzilishi:**

```csharp
// Migrations/20240101120000_AddOrderTable.cs
public partial class AddOrderTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ProductId = table.Column<int>(nullable: false),
                Quantity = table.Column<int>(nullable: false),
                OrderDate = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Orders");
    }
}
```

---

### 2.7 Relationships — bog'lanishlar asoslari

EF Core da jadvallar o'rtasidagi munosabatlar **Navigation Property** lar orqali ifodalanadi.

```csharp
// One-to-Many: Category → Products
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Navigation property — bu kategoriyaga tegishli barcha mahsulotlar
    public ICollection<Product> Products { get; set; } = [];
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Foreign Key
    public int CategoryId { get; set; }

    // Navigation property — mahsulot qaysi kategoriyaga tegishli
    public Category Category { get; set; } = null!;
}
```

**Buni ishlatish:**

```csharp
// Yangi kategoriya bilan mahsulotlar qo'shish
var category = new Category
{
    Name = "Electronics",
    Products =
    [
        new Product { Name = "Laptop", Price = 5000 },
        new Product { Name = "Phone", Price = 3000 }
    ]
};

context.Categories.Add(category);
await context.SaveChangesAsync();
// EF Core avtomatik CategoryId ni to'ldiradi
```

**EF Core conventions (kelishuv qoidalari):** Agar property nomi `[EntityName]Id` formatida bo'lsa (masalan `CategoryId`), EF Core buni avtomatik foreign key sifatida taniydi.

---

## 2. O'rganish metodi

### Topshiriq 1: "Onlayn Do'kon" loyihasini kengaytirish
Level 1 dagi `Product` entity siga quyidagilarni qo'shing:
- `Category` entity yarating va `Product` → `Category` (Many-to-One) munosabatini o'rnating
- Migration yarating va ma'lumotlar bazasini yangilang
- 3 ta kategoriya va har biriga 5 ta mahsulot qo'shish uchun Seed data yozing
- `ExecuteUpdateAsync` bilan barcha "Electronics" kategoriyasidagi mahsulotlar narxini 10% ga oshiring

### Topshiriq 2: LINQ amaliyoti
Quyidagi so'rovlarni yozing:
- Eng qimmat 5 ta mahsulotni oling
- Har bir kategoriya bo'yicha mahsulotlar sonini hisoblang (GroupBy)
- Sahifalash (pagination) bilan mahsulotlar ro'yxatini qaytaring (page=2, pageSize=10)
- Faqat `Name` va `Price` ni qaytaruvchi projection yozing

### Topshiriq 3: Migration bilan ishlash
- Yangi entity qo'shing: `Order` (Id, ProductId, Quantity, OrderDate)
- Migration yarating, keyin uni `remove` bilan bekor qiling
- Qayta yarating va `database update` qiling
- `dotnet ef migrations script` bilan SQL skriptini ko'ring

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] `AddRange` bilan bir nechta obyekt qo'sha olaman
- [ ] `ExecuteUpdateAsync` / `ExecuteDeleteAsync` ni ishlataman
- [ ] `SaveChangesAsync` nima qilishini tushunaman (transaction, rollback)
- [ ] Code First va Database First farqini bilaman
- [ ] LINQ: `Select`, `Skip/Take`, `SingleAsync` ishlataman
- [ ] Migration yaratish, qo'llash, bekor qilish va SQL skript olish bilaman
- [ ] One-to-Many relationship yarata olaman (Navigation Property + Foreign Key)

---

## 3. Solishtirish jadvali: Code First vs Database First

| Mezon | Code First | Database First |
|---|---|---|
| **Boshlash nuqtasi** | C# entity classlar | Mavjud ma'lumotlar bazasi |
| **Sxema boshqaruvi** | Migration lar orqali | DB da qo'lda yoki skriptlar |
| **Versiya nazorati** | ✅ Migration fayllar Git da | ⚠️ DB skriptlarini alohida saqlash kerak |
| **CI/CD integratsiya** | ✅ Oson — `dotnet ef database update` | ⚠️ Alohida skript ishga tushirish kerak |
| **Yangilash** | Entity o'zgartir → migration yarat | DB o'zgartir → scaffold qayta ishlat |
| **Afzalligi** | To'liq nazorat, toza arxitektura | Murakkab mavjud DB bilan ishlash oson |
| **Kamchiligi** | Mavjud DB ga qo'llash qiyin | Model va DB sinxronlash muammolari |
| **Ishlatish holati** | Yangi loyihalar, microservicelar | Legacy tizimlar, DBA boshqaradigan DB |
| **Buyruq** | `dotnet ef migrations add` | `dotnet ef dbcontext scaffold` |

---

## 4. Test

### Savollar

**1.** `ExecuteDeleteAsync` ning oddiy `Remove` + `SaveChanges` dan farqi nimada?
- a) Farqi yo'q, ikkalasi bir xil
- b) `ExecuteDeleteAsync` obyektni oldindan yuklamasdan to'g'ridan-to'g'ri o'chiradi
- c) `ExecuteDeleteAsync` faqat bitta yozuvni o'chiradi
- d) `Remove` tezroq ishlaydi

**2.** Quyidagi kod nima qiladi?
```csharp
await context.Products
    .Where(p => p.Price < 50)
    .ExecuteUpdateAsync(s =>
        s.SetProperty(p => p.Price, p => p.Price * 2));
```

**3.** `SaveChangesAsync()` chaqirilganda quyidagi holatlardan qaysi biri to'g'ri?
- a) Har bir `Add`, `Remove` uchun alohida transaction ochiladi
- b) Barcha o'zgarishlar bitta transaction ichida saqlanadi
- c) Faqat oxirgi o'zgarish saqlanadi
- d) Transaction ishlatilmaydi

**4.** Database First yondashuvida mavjud bazadan C# modellarni yaratish buyrug'i qaysi?
- a) `dotnet ef migrations add`
- b) `dotnet ef dbcontext scaffold`
- c) `dotnet ef database update`
- d) `dotnet ef model generate`

**5.** Quyidagi kodning SQL ekvivalenti nima?
```csharp
var result = await context.Products
    .Where(p => p.CategoryId == 3)
    .Select(p => new { p.Name, p.Price })
    .OrderByDescending(p => p.Price)
    .Take(5)
    .ToListAsync();
```

**6.** Navigation Property va Foreign Key orasidagi farq nimada?

**7.** Quyidagi kodda necha ta SQL so'rov generatsiya bo'ladi?
```csharp
context.Products.Add(new Product { Name = "A", Price = 100, CategoryId = 1 });
context.Products.Add(new Product { Name = "B", Price = 200, CategoryId = 1 });
context.Products.Add(new Product { Name = "C", Price = 300, CategoryId = 2 });
await context.SaveChangesAsync();
```
- a) 3 ta INSERT so'rov
- b) 1 ta batch INSERT so'rov
- c) EF Core provider ga bog'liq (SQL Server da batch, SQLite da alohida)
- d) 0 ta — Add faqat xotirada ishlaydi

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Natijalar **taxminiy/indikativ**, BenchmarkDotNet, .NET 8, SQL Server 2022, 1000 ta yozuv. Aniq natijalar muhitga bog'liq.

### DELETE — 1000 ta yozuvni o'chirish

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (KB) | Izoh |
|---|---|---|---|
| `Remove` + `SaveChanges` (bitta-bitta) | ~850 | ~2000 | Har bir yozuvni oldindan yuklash kerak |
| `RemoveRange` + `SaveChanges` | ~120 | ~1800 | Batch o'chirish, lekin yuklash kerak |
| `ExecuteDeleteAsync` | ~5 | ~10 | ⚡ To'g'ridan-to'g'ri SQL, eng tez |

### UPDATE — 1000 ta yozuvni yangilash

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (KB) | Izoh |
|---|---|---|---|
| FindAsync + property o'zgartirish (loop) | ~900 | ~2200 | Change Tracking ishlaydi |
| `ExecuteUpdateAsync` | ~4 | ~10 | ⚡ To'g'ridan-to'g'ri SQL |

### SELECT — Projection vs Full entity

| Operatsiya | O'rtacha vaqt (ms) | Xotira sarfi (KB) | Izoh |
|---|---|---|---|
| `ToListAsync()` (barcha ustunlar) | ~3.5 | ~450 | Barcha property lar yuklanadi |
| `Select(p => new { p.Name, p.Price })` | ~1.8 | ~180 | Faqat kerakli ustunlar — 2x tezroq |

### O'lchash usuli
```csharp
[MemoryDiagnoser]
public class CrudBenchmark
{
    [Benchmark]
    public async Task DeleteTraditional()
    {
        var products = await _context.Products.Take(1000).ToListAsync();
        _context.Products.RemoveRange(products);
        await _context.SaveChangesAsync();
    }

    [Benchmark]
    public async Task DeleteBulk()
    {
        await _context.Products
            .Where(p => p.Id <= 1000)
            .ExecuteDeleteAsync();
    }
}
```
