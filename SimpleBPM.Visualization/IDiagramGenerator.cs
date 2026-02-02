namespace SimpleBPM.Visualization;

/// <summary>
/// Generates text-based diagram representations of process definitions and instances.
/// </summary>
public interface IDiagramGenerator
{
    /// <summary>
    /// The diagram format name (e.g., "Mermaid", "D2").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Generates a diagram from a process definition (static view).
    /// </summary>
    string Generate(ProcessDefinition definition, DiagramOptions? options = null);

    /// <summary>
    /// Generates a diagram from a process definition with instance state overlay
    /// showing the current node, completed nodes, and failed nodes.
    /// </summary>
    string Generate(ProcessDefinition definition, ProcessInstance instance, DiagramOptions? options = null);
}
