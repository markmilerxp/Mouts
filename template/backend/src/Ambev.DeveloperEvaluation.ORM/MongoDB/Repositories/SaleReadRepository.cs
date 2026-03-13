using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ambev.DeveloperEvaluation.ORM.MongoDB.Repositories;

/// <summary>
/// MongoDB implementation of ISaleReadRepository.
/// Stores denormalized Sale snapshots for fast read operations with pagination,
/// ordering and dynamic filtering as defined in general-api.md.
/// </summary>
public class SaleReadRepository : ISaleReadRepository
{
    private readonly IMongoCollection<Sale> _collection;

    public SaleReadRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<Sale>("sales");
    }

    public async Task UpsertAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Sale>.Filter.Eq(s => s.Id, sale.Id);
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, sale, options, cancellationToken);
    }

    public async Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Sale>.Filter.Eq(s => s.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Sale>.Filter.Eq(s => s.Id, id);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }

    public async Task<(IEnumerable<Sale> Items, int Total)> GetPagedAsync(
        int page,
        int size,
        string? order,
        IDictionary<string, string>? filters,
        CancellationToken cancellationToken = default)
    {
        var filterDef = BuildFilters(filters);
        var sortDef = BuildSort(order);

        var total = (int)await _collection
            .CountDocumentsAsync(filterDef, cancellationToken: cancellationToken);

        var items = await _collection
            .Find(filterDef)
            .Sort(sortDef)
            .Skip((page - 1) * size)
            .Limit(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    private static FilterDefinition<Sale> BuildFilters(IDictionary<string, string>? filters)
    {
        if (filters == null || filters.Count == 0)
            return Builders<Sale>.Filter.Empty;

        var conditions = new List<FilterDefinition<Sale>>();

        foreach (var (key, value) in filters)
        {
            if (key.StartsWith("_min", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("_max", StringComparison.OrdinalIgnoreCase))
                continue;

            if (value.Contains('*'))
            {
                var pattern = "^" + value.Replace("*", ".*") + "$";
                conditions.Add(Builders<Sale>.Filter.Regex(key, new BsonRegularExpression(pattern, "i")));
            }
            else
            {
                conditions.Add(Builders<Sale>.Filter.Eq(key, value));
            }
        }

        foreach (var key in filters.Keys.Where(k =>
            k.StartsWith("_min", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("_max", StringComparison.OrdinalIgnoreCase)))
        {
            var fieldName = key[4..];
            var rawValue = filters[key];

            if (key.StartsWith("_min", StringComparison.OrdinalIgnoreCase) &&
                decimal.TryParse(rawValue, out var minVal))
            {
                conditions.Add(Builders<Sale>.Filter.Gte(fieldName, minVal));
            }
            else if (key.StartsWith("_max", StringComparison.OrdinalIgnoreCase) &&
                decimal.TryParse(rawValue, out var maxVal))
            {
                conditions.Add(Builders<Sale>.Filter.Lte(fieldName, maxVal));
            }
        }

        return conditions.Count > 0
            ? Builders<Sale>.Filter.And(conditions)
            : Builders<Sale>.Filter.Empty;
    }

    private static SortDefinition<Sale> BuildSort(string? order)
    {
        if (string.IsNullOrWhiteSpace(order))
            return Builders<Sale>.Sort.Descending(s => s.CreatedAt);

        var parts = order.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts[0];
        var descending = parts.Length > 1 &&
            parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return descending
            ? Builders<Sale>.Sort.Descending(field)
            : Builders<Sale>.Sort.Ascending(field);
    }
}
