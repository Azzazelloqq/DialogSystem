using System;
using System.Collections.Generic;

namespace DialogSystem.Runtime.Conditions
{
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DialogConditionAttribute : Attribute
{
    public string Id { get; }
    public string DisplayName { get; }

    public DialogConditionAttribute(string id, string displayName = null)
    {
        Id = id;
        DisplayName = displayName ?? id;
    }
}

public interface IDialogCondition
{
    string Id { get; }
    string DisplayName { get; }
    bool Evaluate(DialogConditionContext context, DialogConditionArgs args);
}

public readonly struct DialogConditionArgs
{
    public string Raw { get; }
    public IReadOnlyList<string> Args { get; }

    public DialogConditionArgs(string raw, IReadOnlyList<string> args)
    {
        Raw = raw;
        Args = args;
    }
}

public readonly struct DialogConditionContext
{
    public IDialogContext Context { get; }
    public DialogLine Line { get; }
    public string DialogId { get; }

    public DialogConditionContext(IDialogContext context, DialogLine line, string dialogId)
    {
        Context = context;
        Line = line;
        DialogId = dialogId;
    }
}
}
