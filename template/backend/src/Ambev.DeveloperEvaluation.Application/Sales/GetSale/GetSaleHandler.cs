using System.Text.Json;
using AutoMapper;
using MediatR;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public class GetSaleHandler : IRequestHandler<GetSaleQuery, GetSaleResult>
{
    private const string CacheKeyPrefix = "Sale:";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly IMapper _mapper;

    public GetSaleHandler(ISaleReadRepository saleReadRepository, IDistributedCache cache, IMapper mapper)
    {
        _saleReadRepository = saleReadRepository;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<GetSaleResult> Handle(GetSaleQuery query, CancellationToken cancellationToken)
    {
        var validator = new GetSaleValidator();
        var validationResult = await validator.ValidateAsync(query, cancellationToken);

        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var cacheKey = CacheKeyPrefix + query.Id.ToString();

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached != null)
            return JsonSerializer.Deserialize<GetSaleResult>(cached)!;

        var sale = await _saleReadRepository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {query.Id} not found.");

        var result = _mapper.Map<GetSaleResult>(sale);

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiration },
            cancellationToken);

        return result;
    }
}
