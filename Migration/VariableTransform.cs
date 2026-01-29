namespace SimpleBPM.Migration;

public enum VariableTransformType
{
    Set,
    Rename,
    Remove
}

public class VariableTransform
{
    public VariableTransformType Type { get; }
    public string Name { get; }
    public string? NewName { get; }
    public object? Value { get; }

    private VariableTransform(VariableTransformType type, string name, string? newName = null, object? value = null)
    {
        Type = type;
        Name = name;
        NewName = newName;
        Value = value;
    }

    public static VariableTransform Set(string name, object value) => new(VariableTransformType.Set, name, value: value);
    public static VariableTransform Rename(string from, string to) => new(VariableTransformType.Rename, from, newName: to);
    public static VariableTransform Remove(string name) => new(VariableTransformType.Remove, name);

    internal void Apply(Dictionary<string, object> variables)
    {
        switch (Type)
        {
            case VariableTransformType.Set:
                variables[Name] = Value!;
                break;

            case VariableTransformType.Rename:
                if (variables.TryGetValue(Name, out var val))
                {
                    variables.Remove(Name);
                    variables[NewName!] = val;
                }
                break;

            case VariableTransformType.Remove:
                variables.Remove(Name);
                break;
        }
    }
}
