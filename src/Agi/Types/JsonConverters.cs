using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agi.Types;

/// <summary>
/// Custom JSON converter for enums that uses JsonPropertyName attributes for enum member names.
/// Works across all .NET versions (6, 8, 9).
/// </summary>
public class JsonEnumMemberConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private readonly Dictionary<TEnum, string> _enumToString = new();
    private readonly Dictionary<string, TEnum> _stringToEnum = new();

    public JsonEnumMemberConverter()
    {
        var enumType = typeof(TEnum);
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var memberInfo = enumType.GetMember(value.ToString())[0];
            var attribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>();
            var name = attribute?.Name ?? value.ToString();

            _enumToString[value] = name;
            _stringToEnum[name] = value;
            // Also add the plain enum name as fallback
            _stringToEnum[value.ToString().ToLowerInvariant()] = value;
        }
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();
        if (stringValue == null)
        {
            throw new JsonException($"Cannot convert null to {typeof(TEnum).Name}");
        }

        if (_stringToEnum.TryGetValue(stringValue, out var result))
        {
            return result;
        }

        // Try case-insensitive match
        foreach (var kvp in _stringToEnum)
        {
            if (string.Equals(kvp.Key, stringValue, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        throw new JsonException($"Cannot convert '{stringValue}' to {typeof(TEnum).Name}");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (_enumToString.TryGetValue(value, out var stringValue))
        {
            writer.WriteStringValue(stringValue);
        }
        else
        {
            writer.WriteStringValue(value.ToString().ToLowerInvariant());
        }
    }
}

/// <summary>
/// Converter factory for enums with JsonPropertyName attributes
/// </summary>
public class JsonEnumMemberConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(JsonEnumMemberConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}
