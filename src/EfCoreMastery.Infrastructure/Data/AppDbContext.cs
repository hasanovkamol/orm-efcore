using System.Linq.Expressions;
using EfCoreMastery.Application.Interfaces;
using EfCoreMastery.Domain.Common;
using EfCoreMastery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMastery.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantService? _tenantService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantService? tenantService = null)
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<StudentCourse> StudentCourses => Set<StudentCourse>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CreditCardPayment> CreditCardPayments => Set<CreditCardPayment>();
    public DbSet<BankTransferPayment> BankTransferPayments => Set<BankTransferPayment>();
    public DbSet<CashPayment> CashPayments => Set<CashPayment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global Query Filters (Soft Delete & Multi-Tenancy)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            Expression? filter = null;

            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                var notDeletedCondition = Expression.Equal(isDeletedProperty, Expression.Constant(false));
                filter = notDeletedCondition;
            }

            if (typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType) && _tenantService is not null)
            {
                var tenantIdProperty = Expression.Property(parameter, nameof(IMultiTenant.TenantId));
                var tenantCondition = Expression.Equal(tenantIdProperty, Expression.Constant(_tenantService.GetCurrentTenantId()));

                filter = filter is null ? tenantCondition : Expression.AndAlso(filter, tenantCondition);
            }

            if (filter is not null)
            {
                var lambda = Expression.Lambda(filter, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
