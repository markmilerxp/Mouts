using System.Text;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class GetSaleHandlerTests
{
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly AutoMapper.IMapper _mapper;
    private readonly GetSaleHandler _handler;

    public GetSaleHandlerTests()
    {
        _saleReadRepository = Substitute.For<ISaleReadRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _mapper = Substitute.For<AutoMapper.IMapper>();
        _handler = new GetSaleHandler(_saleReadRepository, _cache, _mapper);
    }

    [Fact(DisplayName = "Given existing id When Handle Then returns sale result")]
    public async Task Handle_ExistingId_ReturnsResult()
    {
        var saleId = Guid.NewGuid();
        var query = new GetSaleQuery { Id = saleId };
        var sale = new Sale { Id = saleId, SaleNumber = "VND-001" };
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<byte[]?>(null));
        _saleReadRepository.GetByIdAsync(saleId, Arg.Any<CancellationToken>()).Returns(sale);
        _cache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var expectedResult = new GetSaleResult { Id = saleId };
        _mapper.Map<GetSaleResult>(Arg.Any<Sale>()).Returns(expectedResult);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(saleId);
        await _saleReadRepository.Received(1).GetByIdAsync(saleId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given cached result When Handle Then returns from cache")]
    public async Task Handle_CacheHit_ReturnsCachedResult()
    {
        var saleId = Guid.NewGuid();
        var query = new GetSaleQuery { Id = saleId };
        var cachedResult = new GetSaleResult { Id = saleId, SaleNumber = "VND-001" };
        var cachedJson = System.Text.Json.JsonSerializer.Serialize(cachedResult);
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(cachedJson)));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(saleId);
        result.SaleNumber.Should().Be("VND-001");
        await _saleReadRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given non-existent id When Handle Then throws KeyNotFoundException")]
    public async Task Handle_NotFound_ThrowsKeyNotFoundException()
    {
        var query = new GetSaleQuery { Id = Guid.NewGuid() };
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<byte[]?>(null));
        _saleReadRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
