using EfCoreMastery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCoreMastery.Infrastructure.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(c => c.Name);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.SKU).HasMaxLength(50);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");

        // Indexes (Level 6)
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.SKU).IsUnique();
        builder.HasIndex(p => new { p.CategoryId, p.Price });
        builder.HasIndex(p => p.Name).HasFilter("[IsActive] = 1");

        // Relationship
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // ComplexType Value Object (Level 5)
        builder.ComplexProperty(p => p.PriceDetails);
    }
}

public class StudentCourseConfiguration : IEntityTypeConfiguration<StudentCourse>
{
    public void Configure(EntityTypeBuilder<StudentCourse> builder)
    {
        builder.HasKey(sc => new { sc.StudentId, sc.CourseId });

        builder.HasOne(sc => sc.Student)
            .WithMany(s => s.StudentCourses)
            .HasForeignKey(sc => sc.StudentId);

        builder.HasOne(sc => sc.Course)
            .WithMany(c => c.StudentCourses)
            .HasForeignKey(sc => sc.CourseId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // TPH Inheritance Strategy (Level 9)
        builder.HasDiscriminator<string>("PaymentType")
            .HasValue<CreditCardPayment>("CreditCard")
            .HasValue<BankTransferPayment>("BankTransfer")
            .HasValue<CashPayment>("Cash");

        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
    }
}
