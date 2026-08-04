using System.Data.Common;
using System.Text.Json;
using EfCoreMastery.Domain.Common;
using EfCoreMastery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreMastery.Infrastructure.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        // Audit automatic properties (CreatedAt / UpdatedAt)
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                entry.Property(x => x.CreatedAt).IsModified = false;
            }
        }

        // Audit Log for changes
        var auditLogs = new List<AuditLog>();
        foreach (var entry in context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var changes = entry.Properties
                .Where(p => entry.State == EntityState.Added || p.IsModified)
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString() ?? "null");

            auditLogs.Add(new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Changes = JsonSerializer.Serialize(changes),
                Timestamp = DateTime.UtcNow
            });
        }

        if (auditLogs.Count > 0 && context is Data.AppDbContext dbContext)
        {
            dbContext.AuditLogs.AddRange(auditLogs);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        foreach (var entry in eventData.Context.ChangeTracker.Entries<ISoftDelete>()
            .Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
            entry.Entity.DeletedBy = "system";
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

public class QueryLoggingInterceptor : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SQL QUERY]: {command.CommandText}");
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
