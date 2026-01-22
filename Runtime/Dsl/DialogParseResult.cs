using System.Collections.Generic;
using DialogSystem.Runtime;

namespace DialogSystem.Runtime.Dsl
{
public sealed class DialogParseResult
{
    public string Source { get; }
    public List<DialogDefinition> Dialogs { get; } = new();
    public List<DialogParserError> Errors { get; } = new();

    public bool HasErrors => Errors.Count > 0;

    public DialogParseResult(string source)
    {
        Source = source;
    }

    public void AddError(int line, string message, string context)
    {
        Errors.Add(new DialogParserError(line, message, context));
    }
}
}
