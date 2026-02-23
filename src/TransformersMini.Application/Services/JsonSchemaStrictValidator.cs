using System.Globalization;
using System.Text.Json;

namespace TransformersMini.Application.Services;

internal sealed class JsonSchemaStrictValidator
{
    public void Validate(string jsonText, string schemaPath)
    {
        using var instance = JsonDocument.Parse(jsonText);
        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));

        var errors = new List<string>();
        ValidateNode(instance.RootElement, schema.RootElement, "$", errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("JSON Schema 校验失败: " + string.Join(" | ", errors));
        }
    }

    private static void ValidateNode(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        ValidateType(value, schema, path, errors);
        ValidateEnum(value, schema, path, errors);
        ValidateNumberRange(value, schema, path, errors);

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(value, schema, path, errors);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            ValidateArray(value, schema, path, errors);
        }
    }

    private static void ValidateObject(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredProp in required.EnumerateArray())
            {
                if (requiredProp.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var name = requiredProp.GetString()!;
                if (!value.TryGetProperty(name, out _))
                {
                    errors.Add($"{path}: 缺少必填字段 '{name}'");
                }
            }
        }

        var hasProps = schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object;
        var additionalAllowed = true;
        if (schema.TryGetProperty("additionalProperties", out var additionalProp) && additionalProp.ValueKind == JsonValueKind.False)
        {
            additionalAllowed = false;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (hasProps && properties.TryGetProperty(property.Name, out var childSchema))
            {
                ValidateNode(property.Value, childSchema, $"{path}.{property.Name}", errors);
                continue;
            }

            if (!additionalAllowed)
            {
                errors.Add($"{path}: 不允许的字段 '{property.Name}'");
            }
        }
    }

    private static void ValidateArray(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("items", out var itemsSchema))
        {
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            ValidateNode(item, itemsSchema, $"{path}[{index}]", errors);
            index++;
        }
    }

    private static void ValidateType(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("type", out var typeNode))
        {
            return;
        }

        var allowedTypes = new List<string>();
        if (typeNode.ValueKind == JsonValueKind.String)
        {
            allowedTypes.Add(typeNode.GetString()!);
        }
        else if (typeNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in typeNode.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    allowedTypes.Add(item.GetString()!);
                }
            }
        }

        if (allowedTypes.Count == 0)
        {
            return;
        }

        var actual = GetJsonType(value);
        if (!allowedTypes.Contains(actual, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"{path}: 类型不匹配，期望 [{string.Join(", ", allowedTypes)}]，实际 {actual}");
        }
    }

    private static void ValidateEnum(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("enum", out var enumNode) || enumNode.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in enumNode.EnumerateArray())
        {
            if (JsonElementEquals(item, value))
            {
                return;
            }
        }

        errors.Add($"{path}: 值不在 enum 列表中");
    }

    private static void ValidateNumberRange(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (value.ValueKind is not (JsonValueKind.Number))
        {
            return;
        }

        if (!value.TryGetDouble(out var number))
        {
            return;
        }

        if (schema.TryGetProperty("minimum", out var minimumNode) &&
            minimumNode.ValueKind == JsonValueKind.Number &&
            minimumNode.TryGetDouble(out var min) &&
            number < min)
        {
            errors.Add($"{path}: 数值 {number.ToString(CultureInfo.InvariantCulture)} 小于 minimum {min.ToString(CultureInfo.InvariantCulture)}");
        }

        if (schema.TryGetProperty("exclusiveMinimum", out var exclusiveMinimumNode) &&
            exclusiveMinimumNode.ValueKind == JsonValueKind.Number &&
            exclusiveMinimumNode.TryGetDouble(out var exclusiveMin) &&
            number <= exclusiveMin)
        {
            errors.Add($"{path}: 数值 {number.ToString(CultureInfo.InvariantCulture)} 必须大于 {exclusiveMin.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    private static string GetJsonType(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => value.TryGetInt64(out _) ? "integer" : "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        _ => "unknown"
    };

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
            {
                return left.TryGetDouble(out var l) && right.TryGetDouble(out var r) && Math.Abs(l - r) < 1e-12;
            }

            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.TryGetDouble(out var l) && right.TryGetDouble(out var r) && Math.Abs(l - r) < 1e-12,
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText()
        };
    }
}
