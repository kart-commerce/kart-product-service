using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Product.Infrastructure.Persistence;

/// <summary>Maps <c>variants.extended_attributes JSONB</c> (database-design.md's schemaless EAV
/// bag) to/from <c>IReadOnlyDictionary&lt;string, object?&gt;</c>.</summary>
public static class JsonDictionaryValueConverter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<IReadOnlyDictionary<string, object?>, string> Converter = new(
        dict => JsonSerializer.Serialize(dict, Options),
        json => JsonSerializer.Deserialize<Dictionary<string, object?>>(json, Options) ?? new Dictionary<string, object?>());

    public static readonly ValueComparer<IReadOnlyDictionary<string, object?>> Comparer = new(
        (left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
        dict => JsonSerializer.Serialize(dict, Options).GetHashCode(),
        dict => JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(dict, Options), Options)!);
}
