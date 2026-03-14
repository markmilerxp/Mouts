using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class ListSalesHandlerTests
{
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly AutoMapper.IMapper _mapper;
    private readonly ListSalesHandler _handler;

    public ListSalesHandlerTests()
    {
        _saleReadRepository = Substitute.For<ISaleReadRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _mapper = Substitute.For<AutoMapper.IMapper>();
        _handler = new ListSalesHandler(_saleReadRepository, _cache, _mapper);
    }

    [Fact(DisplayName = "Given valid query When Handle Then returns paged result")]
    public async Task Handle_ValidQuery_ReturnsPagedResult()
    {
        var query = new ListSalesQuery { Page = 1, Size = 10 };
        var sales = new List<Sale> { new Sale { Id = Guid.NewGuid(), SaleNumber = "VND-001" } };
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<byte[]?>(null));
        _saleReadRepository.GetPagedAsync(1, 10, null, null, Arg.Any<CancellationToken>()).Returns((sales, 1));
        _cache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _mapper.Map<IEnumerable<ListSaleItemResult>>(Arg.Any<object>()).Returns(new List<ListSaleItemResult> { new ListSaleItemResult { Id = sales[0].Id } });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.TotalItems.Should().Be(1);
        result.CurrentPage.Should().Be(1);
        result.TotalPages.Should().Be(1);
        result.Data.Should().HaveCount(1);
        await _saleReadRepository.Received(1).GetPagedAsync(1, 10, null, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given invalid page When Handle Then throws ValidationException")]
    public async Task Handle_InvalidPage_ThrowsValidationException()
    {
        var query = new ListSalesQuery { Page = 0, Size = 10 };

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        await _saleReadRepository.DidNotReceive().GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<IDictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }
}
