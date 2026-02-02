namespace SimpleBPM.Visualization;

/// <summary>
/// Extension methods for generating diagrams directly from ProcessDefinition and ProcessInstance.
/// </summary>
public static class ProcessDiagramExtensions
{
    private static readonly MermaidDiagramGenerator MermaidGenerator = new();
    private static readonly D2DiagramGenerator D2Generator = new();

    /// <summary>
    /// Generates a Mermaid flowchart diagram of the process definition.
    /// </summary>
    public static string ToMermaid(this ProcessDefinition definition, DiagramOptions? options = null)
    {
        return MermaidGenerator.Generate(definition, options);
    }

    /// <summary>
    /// Generates a Mermaid flowchart diagram showing the current state of a process instance.
    /// </summary>
    public static string ToMermaid(this ProcessDefinition definition, ProcessInstance instance, DiagramOptions? options = null)
    {
        return MermaidGenerator.Generate(definition, instance, options);
    }

    /// <summary>
    /// Generates a D2 diagram of the process definition.
    /// </summary>
    public static string ToD2(this ProcessDefinition definition, DiagramOptions? options = null)
    {
        return D2Generator.Generate(definition, options);
    }

    /// <summary>
    /// Generates a D2 diagram showing the current state of a process instance.
    /// </summary>
    public static string ToD2(this ProcessDefinition definition, ProcessInstance instance, DiagramOptions? options = null)
    {
        return D2Generator.Generate(definition, instance, options);
    }

    /// <summary>
    /// Writes a diagram to a file. The format is determined by file extension:
    /// .mmd / .mermaid → Mermaid, .d2 → D2. Defaults to Mermaid.
    /// </summary>
    public static void WriteDiagramToFile(this ProcessDefinition definition, string filePath,
        ProcessInstance? instance = null, DiagramOptions? options = null)
    {
        var generator = ResolveGeneratorFromExtension(filePath);
        var content = instance != null
            ? generator.Generate(definition, instance, options)
            : generator.Generate(definition, options);

        File.WriteAllText(filePath, content);
    }

    private static IDiagramGenerator ResolveGeneratorFromExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".d2" => D2Generator,
            _ => MermaidGenerator
        };
    }
}
