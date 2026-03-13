using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CancelSaleItemHandlerTests
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly CancelSaleItemHandler _handler;

    public CancelSaleItemHandlerTests()
    {
        _saleRepository = Substitute.For<ISaleRepository>();
        _saleReadRepository = Substitute.For<ISaleReadRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _handler = new CancelSaleItemHandler(_saleRepository, _saleReadRepository, _cache);
    }

    [Fact(DisplayName = "Given valid sale and item id When Handle Then cancels item and returns result")]
    public async Task Handle_ValidIds_ReturnsResult()
    {
        var saleId = Guid.NewGuid();
        var sale = new Sale { Id = saleId };
        var item = sale.AddItem(Guid.NewGuid(), "Product", 2, 10m);
        var command = new CancelSaleItemCommand { SaleId = saleId, ItemId = item.Id };
        _saleRepository.GetByIdAsync(saleId, Arg.Any<CancellationToken>()).Returns(sale);
        _saleRepository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>()).Returns(sale);
        _saleReadRepository.UpsertAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _cache.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.SaleId.Should().Be(saleId);
        result.ItemId.Should().Be(item.Id);
        result.IsCancelled.Should().BeTrue();
        await _saleRepository.Received(1).UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given non-existent sale When Handle Then throws KeyNotFoundException")]
    public async Task Handle_SaleNotFound_ThrowsKeyNotFoundException()
    {
        var command = new CancelSaleItemCommand { SaleId = Guid.NewGuid(), ItemId = Guid.NewGuid() };
        _saleRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
