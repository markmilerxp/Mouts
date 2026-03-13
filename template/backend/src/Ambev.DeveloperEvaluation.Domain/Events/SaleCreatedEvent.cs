using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Domain.Events;

public class SaleCreatedEvent
{
    public Sale Sale { get; }
    public DateTime OccurredAt { get; }

    public SaleCreatedEvent(Sale sale)
    {
        Sale = sale;
        OccurredAt = DateTime.UtcNow;
    }
}
