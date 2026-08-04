using EfCoreMastery.Application.Interfaces;
using EfCoreMastery.Domain.Entities;
using EfCoreMastery.Infrastructure.Data;
using EfCoreMastery.Infrastructure.Interceptors;
using EfCoreMastery.Infrastructure.Repositories;
using EfCoreMastery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Controllers & Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Interceptors
builder.Services.AddSingleton<AuditSaveChangesInterceptor>();
builder.Services.AddSingleton<SoftDeleteInterceptor>();
builder.Services.AddSingleton<QueryLoggingInterceptor>();

// Services & Repositories (ITenantService is Singleton to work with IDbContextFactory)
builder.Services.AddSingleton<ITenantService, MockTenantService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// DbContext & DbContextFactory configuration
builder.Services.AddDbContextFactory<AppDbContext>((sp, options) =>
{
    var auditInterceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
    var softDeleteInterceptor = sp.GetRequiredService<SoftDeleteInterceptor>();
    var queryLoggingInterceptor = sp.GetRequiredService<QueryLoggingInterceptor>();

    options.UseInMemoryDatabase("EfCoreMasteryDb")
           .AddInterceptors(auditInterceptor, softDeleteInterceptor, queryLoggingInterceptor);
});

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var auditInterceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
    var softDeleteInterceptor = sp.GetRequiredService<SoftDeleteInterceptor>();
    var queryLoggingInterceptor = sp.GetRequiredService<QueryLoggingInterceptor>();

    options.UseInMemoryDatabase("EfCoreMasteryDb")
           .AddInterceptors(auditInterceptor, softDeleteInterceptor, queryLoggingInterceptor);
});

var app = builder.Build();

// Use CORS
app.UseCors("AllowAll");

// Enable Static Files (for Angular Standalone Dashboard UI)
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EF Core Mastery API Level 1-9 v1");
});

app.UseAuthorization();
app.MapControllers();

// Seed initial data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Categories.Any())
    {
        var category = new Category { Name = "Electronics", Description = "Electronic devices" };
        db.Categories.Add(category);
        db.SaveChanges();

        db.Products.AddRange(
            new Product { Name = "Laptop Pro", SKU = "SKU-001", Price = 1500, Stock = 50, CategoryId = category.Id, TenantId = 1 },
            new Product { Name = "Smartphone X", SKU = "SKU-002", Price = 990, Stock = 100, CategoryId = category.Id, TenantId = 1 }
        );
        db.SaveChanges();
    }
}

app.Run();
