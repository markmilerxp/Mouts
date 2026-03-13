namespace Ambev.DeveloperEvaluation.Domain.Events;

public class SaleCancelledEvent
{
    public Guid SaleId { get; }
    public string SaleNumber { get; }
    public DateTime OccurredAt { get; }

    public SaleCancelledEvent(Guid saleId, string saleNumber)
    {
        SaleId = saleId;
        SaleNumber = saleNumber;
        OccurredAt = DateTime.UtcNow;
    }
}
