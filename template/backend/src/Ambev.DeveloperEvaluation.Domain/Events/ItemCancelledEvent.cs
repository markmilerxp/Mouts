namespace Ambev.DeveloperEvaluation.Domain.Events;

public class ItemCancelledEvent
{
    public Guid SaleId { get; }
    public string SaleNumber { get; }
    public Guid ItemId { get; }
    public DateTime OccurredAt { get; }

    public ItemCancelledEvent(Guid saleId, string saleNumber, Guid itemId)
    {
        SaleId = saleId;
        SaleNumber = saleNumber;
        ItemId = itemId;
        OccurredAt = DateTime.UtcNow;
    }
}
