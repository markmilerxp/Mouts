using AutoMapper;
using MediatR;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public class CancelSaleHandler : IRequestHandler<CancelSaleCommand, CancelSaleResult>
{
    private const string SaleCacheKeyPrefix = "Sale:";

    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<CancelSaleHandler> _logger;

    public CancelSaleHandler(ISaleRepository saleRepository, ISaleReadRepository saleReadRepository, IDistributedCache cache, IMapper mapper, ILogger<CancelSaleHandler> logger)
    {
        _saleRepository = saleRepository;
        _saleReadRepository = saleReadRepository;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<CancelSaleResult> Handle(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        var validator = new CancelSaleValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var sale = await _saleRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {command.Id} not found.");

        sale.Cancel();

        var updated = await _saleRepository.UpdateAsync(sale, cancellationToken);
        await _saleReadRepository.UpsertAsync(updated, cancellationToken);
        await _cache.RemoveAsync(SaleCacheKeyPrefix + updated.Id, cancellationToken);

        _logger.LogInformation("SaleCancelled: SaleId={SaleId}, SaleNumber={SaleNumber}", updated.Id, updated.SaleNumber);

        return _mapper.Map<CancelSaleResult>(updated);
    }
}
