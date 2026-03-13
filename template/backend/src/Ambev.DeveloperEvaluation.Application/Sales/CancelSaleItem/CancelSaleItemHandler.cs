using MediatR;
using FluentValidation;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public class CancelSaleItemHandler : IRequestHandler<CancelSaleItemCommand, CancelSaleItemResult>
{
    private readonly ISaleRepository _saleRepository;

    public CancelSaleItemHandler(ISaleRepository saleRepository)
    {
        _saleRepository = saleRepository;
    }

    public async Task<CancelSaleItemResult> Handle(CancelSaleItemCommand command, CancellationToken cancellationToken)
    {
        var validator = new CancelSaleItemValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var sale = await _saleRepository.GetByIdAsync(command.SaleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {command.SaleId} not found.");

        sale.CancelItem(command.ItemId);

        await _saleRepository.UpdateAsync(sale, cancellationToken);

        return new CancelSaleItemResult
        {
            SaleId = sale.Id,
            ItemId = command.ItemId,
            IsCancelled = true,
            NewSaleTotal = sale.TotalAmount
        };
    }
}
