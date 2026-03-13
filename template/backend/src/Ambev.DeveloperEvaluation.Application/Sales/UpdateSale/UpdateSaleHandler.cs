using AutoMapper;
using MediatR;
using FluentValidation;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, UpdateSaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IMapper _mapper;

    public UpdateSaleHandler(ISaleRepository saleRepository, ISaleReadRepository saleReadRepository, IMapper mapper)
    {
        _saleRepository = saleRepository;
        _saleReadRepository = saleReadRepository;
        _mapper = mapper;
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

        return _mapper.Map<UpdateSaleResult>(updated);
    }
}
