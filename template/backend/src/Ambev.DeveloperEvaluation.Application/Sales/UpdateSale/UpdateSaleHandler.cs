using AutoMapper;
using MediatR;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, UpdateSaleResult>
{
    private const string SaleCacheKeyPrefix = "Sale:";

    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateSaleHandler> _logger;

    public UpdateSaleHandler(ISaleRepository saleRepository, ISaleReadRepository saleReadRepository, IDistributedCache cache, IMapper mapper, ILogger<UpdateSaleHandler> logger)
    {
        _saleRepository = saleRepository;
        _saleReadRepository = saleReadRepository;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UpdateSaleResult> Handle(UpdateSaleCommand command, CancellationToken cancellationToken)
    {
        var validator = new UpdateSaleValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var sale = await _saleRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {command.Id} not found.");

        sale.SaleNumber = command.SaleNumber;
        sale.SaleDate = command.SaleDate;
        sale.CustomerId = command.CustomerId;
        sale.CustomerName = command.CustomerName;
        sale.BranchId = command.BranchId;
        sale.BranchName = command.BranchName;

        foreach (var item in command.Items)
            sale.AddItem(item.ProductId, item.ProductName, item.Quantity, item.UnitPrice);

        var updated = await _saleRepository.UpdateAsync(sale, cancellationToken);
        await _saleReadRepository.UpsertAsync(updated, cancellationToken);
        await _cache.RemoveAsync(SaleCacheKeyPrefix + updated.Id, cancellationToken);

        _logger.LogInformation("SaleModified: SaleId={SaleId}, SaleNumber={SaleNumber}", updated.Id, updated.SaleNumber);

        return _mapper.Map<UpdateSaleResult>(updated);
    }
}
