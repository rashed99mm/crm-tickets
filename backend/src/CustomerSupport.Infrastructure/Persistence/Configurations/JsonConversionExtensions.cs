using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CustomerSupport.Infrastructure.Persistence.Configurations;

internal static class JsonConversionExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<TProperty> HasJsonConversion<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
    {
        var converter = new ValueConverter<TProperty, string?>(
            value => Serialize(value),
            value => Deserialize<TProperty>(value));

        var comparer = new ValueComparer<TProperty>(
            (left, right) => Serialize(left) == Serialize(right),
            value => GetHashCode(value),
            value => Deserialize<TProperty>(Serialize(value)));

        propertyBuilder.HasConversion(converter);
        propertyBuilder.Metadata.SetValueComparer(comparer);

        return propertyBuilder;
    }

    private static int GetHashCode<TProperty>(TProperty value)
    {
        var serialized = Serialize(value);
        return serialized?.GetHashCode() ?? 0;
    }

    private static string? Serialize<TProperty>(TProperty value)
        => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static TProperty Deserialize<TProperty>(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? default!
            : JsonSerializer.Deserialize<TProperty>(json, JsonOptions)!;
}
