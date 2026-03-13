using AutoMapper;
using MediatR;
using FluentValidation;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public class ListSalesHandler : IRequestHandler<ListSalesQuery, ListSalesResult>
{
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IMapper _mapper;

    public ListSalesHandler(ISaleReadRepository saleReadRepository, IMapper mapper)
    {
        _saleReadRepository = saleReadRepository;
        _mapper = mapper;
    }

    public async Task<ListSalesResult> Handle(ListSalesQuery query, CancellationToken cancellationToken)
    {
        var validator = new ListSalesValidator();
        var validationResult = await validator.ValidateAsync(query, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var (items, total) = await _saleReadRepository.GetPagedAsync(
            query.Page,
            query.Size,
            query.Order,
            query.Filters,
            cancellationToken);

        var totalPages = (int)Math.Ceiling((double)total / query.Size);

        return new ListSalesResult
        {
            Data = _mapper.Map<IEnumerable<ListSaleItemResult>>(items),
            TotalItems = total,
            CurrentPage = query.Page,
            TotalPages = totalPages
        };
    }
}
