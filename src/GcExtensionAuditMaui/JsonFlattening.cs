// ### BEGIN: JsonFlattening

using System.Text.Json;

namespace GcExtensionAuditMaui;

public static class JsonTableBuilder
{
    public static List<Dictionary<string, string>> BuildRows(IReadOnlyList<JsonElement> items, int maxDepth = 5)
    {
        var rows = new List<Dictionary<string, string>>(items.Count);

        foreach (var el in items)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (el.ValueKind == JsonValueKind.Object)
            {
                FlattenObject(el, prefix: "", row, depth: 0, maxDepth);
            }
            else
            {
                row["value"] = ToScalar(el);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static void FlattenObject(JsonElement obj, string prefix, Dictionary<string, string> row, int depth, int maxDepth)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            var v = prop.Value;

            if (depth >= maxDepth)
            {
                row[key] = ToScalarOrJson(v);
                continue;
            }

            switch (v.ValueKind)
            {
                case JsonValueKind.Object:
                    FlattenObject(v, key, row, depth + 1, maxDepth);
                    break;

                case JsonValueKind.Array:
                    // Arrays become JSON string (keeps info without exploding columns)
                    row[key] = v.GetRawText();
                    break;

                default:
                    row[key] = ToScalar(v);
                    break;
            }
        }
    }

    private static string ToScalarOrJson(JsonElement v)
        => (v.ValueKind == JsonValueKind.Object || v.ValueKind == JsonValueKind.Array)
            ? v.GetRawText()
            : ToScalar(v);

    private static string ToScalar(JsonElement v)
    {
        try
        {
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.TryGetInt64(out var i) ? i.ToString() : v.GetDouble().ToString("G"),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                JsonValueKind.Undefined => "",
                _ => v.GetRawText()
            };
        }
        catch
        {
            return v.GetRawText();
        }
    }
}

// ### END: JsonFlattening
