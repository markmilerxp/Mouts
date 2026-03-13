using AutoMapper;
using MediatR;
using FluentValidation;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public class GetSaleHandler : IRequestHandler<GetSaleQuery, GetSaleResult>
{
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IMapper _mapper;

    public GetSaleHandler(ISaleReadRepository saleReadRepository, IMapper mapper)
    {
        _saleReadRepository = saleReadRepository;
        _mapper = mapper;
    }

    public async Task<GetSaleResult> Handle(GetSaleQuery query, CancellationToken cancellationToken)
    {
        var validator = new GetSaleValidator();
        var validationResult = await validator.ValidateAsync(query, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var sale = await _saleReadRepository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {query.Id} not found.");

        return _mapper.Map<GetSaleResult>(sale);
    }
}
