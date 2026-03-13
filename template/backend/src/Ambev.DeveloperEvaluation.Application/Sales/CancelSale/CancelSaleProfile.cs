using AutoMapper;
using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public class CancelSaleProfile : Profile
{
    public CancelSaleProfile()
    {
        CreateMap<Sale, CancelSaleResult>();
    }
}
