namespace Kart.Product.Infrastructure.ReadModel;

/// <summary>Binds the <c>"Mongo"</c> config section. <see cref="ConnectionString"/> should point
/// at the <c>mongos</c> router of the sharded cluster (docker-compose.yml), not a bare
/// <c>mongod</c> - <see cref="Database"/>'s <c>product_read_model</c> collection is sharded on
/// <c>category.id</c> (database-design.md).</summary>
public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string Database { get; set; } = "kart";
}
