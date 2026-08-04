using EfCoreMastery.Application.Interfaces;
using EfCoreMastery.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMastery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Level02CrudController(AppDbContext context) : ControllerBase
{
    // EF Core 8: ExecuteUpdateAsync
    [HttpPut("increase-prices")]
    public async Task<IActionResult> IncreasePrices([FromQuery] decimal percentage = 10)
    {
        var updatedRows = await context.Products
            .Where(p => p.Price < 500)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, p => p.Price * (1 + percentage / 100)));

        return Ok(new { AffectedRows = updatedRows });
    }

    // EF Core 8: ExecuteDeleteAsync
    [HttpDelete("cleanup-cheap")]
    public async Task<IActionResult> DeleteCheapProducts([FromQuery] decimal maxPrice = 10)
    {
        var deletedRows = await context.Products
            .Where(p => p.Price <= maxPrice)
            .ExecuteDeleteAsync();

        return Ok(new { DeletedRows = deletedRows });
    }
}

[ApiController]
[Route("api/[controller]")]
public class Level03QueriesController(AppDbContext context) : ControllerBase
{
    // Eager Loading with Include
    [HttpGet("categories-with-products")]
    public async Task<IActionResult> GetCategoriesWithProducts()
    {
        var categories = await context.Categories
            .Include(c => c.Products)
            .AsNoTracking()
            .ToListAsync();
        return Ok(categories);
    }

    // Server-side GroupBy & Projection
    [HttpGet("category-summary")]
    public async Task<IActionResult> GetCategorySummary()
    {
        var summary = await context.Products
            .GroupBy(p => p.Category.Name)
            .Select(g => new CategorySummaryDto
            {
                CategoryName = g.Key,
                ProductCount = g.Count(),
                AveragePrice = g.Average(p => p.Price)
            })
            .ToListAsync();
        return Ok(summary);
    }
}
