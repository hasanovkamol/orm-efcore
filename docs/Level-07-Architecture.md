# Level 7 — Senior (Arxitektura patternlari)

---

## 1. Darslik

### 7.1 Repository Pattern va Unit of Work

**Repository Pattern** — ma'lumotlar bazasi bilan ishlash logikasini abstraktsiya qilib, business logikadan ajratish. **Unit of Work** — bir nechta repository dagi o'zgarishlarni bitta tranzaksiyada saqlash.

```csharp
// Generic Repository interface
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}

// Generic Repository implementatsiya
public class Repository<T>(AppDbContext context) : IRepository<T> where T : class
{
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);

    public async Task<List<T>> GetAllAsync() =>
        await _dbSet.AsNoTracking().ToListAsync();

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        await _dbSet.AsNoTracking().Where(predicate).ToListAsync();

    public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);

    public void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);
}
```

```csharp
// Unit of Work interface
public interface IUnitOfWork : IDisposable
{
    IRepository<Product> Products { get; }
    IRepository<Category> Categories { get; }
    IRepository<Order> Orders { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}

// Unit of Work implementatsiya
public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    private IRepository<Product>? _products;
    private IRepository<Category>? _categories;
    private IRepository<Order>? _orders;

    public IRepository<Product> Products =>
        _products ??= new Repository<Product>(context);
    public IRepository<Category> Categories =>
        _categories ??= new Repository<Category>(context);
    public IRepository<Order> Orders =>
        _orders ??= new Repository<Order>(context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync() =>
        _transaction = await context.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync()
    {
        if (_transaction is not null)
            await _transaction.CommitAsync();
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction is not null)
            await _transaction.RollbackAsync();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        context.Dispose();
    }
}
```

```csharp
// DI registratsiya
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Servisda ishlatish
public class OrderProcessingService(IUnitOfWork unitOfWork)
{
    public async Task CreateOrderAsync(int productId, int quantity)
    {
        await unitOfWork.BeginTransactionAsync();

        try
        {
            var product = await unitOfWork.Products.GetByIdAsync(productId)
                ?? throw new InvalidOperationException("Product not found");

            var order = new Order
            {
                ProductId = productId,
                Quantity = quantity,
                TotalPrice = product.Price * quantity,
                OrderDate = DateTime.UtcNow
            };

            await unitOfWork.Orders.AddAsync(order);
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
```

**⚠️ Repository Pattern haqida munozara:**

EF Core ning `DbContext` o'zi aslida Repository + Unit of Work patternini amalga oshiradi. Ko'p dasturchilar "repository ortiqcha abstraktsiya" deb hisoblashadi. Lekin u quyidagi holatlarda foydali:
- Unit testing da mock qilish oson
- Ma'lumot manbasini almashtirishni rejalashtirish (EF → Dapper)
- Complex business logikani izolyatsiya qilish

---

### 7.2 DDD (Domain-Driven Design) va EF Core

**DDD** — murakkab business logikani domain modeli atrofida tashkil etish yondashuvi.

```csharp
// Domain Entity — business logika ichida
public class Order
{
    public int Id { get; private set; }
    public DateTime OrderDate { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }

    // Navigation — private set
    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // Factory method — yaratish logikasi
    public static Order Create(int customerId)
    {
        return new Order
        {
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Draft,
            CustomerId = customerId
        };
    }

    // Business logic — entity ichida
    public void AddItem(int productId, decimal price, int quantity)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Cannot modify confirmed order");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
        }
        else
        {
            _items.Add(new OrderItem(productId, price, quantity));
        }

        RecalculateTotal();
    }

    public void Confirm()
    {
        if (!_items.Any())
            throw new InvalidOperationException("Cannot confirm empty order");

        Status = OrderStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot cancel shipped order");

        Status = OrderStatus.Cancelled;
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items.Sum(i => i.Price * i.Quantity);
    }

    public int CustomerId { get; private set; }
}

public enum OrderStatus { Draft, Confirmed, Shipped, Delivered, Cancelled }
```

```csharp
// EF Core konfiguratsiya — DDD entity uchun
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .HasConversion<string>() // Enum ni string sifatida saqlash
            .HasMaxLength(50);

        builder.Property(o => o.TotalAmount)
            .HasColumnType("decimal(18,2)");

        // Private collection ni map qilish
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId");

        // Backing field ni ishlatish
        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

---

### 7.3 Clean Architecture bilan integratsiya

```
Solution/
├── Domain/                    # Entity, Value Object, Interface
│   ├── Entities/
│   ├── ValueObjects/
│   └── Interfaces/
│       ├── IRepository.cs
│       └── IUnitOfWork.cs
├── Application/               # Use Cases, DTOs, Mapping
│   ├── DTOs/
│   ├── Services/
│   └── Interfaces/
│       └── IOrderService.cs
├── Infrastructure/            # EF Core, DB konfiguratsiya
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Configurations/
│   ├── Repositories/
│   └── DependencyInjection.cs
└── WebAPI/                    # Controllers, Program.cs
    ├── Controllers/
    └── Program.cs
```

```csharp
// Domain Layer — hech qanday EF Core reference yo'q!
// Domain/Interfaces/IProductRepository.cs
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<List<Product>> GetByCategoryAsync(int categoryId);
    Task AddAsync(Product product);
}

// Infrastructure Layer — EF Core faqat shu yerda
// Infrastructure/Repositories/ProductRepository.cs
public class ProductRepository(AppDbContext context) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(int id) =>
        await context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<Product>> GetByCategoryAsync(int categoryId) =>
        await context.Products
            .Where(p => p.CategoryId == categoryId)
            .AsNoTracking()
            .ToListAsync();

    public async Task AddAsync(Product product) =>
        await context.Products.AddAsync(product);
}

// Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
```

---

### 7.4 CQRS Pattern va EF Core

**CQRS (Command Query Responsibility Segregation)** — o'qish va yozish operatsiyalarini ajratish.

```csharp
// COMMAND — yozish uchun (Change Tracking bilan)
public interface ICommandRepository<T> where T : class
{
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}

// QUERY — o'qish uchun (AsNoTracking)
public interface IQueryRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    IQueryable<T> Query(); // Flexible LINQ
}

// Query repository — faqat o'qish (AsNoTracking default)
public class ProductQueryRepository(AppDbContext context) : IQueryRepository<Product>
{
    public async Task<Product?> GetByIdAsync(int id) =>
        await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<Product>> GetAllAsync() =>
        await context.Products.AsNoTracking().ToListAsync();

    public IQueryable<Product> Query() =>
        context.Products.AsNoTracking();
}

// Command repository — yozish (tracking bilan)
public class ProductCommandRepository(AppDbContext context) : ICommandRepository<Product>
{
    public async Task AddAsync(Product entity) =>
        await context.Products.AddAsync(entity);

    public void Update(Product entity) =>
        context.Products.Update(entity);

    public void Remove(Product entity) =>
        context.Products.Remove(entity);
}
```

**CQRS bilan alohida Read/Write DbContext:**

```csharp
// Write DbContext — to'liq (tracking, migrations)
public class WriteDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    // ... boshqa DbSetlar

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WriteDbContext).Assembly);
    }
}

// Read DbContext — faqat o'qish (NoTracking, read replica)
public class ReadDbContext : DbContext
{
    public IQueryable<Product> Products => Set<Product>().AsNoTracking();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // Read replica connection string
    }
}

// DI
builder.Services.AddDbContext<WriteDbContext>(o =>
    o.UseSqlServer(config.GetConnectionString("Write")));
builder.Services.AddDbContext<ReadDbContext>(o =>
    o.UseSqlServer(config.GetConnectionString("ReadReplica"))
     .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
```

---

### 7.5 NoSQL vs ORM farqlari

ORM asosan relational (jadval) ma'lumotlar bazalari uchun mo'ljallangan. NoSQL uchun boshqa yondashuvlar kerak.

*(Solishtirish jadvali bo'limda berilgan)*

---

### 7.6 Transaction Monitoring va Audit

```csharp
// Interceptor — barcha SQL so'rovlarni kuzatish
public class QueryLoggingInterceptor : DbCommandInterceptor
{
    private readonly ILogger<QueryLoggingInterceptor> _logger;

    public QueryLoggingInterceptor(ILogger<QueryLoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing query: {CommandText}\nParameters: {Parameters}",
            command.CommandText,
            string.Join(", ", command.Parameters.Cast<DbParameter>()
                .Select(p => $"{p.ParameterName}={p.Value}")));

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

// SaveChanges Interceptor — o'zgarishlarni audit qilish
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = new List<AuditEntry>();

        foreach (var entry in context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var auditEntry = new AuditEntry
            {
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                Changes = entry.Properties
                    .Where(p => entry.State == EntityState.Added || p.IsModified)
                    .ToDictionary(
                        p => p.Metadata.Name,
                        p => new { Old = p.OriginalValue, New = p.CurrentValue })
            };
            auditEntries.Add(auditEntry);
        }

        // Audit larni saqlash (alohida jadvalga yoki log ga)
        if (auditEntries.Count > 0 && context is AppDbContext appContext)
        {
            foreach (var audit in auditEntries)
            {
                appContext.AuditLogs.Add(new AuditLog
                {
                    EntityName = audit.EntityName,
                    Action = audit.Action,
                    Changes = JsonSerializer.Serialize(audit.Changes),
                    Timestamp = audit.Timestamp
                });
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

// Ro'yxatdan o'tkazish
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
        .AddInterceptors(
            sp.GetRequiredService<QueryLoggingInterceptor>(),
            sp.GetRequiredService<AuditSaveChangesInterceptor>()));
```

---

## 2. O'rganish metodi

### Topshiriq 1: Repository + Unit of Work
- Generic `IRepository<T>` va `IUnitOfWork` yarating
- `ProductRepository` va `OrderRepository` implementatsiya qiling
- `OrderService` da Unit of Work orqali buyurtma yaratish va mahsulot zaxirasini kamaytirish

### Topshiriq 2: Clean Architecture loyiha
- 4 qatlamli (Domain, Application, Infrastructure, WebAPI) loyiha yarating
- Domain da entity va interfacelar, Infrastructure da EF Core
- Application da servislar — faqat interfacelar orqali ishlash

### Topshiriq 3: CQRS + Audit
- Read/Write repositorylarni ajrating
- `SaveChangesInterceptor` bilan audit log tizimini yarating
- Barcha o'zgarishlar `AuditLogs` jadvaliga yozilsin

### ✅ O'zini-o'zi tekshirish checklisti
- [ ] Repository Pattern va Unit of Work ni tushunaman va yarata olaman
- [ ] DDD asoslari — entity, value object, factory method ishlataman
- [ ] Clean Architecture qatlamlarini bilaman va EF Core ni faqat Infrastructure da ishlataman
- [ ] CQRS pattern bilan read/write ajratishni bilaman
- [ ] `DbCommandInterceptor` va `SaveChangesInterceptor` yarata olaman
- [ ] NoSQL va Relational DB farqlarini tushunaman

---

## 3. Solishtirish jadvali: Repository Pattern vs to'g'ridan-to'g'ri DbContext

| Mezon | Repository + UoW | To'g'ridan-to'g'ri DbContext |
|---|---|---|
| **Abstraktsiya** | ✅ Yuqori — interface orqali | ❌ Past — DbContext ga bog'liq |
| **Unit Testing** | ✅ Mock qilish oson | ⚠️ InMemory provider kerak |
| **Kod hajmi** | ⚠️ Ko'proq (interface + impl) | ✅ Kamroq |
| **Flexibility** | ✅ Data source almashtiriladi | ❌ EF Core ga qattiq bog'liq |
| **IQueryable expose** | ⚠️ Leaky abstraction xavfi | ✅ LINQ to'liq ishlatiladi |
| **CQRS bilan** | ✅ Ajratish oson | ⚠️ Qiyinroq |
| **Kichik loyiha** | ❌ Over-engineering | ✅ Tez va sodda |
| **Katta loyiha** | ✅ Tavsiya etiladi | ⚠️ Murakkablashadi |
| **DDD bilan** | ✅ Mos keladi | ⚠️ Domain → Infra bog'liqligi |
| **Yangi dasturchi uchun** | ⚠️ Tushunish qiyin | ✅ Oddiy |

---

## 4. Test

### Savollar

**1.** Unit of Work ning asosiy vazifasi nimada?
- a) SQL yozish
- b) Bir nechta repository o'zgarishlarini bitta tranzaksiyada saqlash
- c) Ma'lumotlar bazasi yaratish
- d) Migration qilish

**2.** Clean Architecture da EF Core qaysi qatlamda bo'lishi kerak?
- a) Domain
- b) Application
- c) Infrastructure
- d) Presentation

**3.** DDD da entity ning propertylariga `private set` qo'yishning sababi nima?

**4.** CQRS pattern da Read va Write operatsiyalarni ajratishning asosiy foydasi nimada?

**5.** Quyidagi kod qaysi pattern ni amalga oshiradi?
```csharp
public class WriteDbContext : DbContext { /* tracking bor */ }
public class ReadDbContext : DbContext { /* AsNoTracking */ }
```
- a) Repository Pattern
- b) CQRS
- c) Unit of Work
- d) Singleton

**6.** `SaveChangesInterceptor` nima uchun ishlatiladi?
- a) SQL so'rovlarni cache qilish
- b) SaveChanges dan oldin/keyin qo'shimcha logika bajarish (audit, validation)
- c) Ma'lumotlar bazasini yaratish
- d) Connection pooling

**7.** Repository Pattern ga qarshi eng kuchli argument nimada?

---

## 5. Benchmark natijalari

> ⚠️ **Eslatma:** Natijalar **taxminiy/indikativ**, BenchmarkDotNet, .NET 8, SQL Server 2022.

### Repository Pattern overhead — 1000 ta CRUD operatsiya

| Operatsiya | Direct DbContext (ms) | Repository + UoW (ms) | Overhead |
|---|---|---|---|
| AddAsync + SaveChanges | ~200 | ~205 | +2.5% — deyarli farq yo'q |
| GetByIdAsync | ~50 | ~52 | +4% |
| Complex query (LINQ) | ~30 | ~35 | +16% (IQueryable wrap) |

### CQRS — Read vs Write ajratish ta'siri

| Operatsiya | Bitta DbContext (ms) | Alohida Read/Write (ms) | Izoh |
|---|---|---|---|
| SELECT 10K (read heavy) | ~80 | ~60 | ⚡ NoTracking default — 25% tez |
| INSERT 1K (write heavy) | ~300 | ~300 | Farq yo'q |
| Mixed (70% read, 30% write) | ~150 | ~110 | ⚡ 27% yaxshilanish |

### Interceptor overhead — 1000 ta SaveChanges

| Operatsiya | Interceptorsiz (ms) | Audit Interceptor bilan (ms) | Izoh |
|---|---|---|---|
| SaveChanges (1 entity) | ~3 | ~5 | +2ms audit yozish |
| SaveChanges (50 entity) | ~15 | ~25 | +10ms — ko'p property tekshirish |
