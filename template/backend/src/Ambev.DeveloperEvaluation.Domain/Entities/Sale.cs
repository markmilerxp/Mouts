using Ambev.DeveloperEvaluation.Common.Validation;
using Ambev.DeveloperEvaluation.Domain.Common;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Validation;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

/// <summary>
/// Sale aggregate root.
/// Encapsulates business rules for sale lifecycle: creation, item management, and cancellation.
/// References to Customer, Branch and Product follow the External Identities pattern —
/// they are stored as IDs + denormalized names, not as foreign keys to other aggregates.
/// </summary>
public class Sale : BaseEntity
{
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }

    // External Identity: Customer belongs to another domain
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    // External Identity: Branch belongs to another domain
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    public decimal TotalAmount { get; private set; }
    public SaleStatus Status { get; private set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<SaleItem> _items = new();
    public IReadOnlyList<SaleItem> Items => _items.AsReadOnly();

    public Sale()
    {
        CreatedAt = DateTime.UtcNow;
        Status = SaleStatus.Active;
    }

    /// <summary>
    /// Adds a new item to the sale, applying discount rules and recalculating totals.
    /// </summary>
    public SaleItem AddItem(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        var item = new SaleItem
        {
            SaleId = Id,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice
        };

        item.ApplyDiscount();
        _items.Add(item);
        Recalculate();

        return item;
    }

    /// <summary>
    /// Updates an existing item's quantity and recalculates discounts and totals.
    /// </summary>
    public void UpdateItem(Guid itemId, int quantity, decimal unitPrice)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException($"Item with ID {itemId} not found in sale {Id}.");

        if (item.IsCancelled)
            throw new DomainException($"Cannot update a cancelled item.");

        item.Quantity = quantity;
        item.UnitPrice = unitPrice;
        item.ApplyDiscount();

        Recalculate();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels the entire sale.
    /// </summary>
    public void Cancel()
    {
        if (Status == SaleStatus.Cancelled)
            throw new DomainException($"Sale {SaleNumber} is already cancelled.");

        Status = SaleStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels a specific item within the sale and recalculates the total.
    /// </summary>
    public void CancelItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException($"Item with ID {itemId} not found in sale {Id}.");

        if (item.IsCancelled)
            throw new DomainException($"Item {itemId} is already cancelled.");

        item.IsCancelled = true;
        Recalculate();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Recalculates the total amount based on active (non-cancelled) items.
    /// </summary>
    public void Recalculate()
    {
        TotalAmount = _items
            .Where(i => !i.IsCancelled)
            .Sum(i => i.TotalAmount);
    }

    public ValidationResultDetail Validate()
    {
        var validator = new SaleValidator();
        var result = validator.Validate(this);
        return new ValidationResultDetail
        {
            IsValid = result.IsValid,
            Errors = result.Errors.Select(e => (ValidationErrorDetail)e)
        };
    }
}
