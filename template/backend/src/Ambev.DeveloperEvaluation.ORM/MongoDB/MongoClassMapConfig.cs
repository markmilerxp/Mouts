using Ambev.DeveloperEvaluation.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Ambev.DeveloperEvaluation.ORM.MongoDB;

/// <summary>
/// Configures BSON class maps for domain entities used in MongoDB read store.
/// Must be called once at application startup, before any MongoDB operation.
/// </summary>
public static class MongoClassMapConfig
{
    private static bool _registered;
    private static readonly object _lock = new();

    public static void Register()
    {
        lock (_lock)
        {
            if (_registered) return;

            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            if (!BsonClassMap.IsClassMapRegistered(typeof(SaleItem)))
            {
                BsonClassMap.RegisterClassMap<SaleItem>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIdMember(cm.GetMemberMap(i => i.Id));
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Sale)))
            {
                BsonClassMap.RegisterClassMap<Sale>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIdMember(cm.GetMemberMap(s => s.Id));
                    cm.UnmapMember(s => s.Items);
                    cm.MapField("_items").SetElementName("Items");
                    cm.SetIgnoreExtraElements(true);
                });
            }

            _registered = true;
        }
    }
}
