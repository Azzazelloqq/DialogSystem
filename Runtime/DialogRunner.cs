using System;
using System.Collections.Generic;
using DialogSystem.Runtime.Conditions;
using DialogSystem.Runtime.Expressions;

namespace DialogSystem.Runtime
{
public sealed class DialogRunner
{
    private DialogAsset _asset;
    private DialogDefinition _currentDialog;
    private int _instructionIndex;
    private readonly IDialogContext _context;
    private readonly List<DialogCallFrame> _callStack = new();
    private List<DialogChoiceOption> _pendingChoices;
    private int _choiceInstructionIndex = -1;

    public string LastError { get; private set; }
    public DialogDefinition CurrentDialog => _currentDialog;

    public DialogRunner(DialogAsset asset, IDialogContext context = null)
    {
        _asset = asset;
        _context = context ?? new DialogContext();
    }

    public void SetAsset(DialogAsset asset)
    {
        _asset = asset;
    }

    public DialogEvent Start(string dialogId = null)
    {
        ClearState();
        if (_asset == null)
        {
            return SetError("Dialog asset is null.");
        }

        if (_context is DialogContext dialogContext)
        {
            dialogContext.RegisterDialogAsset(_asset);
        }

        _currentDialog = !string.IsNullOrWhiteSpace(dialogId) && _asset.TryGetDialog(dialogId, out var dialog)
            ? dialog
            : _asset.GetDefaultDialog();

        if (_currentDialog == null)
        {
            return SetError("Dialog not found.");
        }

        _instructionIndex = _currentDialog.EntryIndex;
        return Advance();
    }

    public DialogEvent Advance()
    {
        LastError = null;

        if (_pendingChoices != null)
        {
            return SetError("Choice selection is required before continuing.");
        }

        while (true)
        {
            if (_currentDialog == null)
            {
                return DialogEvent.EndEvent();
            }

            if (_instructionIndex < 0 || _instructionIndex >= _currentDialog.Instructions.Count)
            {
                return DialogEvent.EndEvent();
            }

            var instruction = _currentDialog.Instructions[_instructionIndex];
            if (instruction == null)
            {
                _instructionIndex++;
                continue;
            }

            switch (instruction.Type)
            {
                case DialogInstructionType.Line:
                    var lineData = new DialogLine(instruction.Speaker, instruction.Text, instruction.Tags, instruction.Id);
                    if (!string.IsNullOrWhiteSpace(instruction.Condition))
                    {
                        if (!TryEvaluateCondition(instruction.Condition, lineData, out var lineAllowed, out var lineError))
                        {
                            return SetError(lineError);
                        }

                        if (!lineAllowed)
                        {
                            _instructionIndex++;
                            continue;
                        }
                    }

                    _instructionIndex++;
                    return DialogEvent.LineEvent(lineData);
                case DialogInstructionType.ChoiceGroup:
                    if (!TryBuildChoices(instruction, out var choices, out var choiceError))
                    {
                        return SetError(choiceError);
                    }

                    if (choices.Count == 0)
                    {
                        _instructionIndex++;
                        continue;
                    }

                    _pendingChoices = choices;
                    _choiceInstructionIndex = _instructionIndex;
                    return DialogEvent.ChoicesEvent(new DialogChoiceSet(choices));
                case DialogInstructionType.If:
                    if (!TryEvaluateCondition(instruction.Expression, default, out var condition, out var conditionError))
                    {
                        return SetError(conditionError);
                    }

                    _instructionIndex = condition ? _instructionIndex + 1 : instruction.JumpIndex;
                    continue;
                case DialogInstructionType.Jump:
                    if (instruction.JumpIndex >= 0 && string.IsNullOrWhiteSpace(instruction.Target))
                    {
                        _instructionIndex = instruction.JumpIndex;
                        continue;
                    }

                    if (!TryJumpToTarget(instruction.Target, out conditionError))
                    {
                        return SetError(conditionError);
                    }

                    continue;
                case DialogInstructionType.Call:
                    if (!TryCallTarget(instruction.Target, out conditionError))
                    {
                        return SetError(conditionError);
                    }

                    continue;
                case DialogInstructionType.Return:
                    if (_callStack.Count == 0)
                    {
                        return DialogEvent.EndEvent();
                    }

                    var frame = _callStack[^1];
                    _callStack.RemoveAt(_callStack.Count - 1);
                    if (!TryRestoreFrame(frame, out conditionError))
                    {
                        return SetError(conditionError);
                    }

                    continue;
                case DialogInstructionType.Set:
                    if (!TryEvaluateExpression(instruction.Expression, out var value, out conditionError))
                    {
                        return SetError(conditionError);
                    }

                    _context.SetVariable(instruction.Variable, value);
                    _instructionIndex++;
                    continue;
                case DialogInstructionType.Command:
                    if (!TryEvaluateExpression(instruction.Expression, out _, out conditionError))
                    {
                        return SetError(conditionError);
                    }

                    _instructionIndex++;
                    continue;
                case DialogInstructionType.Outcome:
                    _instructionIndex = _currentDialog.Instructions.Count;
                    return DialogEvent.OutcomeEvent(instruction.Outcome);
                default:
                    _instructionIndex++;
                    continue;
            }
        }
    }

    public DialogEvent Choose(int index)
    {
        LastError = null;
        if (_pendingChoices == null)
        {
            return SetError("No pending choices.");
        }

        if (index < 0 || index >= _pendingChoices.Count)
        {
            return SetError("Choice index is out of range.");
        }

        var choice = _pendingChoices[index];
        _pendingChoices = null;
        var target = choice.Target;

        if (!string.IsNullOrWhiteSpace(target))
        {
            if (TryGetOutcomeFromTarget(target, out var outcome))
            {
                _instructionIndex = _currentDialog.Instructions.Count;
                return DialogEvent.OutcomeEvent(outcome);
            }

            if (!TryJumpToTarget(target, out var error))
            {
                return SetError(error);
            }
        }
        else
        {
            _instructionIndex = _choiceInstructionIndex + 1;
        }

        _choiceInstructionIndex = -1;
        return Advance();
    }

    public DialogState CaptureState()
    {
        var callStackCopy = new List<DialogCallFrame>(_callStack.Count);
        foreach (var frame in _callStack)
        {
            callStackCopy.Add(new DialogCallFrame(frame.DialogId, frame.InstructionIndex));
        }

        var pending = new List<DialogChoiceState>();
        if (_pendingChoices != null)
        {
            foreach (var choice in _pendingChoices)
            {
                var tags = choice.Tags == null ? null : new List<string>(choice.Tags);
                pending.Add(new DialogChoiceState(choice.Text, choice.Target, choice.Id, tags));
            }
        }

        return new DialogState(_currentDialog?.Id, _instructionIndex, _pendingChoices != null, callStackCopy, pending);
    }

    public bool RestoreState(DialogState state, out string error)
    {
        error = null;
        if (state == null)
        {
            error = "Dialog state is null.";
            return false;
        }

        if (_asset == null || !_asset.TryGetDialog(state.DialogId, out _currentDialog))
        {
            error = $"Dialog '{state.DialogId}' not found.";
            return false;
        }

        _instructionIndex = state.InstructionIndex;
        _callStack.Clear();
        foreach (var frame in state.CallStack)
        {
            _callStack.Add(new DialogCallFrame(frame.DialogId, frame.InstructionIndex));
        }

        if (state.IsWaitingForChoice)
        {
            _pendingChoices = new List<DialogChoiceOption>();
            foreach (var choice in state.PendingChoices)
            {
                _pendingChoices.Add(new DialogChoiceOption(choice.Text, choice.Tags, choice.Id, choice.Target));
            }

            _choiceInstructionIndex = _instructionIndex;
        }
        else
        {
            _pendingChoices = null;
            _choiceInstructionIndex = -1;
        }

        return true;
    }

    private void ClearState()
    {
        LastError = null;
        _currentDialog = null;
        _instructionIndex = 0;
        _callStack.Clear();
        _pendingChoices = null;
        _choiceInstructionIndex = -1;
    }

    private DialogEvent SetError(string message)
    {
        LastError = message;
        return DialogEvent.ErrorEvent(message);
    }

    private bool TryBuildChoices(DialogInstruction instruction, out List<DialogChoiceOption> choices, out string error)
    {
        choices = new List<DialogChoiceOption>();
        error = null;
        foreach (var choice in instruction.Choices)
        {
            if (choice == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(choice.Condition))
            {
            if (!TryEvaluateCondition(choice.Condition, default, out var allowed, out error))
                {
                    return false;
                }

                if (!allowed)
                {
                    continue;
                }
            }

            choices.Add(new DialogChoiceOption(choice.Text, choice.Tags, choice.Id, choice.Target));
        }

        return true;
    }

    private bool TryEvaluateCondition(string conditionText, DialogLine line, out bool result, out string error)
    {
        result = false;
        error = null;
        if (DialogConditionRegistry.TryEvaluate(conditionText,
                new DialogConditionContext(_context, line, _currentDialog?.Id), out result, out error))
        {
            return true;
        }

        if (error != null)
        {
            return false;
        }

        if (!DialogExpressionCache.TryGet(conditionText, out var compiled, out error))
        {
            return false;
        }

        result = compiled.EvaluateBool(_context, out error);
        return error == null;
    }

    private bool TryEvaluateExpression(string expression, out object value, out string error)
    {
        value = null;
        if (!DialogExpressionCache.TryGet(expression, out var compiled, out error))
        {
            return false;
        }

        value = compiled.Evaluate(_context, out error);
        return error == null;
    }

    private static bool TryGetOutcomeFromTarget(string target, out string outcome)
    {
        outcome = null;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var trimmed = target.Trim();
        const string exitPrefix = "exit:";
        const string outcomePrefix = "outcome:";
        if (trimmed.StartsWith(exitPrefix, StringComparison.OrdinalIgnoreCase))
        {
            outcome = trimmed.Substring(exitPrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(outcome);
        }

        if (trimmed.StartsWith(outcomePrefix, StringComparison.OrdinalIgnoreCase))
        {
            outcome = trimmed.Substring(outcomePrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(outcome);
        }

        return false;
    }

    private bool TryCallTarget(string target, out string error)
    {
        if (!TryResolveTarget(target, out var dialog, out var index, out error))
        {
            return false;
        }

        _callStack.Add(new DialogCallFrame(_currentDialog.Id, _instructionIndex + 1));
        _currentDialog = dialog;
        _instructionIndex = index;
        return true;
    }

    private bool TryJumpToTarget(string target, out string error)
    {
        if (!TryResolveTarget(target, out var dialog, out var index, out error))
        {
            return false;
        }

        _currentDialog = dialog;
        _instructionIndex = index;
        return true;
    }

    private bool TryResolveTarget(string target, out DialogDefinition dialog, out int index, out string error)
    {
        dialog = null;
        index = -1;
        error = null;

        if (!DialogTarget.TryParse(target, _currentDialog?.Id, out var parsed))
        {
            error = $"Invalid target '{target}'.";
            return false;
        }

        if (_currentDialog != null && parsed.DialogId == _currentDialog.Id)
        {
            dialog = _currentDialog;
        }
        else if (_asset != null && _asset.TryGetDialog(parsed.DialogId, out dialog))
        {
        }
        else if (_context != null && _context.TryResolveDialog(parsed.DialogId, out dialog))
        {
        }
        else
        {
            error = $"Dialog '{parsed.DialogId}' not found.";
            return false;
        }

        if (!dialog.TryGetLabelIndex(parsed.LabelId, out index))
        {
            error = $"Label '{parsed.LabelId}' not found in dialog '{dialog.Id}'.";
            return false;
        }

        return true;
    }

    private bool TryRestoreFrame(DialogCallFrame frame, out string error)
    {
        error = null;
        if (frame == null)
        {
            error = "Call frame is null.";
            return false;
        }

        if (_asset == null || !_asset.TryGetDialog(frame.DialogId, out var dialog))
        {
            if (_context == null || !_context.TryResolveDialog(frame.DialogId, out dialog))
            {
                error = $"Dialog '{frame.DialogId}' not found.";
                return false;
            }
        }

        _currentDialog = dialog;
        _instructionIndex = frame.InstructionIndex;
        return true;
    }
}
}
