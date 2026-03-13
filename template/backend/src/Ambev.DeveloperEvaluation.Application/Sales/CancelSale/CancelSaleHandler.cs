using AutoMapper;
using MediatR;
using FluentValidation;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public class CancelSaleHandler : IRequestHandler<CancelSaleCommand, CancelSaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IMapper _mapper;

    public CancelSaleHandler(ISaleRepository saleRepository, ISaleReadRepository saleReadRepository, IMapper mapper)
    {
        _saleRepository = saleRepository;
        _saleReadRepository = saleReadRepository;
        _mapper = mapper;
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

        return _mapper.Map<CancelSaleResult>(updated);
    }
}
