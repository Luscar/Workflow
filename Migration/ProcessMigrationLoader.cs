using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleBPM.Migration;

public static class ProcessMigrationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ProcessMigration FromJson(string json)
    {
        var jsonDef = JsonSerializer.Deserialize<MigrationJsonDefinition>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid migration JSON");

        var migration = new ProcessMigration(jsonDef.FromVersion, jsonDef.ToVersion);

        if (jsonDef.NodeMappings != null)
        {
            foreach (var kvp in jsonDef.NodeMappings)
                migration.MapNode(kvp.Key, kvp.Value);
        }

        if (jsonDef.VariableTransforms != null)
        {
            foreach (var t in jsonDef.VariableTransforms)
            {
                var type = Enum.Parse<VariableTransformType>(t.Type, ignoreCase: true);
                switch (type)
                {
                    case VariableTransformType.Set:
                        migration.SetVariable(t.Name!, ResolveJsonValue(t.Value));
                        break;
                    case VariableTransformType.Rename:
                        migration.RenameVariable(t.Name!, t.NewName!);
                        break;
                    case VariableTransformType.Remove:
                        migration.RemoveVariable(t.Name!);
                        break;
                }
            }
        }

        return migration;
    }

    public static ProcessMigration FromJsonFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return FromJson(json);
    }

    public static string ToJson(ProcessMigration migration)
    {
        var jsonDef = new MigrationJsonDefinition
        {
            FromVersion = migration.FromVersion,
            ToVersion = migration.ToVersion,
            NodeMappings = migration.NodeMappings.Count > 0 ? migration.NodeMappings : null,
            VariableTransforms = migration.VariableTransforms.Count > 0
                ? migration.VariableTransforms.Select(ToJsonTransform).ToList()
                : null
        };

        return JsonSerializer.Serialize(jsonDef, JsonOptions);
    }

    private static object ResolveJsonValue(JsonElement? element)
    {
        if (element == null) return "";

        return element.Value.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString()!,
            JsonValueKind.Number => element.Value.TryGetInt64(out var l) ? l : element.Value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.Value.GetRawText()
        };
    }

    private static VariableTransformJsonDefinition ToJsonTransform(VariableTransform t)
    {
        return new VariableTransformJsonDefinition
        {
            Type = t.Type.ToString().ToLowerInvariant(),
            Name = t.Name,
            NewName = t.Type == VariableTransformType.Rename ? t.NewName : null,
            Value = t.Type == VariableTransformType.Set ? JsonSerializer.SerializeToElement(t.Value) : null
        };
    }
}

public class MigrationJsonDefinition
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public Dictionary<string, string>? NodeMappings { get; set; }
    public List<VariableTransformJsonDefinition>? VariableTransforms { get; set; }
}

public class VariableTransformJsonDefinition
{
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NewName { get; set; }
    public JsonElement? Value { get; set; }
}
