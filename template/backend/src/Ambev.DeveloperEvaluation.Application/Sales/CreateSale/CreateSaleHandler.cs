using AutoMapper;
using MediatR;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public class CreateSaleHandler : IRequestHandler<CreateSaleCommand, CreateSaleResult>
{
    private const string SaleCacheKeyPrefix = "Sale:";

    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly IMapper _mapper;

    public CreateSaleHandler(ISaleRepository saleRepository, ISaleReadRepository saleReadRepository, IDistributedCache cache, IMapper mapper)
    {
        _saleRepository = saleRepository;
        _saleReadRepository = saleReadRepository;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<CreateSaleResult> Handle(CreateSaleCommand command, CancellationToken cancellationToken)
    {
        var validator = new CreateSaleValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var sale = new Sale
        {
            SaleNumber = command.SaleNumber,
            SaleDate = command.SaleDate,
            CustomerId = command.CustomerId,
            CustomerName = command.CustomerName,
            BranchId = command.BranchId,
            BranchName = command.BranchName
        };

        foreach (var item in command.Items)
            sale.AddItem(item.ProductId, item.ProductName, item.Quantity, item.UnitPrice);

        var created = await _saleRepository.CreateAsync(sale, cancellationToken);
        await _saleReadRepository.UpsertAsync(created, cancellationToken);
        await _cache.RemoveAsync(SaleCacheKeyPrefix + created.Id, cancellationToken);

        return _mapper.Map<CreateSaleResult>(created);
    }
}
