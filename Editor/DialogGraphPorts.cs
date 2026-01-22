namespace DialogSystem.Editor
{
public enum DialogPortKind
{
    Entry,
    Next,
    Default,
    Jump,
    CallTarget,
    True,
    False,
    Choice
}

public sealed class DialogPortData
{
    public DialogPortKind Kind { get; }
    public int ChoiceIndex { get; }

    public DialogPortData(DialogPortKind kind, int choiceIndex = -1)
    {
        Kind = kind;
        ChoiceIndex = choiceIndex;
    }
}
}
