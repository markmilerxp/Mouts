using AutoMapper;
using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public class ListSalesProfile : Profile
{
    public ListSalesProfile()
    {
        CreateMap<SaleItem, ListSaleLineItemResult>();
        CreateMap<Sale, ListSaleItemResult>()
            .ForMember(d => d.Items, o => o.MapFrom(s => s.Items));
    }
}
