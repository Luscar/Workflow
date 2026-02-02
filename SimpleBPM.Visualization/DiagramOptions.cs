namespace SimpleBPM.Visualization;

public class DiagramOptions
{
    /// <summary>
    /// Direction of the diagram layout.
    /// </summary>
    public DiagramDirection Direction { get; set; } = DiagramDirection.TopToBottom;

    /// <summary>
    /// Whether to show the process name as a title.
    /// </summary>
    public bool ShowTitle { get; set; } = true;

    /// <summary>
    /// Whether to show node type details (command name, signal name, etc.) as labels.
    /// </summary>
    public bool ShowNodeDetails { get; set; } = true;

    /// <summary>
    /// Whether to show decision route condition labels on edges.
    /// </summary>
    public bool ShowConditionLabels { get; set; } = true;

    /// <summary>
    /// Whether to include a legend explaining node type shapes/colors.
    /// </summary>
    public bool ShowLegend { get; set; } = false;

    /// <summary>
    /// When rendering with a ProcessInstance, whether to show execution
    /// history information (step count, durations) as annotations.
    /// </summary>
    public bool ShowExecutionInfo { get; set; } = false;
}

public enum DiagramDirection
{
    TopToBottom,
    LeftToRight
}
