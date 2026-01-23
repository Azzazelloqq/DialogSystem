using System;
using System.Collections.Generic;

namespace DialogSystem.Editor.Dsl
{
public enum DialogDslBlockType
{
    Line,
    ChoiceGroup,
    ConditionGroup,
    Exit,
    Raw
}

[Serializable]
public sealed class DialogDslDocument
{
    public string DialogId = "main";
    public List<DialogDslBlock> Blocks = new();
}

[Serializable]
public sealed class DialogDslBlock
{
    public string Id;
    public DialogDslBlockType Type;
    public string Speaker;
    public string Text;
    public string Condition;
    public string Outcome;
    public string StableId;
    public List<string> Tags = new();
    public List<DialogDslChoice> Choices = new();
    public List<DialogDslBlock> Children = new();
    public string Raw;
}

[Serializable]
public sealed class DialogDslChoice
{
    public string Text;
    public string Condition;
    public string Outcome;
    public string StableId;
    public List<string> Tags = new();
}
}
