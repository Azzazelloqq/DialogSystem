using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Editor
{
public enum DialogGraphNodeType
{
    Start,
    Line,
    Choice,
    Condition,
    Set,
    Command,
    Jump,
    Call,
    Return
}

[Serializable]
public sealed class DialogGraphNodeData
{
    public string Id;
    public DialogGraphNodeType Type;
    public Vector2 Position;
    public string Label;
    public string Speaker;
    public string Text;
    public string Expression;
    public string Variable;
    public string StableId;
    public List<string> Tags = new();

    public string NextNodeId;
    public string TargetNodeId;
    public string TargetOverride;
    public string TrueNodeId;
    public string FalseNodeId;

    public List<DialogGraphChoiceData> Choices = new();
}

[Serializable]
public sealed class DialogGraphChoiceData
{
    public string Id;
    public string Text;
    public string Condition;
    public string TargetNodeId;
    public string TargetOverride;
    public List<string> Tags = new();
}
}
