using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Domain.Repositories;

/// <summary>
/// Repository interface for Sale read operations (MongoDB — denormalized read model).
/// Supports pagination, ordering and filtering as defined in general-api.md.
/// </summary>
public interface ISaleReadRepository
{
    Task UpsertAsync(Sale sale, CancellationToken cancellationToken = default);
    Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IEnumerable<Sale> Items, int Total)> GetPagedAsync(
        int page,
        int size,
        string? order,
        IDictionary<string, string>? filters,
        CancellationToken cancellationToken = default);
}
