using Ambev.DeveloperEvaluation.Domain.Common;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

/// <summary>
/// Represents an individual item within a sale.
/// Enforces quantity-based discount rules as defined by business requirements.
/// </summary>
public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }

    // External Identity pattern: product belongs to another domain
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Applies quantity-based discount tiers and recalculates TotalAmount.
    /// Rules:
    ///   - Below 4 items  → no discount allowed
    ///   - 4 to 9 items   → 10% discount
    ///   - 10 to 20 items → 20% discount
    ///   - Above 20 items → not permitted (DomainException)
    /// </summary>
    public void ApplyDiscount()
    {
        if (Quantity > 20)
            throw new DomainException(
                $"It is not possible to sell above 20 identical items. Requested: {Quantity}.");

        Discount = Quantity switch
        {
            >= 10 => 0.20m,
            >= 4  => 0.10m,
            _     => 0.00m
        };

        TotalAmount = Quantity * UnitPrice * (1 - Discount);
    }
}
