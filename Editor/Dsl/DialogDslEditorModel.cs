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
    public bool IsCollapsed;
    public string Speaker;
    public string Text;
    public string TextKey;
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
    public string Id;
    public string Text;
    public string TextKey;
    public string Condition;
    public string Outcome;
    public string StableId;
    public List<string> Tags = new();
}

[Serializable]
public sealed class DialogLocalizationVariant
{
    public string Locale;
    public List<DialogLocalizedLine> Lines = new();
    public List<DialogLocalizedChoice> Choices = new();
}

[Serializable]
public sealed class DialogLocalizedLine
{
    public string BlockId;
    public string Speaker;
    public string Text;
}

[Serializable]
public sealed class DialogLocalizedChoice
{
    public string ChoiceId;
    public string Text;
}
}
