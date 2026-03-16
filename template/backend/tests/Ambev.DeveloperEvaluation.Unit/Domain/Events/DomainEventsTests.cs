using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Events;

public class DomainEventsTests
{
    [Fact(DisplayName = "Given sale When creating SaleCreatedEvent Then exposes sale and occurred date")]
    public void SaleCreatedEvent_Creation_SetsValues()
    {
        var sale = new Sale { Id = Guid.NewGuid(), SaleNumber = "VND-001" };

        var ev = new SaleCreatedEvent(sale);

        ev.Sale.Should().Be(sale);
        ev.OccurredAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact(DisplayName = "Given sale When creating SaleModifiedEvent Then exposes sale and occurred date")]
    public void SaleModifiedEvent_Creation_SetsValues()
    {
        var sale = new Sale { Id = Guid.NewGuid(), SaleNumber = "VND-001" };

        var ev = new SaleModifiedEvent(sale);

        ev.Sale.Should().Be(sale);
        ev.OccurredAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact(DisplayName = "Given ids When creating SaleCancelledEvent Then exposes values")]
    public void SaleCancelledEvent_Creation_SetsValues()
    {
        var saleId = Guid.NewGuid();
        const string saleNumber = "VND-001";

        var ev = new SaleCancelledEvent(saleId, saleNumber);

        ev.SaleId.Should().Be(saleId);
        ev.SaleNumber.Should().Be(saleNumber);
        ev.OccurredAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact(DisplayName = "Given ids When creating ItemCancelledEvent Then exposes values")]
    public void ItemCancelledEvent_Creation_SetsValues()
    {
        var saleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        const string saleNumber = "VND-001";

        var ev = new ItemCancelledEvent(saleId, saleNumber, itemId);

        ev.SaleId.Should().Be(saleId);
        ev.ItemId.Should().Be(itemId);
        ev.SaleNumber.Should().Be(saleNumber);
        ev.OccurredAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact(DisplayName = "Given user When creating UserRegisteredEvent Then exposes user")]
    public void UserRegisteredEvent_Creation_SetsValues()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "Marco", Email = "m@a.com" };

        var ev = new UserRegisteredEvent(user);

        ev.User.Should().Be(user);
    }
}

