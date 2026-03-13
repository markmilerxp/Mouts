using Ambev.DeveloperEvaluation.Domain.Entities;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Domain.Validation;

public class SaleItemValidator : AbstractValidator<SaleItem>
{
    public SaleItemValidator()
    {
        RuleFor(i => i.ProductId)
            .NotEmpty()
            .WithMessage("ProductId is required.");

        RuleFor(i => i.ProductName)
            .NotEmpty()
            .WithMessage("ProductName is required.")
            .MaximumLength(200)
            .WithMessage("ProductName cannot exceed 200 characters.");

        RuleFor(i => i.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than zero.")
            .LessThanOrEqualTo(20)
            .WithMessage("It is not possible to sell above 20 identical items.");

        RuleFor(i => i.UnitPrice)
            .GreaterThan(0)
            .WithMessage("UnitPrice must be greater than zero.");

        RuleFor(i => i.Discount)
            .InclusiveBetween(0, 1)
            .WithMessage("Discount must be between 0 and 1 (0% to 100%).");

        RuleFor(i => i.TotalAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("TotalAmount cannot be negative.");
    }
}
