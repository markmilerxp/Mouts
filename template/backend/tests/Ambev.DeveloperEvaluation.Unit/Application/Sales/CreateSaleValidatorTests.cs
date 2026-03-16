using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CreateSaleValidatorTests
{
    [Fact(DisplayName = "Given valid command When Validate Then command is valid")]
    public void Validate_ValidCommand_IsValid()
    {
        var validator = new CreateSaleValidator();
        var command = SalesHandlerTestData.CreateValidCreateCommand();

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Given command without sale number and items When Validate Then command is invalid")]
    public void Validate_InvalidCommand_IsInvalid()
    {
        var validator = new CreateSaleValidator();
        var command = SalesHandlerTestData.CreateValidCreateCommand();
        command.SaleNumber = string.Empty;
        command.Items = new List<CreateSaleItemCommand>();

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .Should().Contain("Sale number is required.")
            .And.Contain("Sale must have at least one item.");
    }
}

