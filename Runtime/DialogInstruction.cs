using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime
{
public enum DialogInstructionType
{
    Line,
    ChoiceGroup,
    If,
    Jump,
    Call,
    Return,
    Set,
    Command
}

[Serializable]
public sealed class DialogInstruction
{
    [SerializeField] private DialogInstructionType _type;
    [SerializeField] private string _speaker;
    [SerializeField] private string _text;
    [SerializeField] private string _expression;
    [SerializeField] private string _variable;
    [SerializeField] private string _target;
    [SerializeField] private int _jumpIndex = -1;
    [SerializeField] private List<DialogChoice> _choices = new();
    [SerializeField] private List<string> _tags = new();
    [SerializeField] private string _id;
    [SerializeField] private bool _internal;

    public DialogInstructionType Type => _type;
    public string Speaker => _speaker;
    public string Text => _text;
    public string Expression => _expression;
    public string Variable => _variable;
    public string Target => _target;
    public int JumpIndex => _jumpIndex;
    public IReadOnlyList<DialogChoice> Choices => _choices;
    public IReadOnlyList<string> Tags => _tags;
    public string Id => _id;
    public bool IsInternal => _internal;

    public static DialogInstruction Line(string speaker, string text, string id, List<string> tags)
    {
        return new DialogInstruction
        {
            _type = DialogInstructionType.Line,
            _speaker = speaker,
            _text = text,
            _id = id,
            _tags = tags ?? new List<string>()
        };
    }

    public static DialogInstruction ChoiceGroup(List<DialogChoice> choices)
    {
        return new DialogInstruction
        {
            _type = DialogInstructionType.ChoiceGroup,
            _choices = choices ?? new List<DialogChoice>()
        };
    }

    public static DialogInstruction If(string expression, int jumpIndex, bool isInternal)
    {
        return new DialogInstruction
        {
            _type = DialogInstructionType.If,
            _expression = expression,
            _jumpIndex = jumpIndex,
            _internal = isInternal
        };
    }

    public static DialogInstruction Jump(string target, int jumpIndex, bool isInternal)
    {
        return new DialogInstruction
        {
            _type = DialogInstructionType.Jump,
            _target = target,
            _jumpIndex = jumpIndex,
            _internal = isInternal
        };
    }

    public static DialogInstruction Call(string target)
    {
        return new DialogInstruction
        {
            _type = DialogInstructionType.Call,
            _target = target
        };
    }

    public static DialogInstruction Return()
    {
        return new DialogInstruction
        {
            _type = DialogInstructionType.Return
        };
    }

    public static DialogInstruction Set(string variable, string expression)
    {
        return new DialogInstruction
        {
            _type = DialogInstructionType.Set,
            _variable = variable,
            _expression = expression
        };
    }

    public static DialogInstruction Command(string expression)
    {
        return new DialogInstruction
        {
            _type = DialogInstructionType.Command,
            _expression = expression
        };
    }
}

[Serializable]
public sealed class DialogChoice
{
    [SerializeField] private string _text;
    [SerializeField] private string _condition;
    [SerializeField] private string _target;
    [SerializeField] private List<string> _tags = new();
    [SerializeField] private string _id;

    public string Text => _text;
    public string Condition => _condition;
    public string Target => _target;
    public IReadOnlyList<string> Tags => _tags;
    public string Id => _id;

    public DialogChoice(string text, string condition, string target, string id, List<string> tags)
    {
        _text = text;
        _condition = condition;
        _target = target;
        _id = id;
        _tags = tags ?? new List<string>();
    }
}

[Serializable]
public sealed class DialogLabel
{
    [SerializeField] private string _name;
    [SerializeField] private int _instructionIndex;

    public string Name => _name;
    public int InstructionIndex => _instructionIndex;

    public DialogLabel(string name, int instructionIndex)
    {
        _name = name;
        _instructionIndex = instructionIndex;
    }
}
}
