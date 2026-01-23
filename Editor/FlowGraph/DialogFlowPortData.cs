namespace DialogSystem.Editor.FlowGraph
{
public enum DialogFlowPortKind
{
    Entry,
    Next,
    Outcome
}

public sealed class DialogFlowPortData
{
    public DialogFlowPortKind Kind { get; }
    public int OutcomeIndex { get; }

    public DialogFlowPortData(DialogFlowPortKind kind, int outcomeIndex = -1)
    {
        Kind = kind;
        OutcomeIndex = outcomeIndex;
    }
}
}
