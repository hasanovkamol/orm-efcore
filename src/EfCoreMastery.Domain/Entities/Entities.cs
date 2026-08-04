using System.ComponentModel.DataAnnotations;
using EfCoreMastery.Domain.Common;
using EfCoreMastery.Domain.ValueObjects;

namespace EfCoreMastery.Domain.Entities;

public class Category : IAuditable, ISoftDelete
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public ICollection<Product> Products { get; set; } = [];

    // Audit & Soft Delete
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public class Product : IAuditable, ISoftDelete, IMultiTenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastChecked { get; set; }

    // Foreign Key & Navigation
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Complex Type Value Object
    public Money PriceDetails { get; set; } = new();

    // Concurrency Token (Level 9)
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    // Interfaces
    public int TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public class Student
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public ICollection<StudentCourse> StudentCourses { get; set; } = [];
}

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<StudentCourse> StudentCourses { get; set; } = [];
}

public class StudentCourse
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public DateTime EnrolledAt { get; set; }
    public decimal? Grade { get; set; }
}

public class Order : IAuditable, IMultiTenant
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";

    public Address ShippingAddress { get; set; } = new();
    public ICollection<OrderItem> Items { get; set; } = [];

    public int TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// Level 9: TPH Inheritance
public abstract class Payment
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public int OrderId { get; set; }
}

public class CreditCardPayment : Payment
{
    public string CardNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
}

public class BankTransferPayment : Payment
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

public class CashPayment : Payment
{
    public string ReceivedBy { get; set; } = string.Empty;
}

// Outbox Pattern Entity
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}

// Audit Log Entity
public class AuditLog
{
    public long Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
