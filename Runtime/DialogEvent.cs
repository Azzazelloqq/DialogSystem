using System.Collections.Generic;

namespace DialogSystem.Runtime
{
public enum DialogEventType
{
    Line,
    Choices,
    Outcome,
    End,
    Error
}

public readonly struct DialogLine
{
    public readonly string Speaker;
    public readonly string Text;
    public readonly IReadOnlyList<string> Tags;
    public readonly string Id;

    public DialogLine(string speaker, string text, IReadOnlyList<string> tags, string id)
    {
        Speaker = speaker;
        Text = text;
        Tags = tags;
        Id = id;
    }
}

public readonly struct DialogChoiceOption
{
    public readonly string Text;
    public readonly IReadOnlyList<string> Tags;
    public readonly string Id;
    public readonly string Target;

    public DialogChoiceOption(string text, IReadOnlyList<string> tags, string id, string target)
    {
        Text = text;
        Tags = tags;
        Id = id;
        Target = target;
    }
}

public sealed class DialogChoiceSet
{
    public IReadOnlyList<DialogChoiceOption> Options { get; }

    public DialogChoiceSet(IReadOnlyList<DialogChoiceOption> options)
    {
        Options = options;
    }
}

public readonly struct DialogEvent
{
    public readonly DialogEventType Type;
    public readonly DialogLine Line;
    public readonly DialogChoiceSet Choices;
    public readonly string Outcome;
    public readonly string Error;

    private DialogEvent(DialogEventType type, DialogLine line, DialogChoiceSet choices, string outcome, string error)
    {
        Type = type;
        Line = line;
        Choices = choices;
        Outcome = outcome;
        Error = error;
    }

    public static DialogEvent LineEvent(DialogLine line) => new(DialogEventType.Line, line, null, null, null);
    public static DialogEvent ChoicesEvent(DialogChoiceSet choices) => new(DialogEventType.Choices, default, choices, null, null);
    public static DialogEvent OutcomeEvent(string outcome) => new(DialogEventType.Outcome, default, null, outcome, null);
    public static DialogEvent EndEvent() => new(DialogEventType.End, default, null, null, null);
    public static DialogEvent ErrorEvent(string error) => new(DialogEventType.Error, default, null, null, error);
}
}
