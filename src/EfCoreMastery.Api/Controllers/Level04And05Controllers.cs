using EfCoreMastery.Domain.Entities;
using EfCoreMastery.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EfCoreMastery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Level04AdvancedController(AppDbContext context) : ControllerBase
{
    // Transaction & Savepoint Demonstration
    [HttpPost("create-order-transaction")]
    public async Task<IActionResult> CreateOrderWithTransaction([FromQuery] int productId, [FromQuery] int quantity)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var product = await context.Products.FindAsync(productId);
            if (product is null || product.Stock < quantity)
            {
                return BadRequest("Insufficient stock or product not found.");
            }

            product.Stock -= quantity;
            await context.SaveChangesAsync();

            await transaction.CreateSavepointAsync("StockUpdated");

            var order = new Order
            {
                OrderDate = DateTime.UtcNow,
                TotalAmount = product.Price * quantity
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(new { Message = "Order created successfully", OrderId = order.Id });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Many-to-Many Enrollment
    [HttpPost("enroll-student")]
    public async Task<IActionResult> EnrollStudent([FromQuery] int studentId, [FromQuery] int courseId)
    {
        var enrollment = new StudentCourse
        {
            StudentId = studentId,
            CourseId = courseId,
            EnrolledAt = DateTime.UtcNow
        };
        context.StudentCourses.Add(enrollment);
        await context.SaveChangesAsync();
        return Ok(enrollment);
    }
}

[ApiController]
[Route("api/[controller]")]
public class Level05PerformanceController(AppDbContext context) : ControllerBase
{
    // AsNoTracking vs Tracking Inspection
    [HttpGet("changetracker-status")]
    public async Task<IActionResult> InspectChangeTracker()
    {
        await context.Products.Take(5).ToListAsync(); // Tracked
        var entries = context.ChangeTracker.Entries()
            .Select(e => new
            {
                Entity = e.Entity.GetType().Name,
                State = e.State.ToString()
            });

        return Ok(entries);
    }
}
