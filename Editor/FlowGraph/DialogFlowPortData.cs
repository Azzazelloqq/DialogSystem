namespace DialogSystem.Editor.FlowGraph
{
public enum DialogFlowPortKind
{
    Entry,
    Next,
    Outcome,
    Choice
}

public sealed class DialogFlowPortData
{
    public DialogFlowPortKind Kind { get; }
    public int Index { get; }

    public DialogFlowPortData(DialogFlowPortKind kind, int outcomeIndex = -1)
    {
        Kind = kind;
        Index = outcomeIndex;
    }
}
}
