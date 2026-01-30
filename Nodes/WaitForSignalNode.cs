namespace SimpleBPM.Nodes;

public class WaitForSignalNode : ProcessNode
{
    public string SignalName { get; set; }

    public WaitForSignalNode(string signalName) : base(NodeType.WaitForSignal)
    {
        SignalName = signalName;
    }
}
