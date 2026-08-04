using EFCore.BulkExtensions;
using EfCoreMastery.Application.Interfaces;
using EfCoreMastery.Domain.Entities;
using EfCoreMastery.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMastery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Level06AdvancedQueryController(AppDbContext context) : ControllerBase
{
    // Split Query
    [HttpGet("orders-split")]
    public async Task<IActionResult> GetOrdersSplit()
    {
        var orders = await context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();

        return Ok(orders);
    }

    // Ignore Query Filter (Include Soft Deleted)
    [HttpGet("all-products-including-deleted")]
    public async Task<IActionResult> GetAllIncludingDeleted()
    {
        var products = await context.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync();

        return Ok(products);
    }
}

[ApiController]
[Route("api/[controller]")]
public class Level07ArchitectureController(IUnitOfWork unitOfWork) : ControllerBase
{
    // Repository & UnitOfWork demonstration
    [HttpPost("create-product-uow")]
    public async Task<IActionResult> CreateProductWithUow([FromBody] Product product)
    {
        await unitOfWork.BeginTransactionAsync();
        try
        {
            await unitOfWork.Products.AddAsync(product);
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();
            return Ok(product);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class Level08ScaleController(AppDbContext context) : ControllerBase
{
    // Bulk Extensions Insert
    [HttpPost("bulk-insert-products")]
    public async Task<IActionResult> BulkInsertProducts([FromQuery] int count = 1000)
    {
        var products = Enumerable.Range(1, count).Select(i => new Product
        {
            Name = $"Bulk Product {i}",
            SKU = $"SKU-BULK-{Guid.NewGuid():N}",
            Price = i * 10,
            Stock = 100,
            CategoryId = 1,
            TenantId = 1
        }).ToList();

        await context.BulkInsertAsync(products);
        return Ok(new { Message = $"{count} products inserted in bulk" });
    }
}

[ApiController]
[Route("api/[controller]")]
public class Level09EnterpriseController(AppDbContext context) : ControllerBase
{
    // TPH Inheritance Polymorphic Query
    [HttpGet("payments")]
    public async Task<IActionResult> GetAllPayments()
    {
        var payments = await context.Payments.AsNoTracking().ToListAsync();
        return Ok(payments);
    }

    [HttpPost("payment/creditcard")]
    public async Task<IActionResult> AddCreditCardPayment([FromBody] CreditCardPayment payment)
    {
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return Ok(payment);
    }

    // Concurrency Handling Example
    [HttpPut("update-stock-concurrency")]
    public async Task<IActionResult> UpdateStockConcurrency([FromQuery] int productId, [FromQuery] int change)
    {
        try
        {
            var product = await context.Products.FindAsync(productId);
            if (product is null) return NotFound();

            product.Stock += change;
            await context.SaveChangesAsync();
            return Ok(product);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { Message = "Concurrency conflict detected. Another process modified the data.", Exception = ex.Message });
        }
    }
}
