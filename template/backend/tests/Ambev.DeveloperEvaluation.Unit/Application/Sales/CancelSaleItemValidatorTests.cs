using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CancelSaleItemValidatorTests
{
    [Fact(DisplayName = "Given valid command When Validate Then command is valid")]
    public void Validate_ValidCommand_IsValid()
    {
        var validator = new CancelSaleItemValidator();
        var command = new CancelSaleItemCommand
        {
            SaleId = Guid.NewGuid(),
            ItemId = Guid.NewGuid()
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Given empty sale id and item id When Validate Then command is invalid")]
    public void Validate_EmptyIds_IsInvalid()
    {
        var validator = new CancelSaleItemValidator();
        var command = new CancelSaleItemCommand
        {
            SaleId = Guid.Empty,
            ItemId = Guid.Empty
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .Should().Contain("Sale ID is required.")
            .And.Contain("Item ID is required.");
    }
}

