using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CreateSaleHandlerTests
{
    private readonly ISaleRepository _saleRepository;
    private readonly ISaleReadRepository _saleReadRepository;
    private readonly IDistributedCache _cache;
    private readonly AutoMapper.IMapper _mapper;
    private readonly CreateSaleHandler _handler;

    public CreateSaleHandlerTests()
    {
        _saleRepository = Substitute.For<ISaleRepository>();
        _saleReadRepository = Substitute.For<ISaleReadRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _mapper = Substitute.For<AutoMapper.IMapper>();
        _handler = new CreateSaleHandler(_saleRepository, _saleReadRepository, _cache, _mapper);
    }

    [Fact(DisplayName = "Given valid command When Handle Then creates sale and returns result")]
    public async Task Handle_ValidCommand_ReturnsResult()
    {
        var command = SalesHandlerTestData.CreateValidCreateCommand(2);
        var createdId = Guid.NewGuid();
        Sale? capturedSale = null;
        _saleRepository.CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedSale = call.ArgAt<Sale>(0);
                capturedSale.Id = createdId;
                return Task.FromResult(capturedSale);
            });
        _cache.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _saleReadRepository.UpsertAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var expectedResult = new CreateSaleResult { Id = createdId, TotalAmount = 100 };
        _mapper.Map<CreateSaleResult>(Arg.Any<Sale>()).Returns(expectedResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(createdId);
        await _saleRepository.Received(1).CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
        await _saleReadRepository.Received(1).UpsertAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(Arg.Is<string>(k => k == "Sale:" + createdId), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given invalid command When Handle Then throws ValidationException")]
    public async Task Handle_InvalidCommand_ThrowsValidationException()
    {
        var command = new CreateSaleCommand { SaleNumber = "", Items = new List<CreateSaleItemCommand>() };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        await _saleRepository.DidNotReceive().CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }
}
