using Ambev.DeveloperEvaluation.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities;

public class SaleItemTests
{
    [Theory(DisplayName = "Given quantity below 4 When ApplyDiscount Then no discount is applied")]
    [InlineData(1)]
    [InlineData(3)]
    public void ApplyDiscount_QuantityBelow4_NoDiscount(int quantity)
    {
        var item = new SaleItem
        {
            Quantity = quantity,
            UnitPrice = 10m
        };

        item.ApplyDiscount();

        item.Discount.Should().Be(0m);
        item.TotalAmount.Should().Be(quantity * 10m);
    }

    [Theory(DisplayName = "Given quantity from 4 to 9 When ApplyDiscount Then applies 10 percent")]
    [InlineData(4)]
    [InlineData(9)]
    public void ApplyDiscount_Quantity4To9_Applies10Percent(int quantity)
    {
        var item = new SaleItem
        {
            Quantity = quantity,
            UnitPrice = 10m
        };

        item.ApplyDiscount();

        item.Discount.Should().Be(0.10m);
        item.TotalAmount.Should().Be(quantity * 10m * 0.90m);
    }

    [Theory(DisplayName = "Given quantity from 10 to 20 When ApplyDiscount Then applies 20 percent")]
    [InlineData(10)]
    [InlineData(20)]
    public void ApplyDiscount_Quantity10To20_Applies20Percent(int quantity)
    {
        var item = new SaleItem
        {
            Quantity = quantity,
            UnitPrice = 10m
        };

        item.ApplyDiscount();

        item.Discount.Should().Be(0.20m);
        item.TotalAmount.Should().Be(quantity * 10m * 0.80m);
    }

    [Fact(DisplayName = "Given quantity above 20 When ApplyDiscount Then throws DomainException")]
    public void ApplyDiscount_QuantityAbove20_ThrowsDomainException()
    {
        var item = new SaleItem
        {
            Quantity = 21,
            UnitPrice = 10m
        };

        var act = () => item.ApplyDiscount();

        act.Should().Throw<DomainException>();
    }
}

