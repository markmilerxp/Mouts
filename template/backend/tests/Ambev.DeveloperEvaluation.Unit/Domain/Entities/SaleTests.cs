using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities;

public class SaleTests
{
    [Fact(DisplayName = "Given active sale When Cancel Then status becomes Cancelled")]
    public void Cancel_ActiveSale_ChangesStatus()
    {
        var sale = new Sale { SaleNumber = "VND-001" };

        sale.Cancel();

        sale.Status.Should().Be(SaleStatus.Cancelled);
        sale.UpdatedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "Given cancelled sale When Cancel Then throws DomainException")]
    public void Cancel_AlreadyCancelled_ThrowsDomainException()
    {
        var sale = new Sale { SaleNumber = "VND-001" };
        sale.Cancel();

        var act = () => sale.Cancel();

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Given sale with item When CancelItem Then marks item cancelled and recalculates total")]
    public void CancelItem_ExistingItem_CancelsAndRecalculates()
    {
        var sale = new Sale { Id = Guid.NewGuid(), SaleNumber = "VND-001" };
        var itemA = sale.AddItem(Guid.NewGuid(), "Product A", 2, 10m);
        itemA.Id = Guid.NewGuid();
        var itemB = sale.AddItem(Guid.NewGuid(), "Product B", 2, 20m);
        itemB.Id = Guid.NewGuid();
        var before = sale.TotalAmount;

        sale.CancelItem(itemA.Id);

        sale.TotalAmount.Should().BeLessThan(before);
        sale.Items.Single(i => i.Id == itemA.Id).IsCancelled.Should().BeTrue();
        sale.UpdatedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "Given non-existing item When CancelItem Then throws DomainException")]
    public void CancelItem_ItemNotFound_ThrowsDomainException()
    {
        var sale = new Sale { Id = Guid.NewGuid(), SaleNumber = "VND-001" };
        sale.AddItem(Guid.NewGuid(), "Product A", 2, 10m).Id = Guid.NewGuid();

        var act = () => sale.CancelItem(Guid.NewGuid());

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Given already cancelled item When CancelItem Then throws DomainException")]
    public void CancelItem_AlreadyCancelled_ThrowsDomainException()
    {
        var sale = new Sale { Id = Guid.NewGuid(), SaleNumber = "VND-001" };
        var item = sale.AddItem(Guid.NewGuid(), "Product A", 2, 10m);
        item.Id = Guid.NewGuid();
        sale.CancelItem(item.Id);

        var act = () => sale.CancelItem(item.Id);

        act.Should().Throw<DomainException>();
    }
}

