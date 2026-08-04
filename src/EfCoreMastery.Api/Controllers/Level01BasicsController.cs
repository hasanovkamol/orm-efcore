using EfCoreMastery.Domain.Entities;
using EfCoreMastery.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMastery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Level01BasicsController(AppDbContext context) : ControllerBase
{
    // EF Core CRUD
    [HttpGet("products")]
    public async Task<IActionResult> GetAllProducts()
    {
        var products = await context.Products.ToListAsync();
        return Ok(products);
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAllProducts), new { id = product.Id }, product);
    }

    // LINQ basic filtering
    [HttpGet("products/expensive")]
    public async Task<IActionResult> GetExpensiveProducts([FromQuery] decimal minPrice = 100)
    {
        var products = await context.Products
            .Where(p => p.Price > minPrice)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return Ok(products);
    }
}
