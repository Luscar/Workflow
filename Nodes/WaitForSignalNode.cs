namespace SimpleBPM.Nodes;

public class WaitForSignalNode : NodeDefinition
{
    public string SignalName { get; set; }

    public WaitForSignalNode(string signalName) : base(NodeType.WaitForSignal)
    {
        SignalName = signalName;
    }
}
