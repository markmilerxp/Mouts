using System.Text.Json;
using AutoMapper;
using MediatR;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public class ListSalesHandler : IRequestHandler<ListSalesQuery, ListSalesResult>
{
    private const string CacheKeyPrefix = "Sales:List:";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly IMapper _mapper;

    public ListSalesHandler(ISaleReadRepository saleReadRepository, IDistributedCache cache, IMapper mapper)
    {
        _saleReadRepository = saleReadRepository;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<ListSalesResult> Handle(ListSalesQuery query, CancellationToken cancellationToken)
    {
        var validator = new ListSalesValidator();
        var validationResult = await validator.ValidateAsync(query, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var cacheKey = BuildListCacheKey(query);

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached != null)
            return JsonSerializer.Deserialize<ListSalesResult>(cached)!;

        var (items, total) = await _saleReadRepository.GetPagedAsync(
            query.Page,
            query.Size,
            query.Order,
            query.Filters,
            cancellationToken);

        var totalPages = (int)Math.Ceiling((double)total / query.Size);

        var result = new ListSalesResult
        {
            Data = _mapper.Map<IEnumerable<ListSaleItemResult>>(items),
            TotalItems = total,
            CurrentPage = query.Page,
            TotalPages = totalPages
        };

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiration },
            cancellationToken);

        return result;
    }

    private static string BuildListCacheKey(ListSalesQuery query)
    {
        var order = query.Order ?? string.Empty;
        var filterPart = query.Filters == null || query.Filters.Count == 0
            ? ""
            : string.Join("|", query.Filters.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
        return $"{CacheKeyPrefix}{query.Page}:{query.Size}:{order}:{filterPart}";
    }
}
