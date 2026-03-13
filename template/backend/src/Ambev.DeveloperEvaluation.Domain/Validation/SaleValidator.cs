using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Domain.Validation;

public class SaleValidator : AbstractValidator<Sale>
{
    public SaleValidator()
    {
        RuleFor(s => s.SaleNumber)
            .NotEmpty()
            .WithMessage("SaleNumber is required.")
            .MaximumLength(50)
            .WithMessage("SaleNumber cannot exceed 50 characters.");

        RuleFor(s => s.SaleDate)
            .NotEmpty()
            .WithMessage("SaleDate is required.");

        RuleFor(s => s.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required.");

        RuleFor(s => s.CustomerName)
            .NotEmpty()
            .WithMessage("CustomerName is required.")
            .MaximumLength(200)
            .WithMessage("CustomerName cannot exceed 200 characters.");

        RuleFor(s => s.BranchId)
            .NotEmpty()
            .WithMessage("BranchId is required.");

        RuleFor(s => s.BranchName)
            .NotEmpty()
            .WithMessage("BranchName cannot exceed 200 characters.")
            .MaximumLength(200)
            .WithMessage("BranchName cannot exceed 200 characters.");

        RuleFor(s => s.Status)
            .NotEqual(SaleStatus.Unknown)
            .WithMessage("Sale status cannot be Unknown.");

        RuleFor(s => s.Items)
            .NotEmpty()
            .WithMessage("Sale must contain at least one item.");

        RuleForEach(s => s.Items)
            .SetValidator(new SaleItemValidator());
    }
}
