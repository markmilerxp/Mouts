using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class UpdateSaleValidatorTests
{
    [Fact(DisplayName = "Given valid command When Validate Then command is valid")]
    public void Validate_ValidCommand_IsValid()
    {
        var validator = new UpdateSaleValidator();
        var command = SalesHandlerTestData.CreateValidUpdateCommand(Guid.NewGuid());

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Given command with empty id and invalid item When Validate Then command is invalid")]
    public void Validate_InvalidCommand_IsInvalid()
    {
        var validator = new UpdateSaleValidator();
        var command = SalesHandlerTestData.CreateValidUpdateCommand(Guid.Empty);
        command.Items[0].Quantity = 0;

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .Should().Contain("Sale ID is required.")
            .And.Contain("Quantity must be greater than zero.");
    }
}

