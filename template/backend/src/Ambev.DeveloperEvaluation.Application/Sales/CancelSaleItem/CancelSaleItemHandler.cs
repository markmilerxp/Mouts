using MediatR;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public class CancelSaleItemHandler : IRequestHandler<CancelSaleItemCommand, CancelSaleItemResult>
{
    private const string SaleCacheKeyPrefix = "Sale:";

    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CancelSaleItemHandler> _logger;

    public CancelSaleItemHandler(ISaleRepository saleRepository, ISaleReadRepository saleReadRepository, IDistributedCache cache, ILogger<CancelSaleItemHandler> logger)
    {
        _saleRepository = saleRepository;
        _saleReadRepository = saleReadRepository;
        _cache = cache;
        _logger = logger;
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
        await _saleReadRepository.UpsertAsync(sale, cancellationToken);
        await _cache.RemoveAsync(SaleCacheKeyPrefix + sale.Id, cancellationToken);

        _logger.LogInformation("ItemCancelled: SaleId={SaleId}, ItemId={ItemId}", sale.Id, command.ItemId);

        return new CancelSaleItemResult
        {
            SaleId = sale.Id,
            ItemId = command.ItemId,
            IsCancelled = true,
            NewSaleTotal = sale.TotalAmount
        };
    }
}
