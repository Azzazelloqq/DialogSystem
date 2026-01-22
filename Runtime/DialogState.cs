using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime
{
[Serializable]
public sealed class DialogState
{
    [SerializeField] private string _dialogId;
    [SerializeField] private int _instructionIndex;
    [SerializeField] private bool _isWaitingForChoice;
    [SerializeField] private List<DialogCallFrame> _callStack = new();
    [SerializeField] private List<DialogChoiceState> _pendingChoices = new();

    public string DialogId => _dialogId;
    public int InstructionIndex => _instructionIndex;
    public bool IsWaitingForChoice => _isWaitingForChoice;
    public IReadOnlyList<DialogCallFrame> CallStack => _callStack;
    public IReadOnlyList<DialogChoiceState> PendingChoices => _pendingChoices;

    public DialogState(string dialogId, int instructionIndex, bool isWaitingForChoice,
        List<DialogCallFrame> callStack, List<DialogChoiceState> pendingChoices)
    {
        _dialogId = dialogId;
        _instructionIndex = instructionIndex;
        _isWaitingForChoice = isWaitingForChoice;
        _callStack = callStack ?? new List<DialogCallFrame>();
        _pendingChoices = pendingChoices ?? new List<DialogChoiceState>();
    }
}

[Serializable]
public sealed class DialogCallFrame
{
    [SerializeField] private string _dialogId;
    [SerializeField] private int _instructionIndex;

    public string DialogId => _dialogId;
    public int InstructionIndex => _instructionIndex;

    public DialogCallFrame(string dialogId, int instructionIndex)
    {
        _dialogId = dialogId;
        _instructionIndex = instructionIndex;
    }
}

[Serializable]
public sealed class DialogChoiceState
{
    [SerializeField] private string _text;
    [SerializeField] private string _target;
    [SerializeField] private string _id;
    [SerializeField] private List<string> _tags = new();

    public string Text => _text;
    public string Target => _target;
    public string Id => _id;
    public IReadOnlyList<string> Tags => _tags;

    public DialogChoiceState(string text, string target, string id, List<string> tags)
    {
        _text = text;
        _target = target;
        _id = id;
        _tags = tags ?? new List<string>();
    }
}
}
