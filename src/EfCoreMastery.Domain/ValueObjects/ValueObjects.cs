using System.ComponentModel.DataAnnotations.Schema;

namespace EfCoreMastery.Domain.ValueObjects;

[ComplexType]
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

[ComplexType]
public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}
