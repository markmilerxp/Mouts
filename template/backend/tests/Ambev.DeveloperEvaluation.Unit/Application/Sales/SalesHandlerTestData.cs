using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Bogus;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public static class SalesHandlerTestData
{
    private static readonly Faker<CreateSaleItemCommand> CreateItemFaker = new Faker<CreateSaleItemCommand>()
        .RuleFor(x => x.ProductId, f => f.Random.Guid())
        .RuleFor(x => x.ProductName, f => f.Commerce.ProductName())
        .RuleFor(x => x.Quantity, f => f.Random.Int(1, 10))
        .RuleFor(x => x.UnitPrice, f => f.Random.Decimal(1, 100));

    public static CreateSaleCommand CreateValidCreateCommand(int itemCount = 2)
    {
        var items = CreateItemFaker.Generate(Math.Clamp(itemCount, 1, 20));
        return new Faker<CreateSaleCommand>()
            .RuleFor(x => x.SaleNumber, f => $"VND-{f.Random.AlphaNumeric(8).ToUpperInvariant()}")
            .RuleFor(x => x.SaleDate, f => f.Date.RecentOffset(30).UtcDateTime)
            .RuleFor(x => x.CustomerId, f => f.Random.Guid())
            .RuleFor(x => x.CustomerName, f => f.Person.FullName)
            .RuleFor(x => x.BranchId, f => f.Random.Guid())
            .RuleFor(x => x.BranchName, f => f.Company.CompanyName())
            .RuleFor(x => x.Items, _ => items)
            .Generate();
    }

    public static UpdateSaleCommand CreateValidUpdateCommand(Guid saleId, int itemCount = 2)
    {
        var items = CreateItemFaker.Generate(Math.Clamp(itemCount, 1, 20));
        return new Faker<UpdateSaleCommand>()
            .RuleFor(x => x.Id, _ => saleId)
            .RuleFor(x => x.SaleNumber, f => $"VND-{f.Random.AlphaNumeric(8).ToUpperInvariant()}")
            .RuleFor(x => x.SaleDate, f => f.Date.RecentOffset(30).UtcDateTime)
            .RuleFor(x => x.CustomerId, f => f.Random.Guid())
            .RuleFor(x => x.CustomerName, f => f.Person.FullName)
            .RuleFor(x => x.BranchId, f => f.Random.Guid())
            .RuleFor(x => x.BranchName, f => f.Company.CompanyName())
            .RuleFor(x => x.Items, _ => items.Select(i => new UpdateSaleItemCommand
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList())
            .Generate();
    }
}
