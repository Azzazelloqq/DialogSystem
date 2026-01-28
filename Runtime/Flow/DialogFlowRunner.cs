using System;
using System.Collections.Generic;
using DialogSystem.Runtime;

namespace DialogSystem.Runtime.Flow
{
public interface IDialogFlowActionHandler
{
    DialogFlowActionResult Handle(DialogFlowActionRequest request);
}

public readonly struct DialogFlowActionRequest
{
    public string ActionId { get; }
    public string Payload { get; }
    public DialogFlowContext Context { get; }

    public DialogFlowActionRequest(string actionId, string payload, DialogFlowContext context)
    {
        ActionId = actionId;
        Payload = payload;
        Context = context;
    }
}

public readonly struct DialogFlowActionResult
{
    public bool Handled { get; }
    public string NextNodeId { get; }
    public bool EndFlow { get; }

    public DialogFlowActionResult(bool handled, string nextNodeId, bool endFlow)
    {
        Handled = handled;
        NextNodeId = nextNodeId;
        EndFlow = endFlow;
    }

    public static DialogFlowActionResult Continue(string nextNodeId) => new(true, nextNodeId, false);
    public static DialogFlowActionResult End() => new(true, null, true);
}

public readonly struct DialogFlowContext
{
    public string CurrentNodeId { get; }
    public string CurrentDialogId { get; }
    public string LastOutcome { get; }
    public IDialogContext DialogContext { get; }

    public DialogFlowContext(string currentNodeId, string currentDialogId, string lastOutcome, IDialogContext dialogContext)
    {
        CurrentNodeId = currentNodeId;
        CurrentDialogId = currentDialogId;
        LastOutcome = lastOutcome;
        DialogContext = dialogContext;
    }
}

public sealed class DialogFlowRunner
{
    private readonly DialogFlowAsset _flow;
    private readonly DialogRunner _dialogRunner;
    private readonly IDialogContext _context;
    private readonly DialogAsset _defaultDialogAsset;
    private string _currentNodeId;
    private string _lastOutcome;
    private List<DialogChoiceOption> _pendingFlowChoices;

    public IDialogFlowActionHandler ActionHandler { get; set; }
    public string LastError { get; private set; }
    public string CurrentNodeId => _currentNodeId;
    public string CurrentDialogId => _dialogRunner.CurrentDialog?.Id;
    public string LastOutcome => _lastOutcome;
    public bool IsWaitingForFlowChoice => _pendingFlowChoices != null;

    public DialogFlowNodeType? CurrentNodeType
    {
        get
        {
            var node = _flow?.GetNodeById(_currentNodeId);
            return node?.Type;
        }
    }

    public DialogFlowRunner(DialogFlowAsset flow, DialogAsset dialogAsset = null, IDialogContext context = null)
    {
        _flow = flow;
        _defaultDialogAsset = dialogAsset;
        _context = context ?? new DialogContext();
        _dialogRunner = new DialogRunner(dialogAsset, _context);
    }

    public DialogEvent Start()
    {
        LastError = null;
        _currentNodeId = null;
        _lastOutcome = null;
        _pendingFlowChoices = null;

        if (_flow == null)
        {
            return SetError("Dialog flow asset is null.");
        }

        var startNode = _flow.GetStartNode();
        if (startNode == null || string.IsNullOrWhiteSpace(startNode.NextNodeId))
        {
            return SetError("Start node is missing or has no entry.");
        }

        _currentNodeId = startNode.NextNodeId;
        return EnterNode();
    }

    public DialogEvent Advance()
    {
        if (string.IsNullOrWhiteSpace(_currentNodeId))
        {
            return DialogEvent.EndEvent();
        }

        if (_pendingFlowChoices != null)
        {
            return SetError("Flow choice selection is required before continuing.");
        }

        var dialogEvent = _dialogRunner.Advance();
        return HandleDialogEvent(dialogEvent);
    }

    public DialogEvent Choose(int index)
    {
        if (_pendingFlowChoices != null)
        {
            if (index < 0 || index >= _pendingFlowChoices.Count)
            {
                return SetError("Flow choice index is out of range.");
            }

            var choice = _pendingFlowChoices[index];
            _pendingFlowChoices = null;
            _currentNodeId = choice.Target;
            if (string.IsNullOrWhiteSpace(_currentNodeId))
            {
                return DialogEvent.EndEvent();
            }

            return EnterNode();
        }

        var dialogEvent = _dialogRunner.Choose(index);
        return HandleDialogEvent(dialogEvent);
    }

    private DialogEvent EnterNode()
    {
        var node = _flow.GetNodeById(_currentNodeId);
        if (node == null)
        {
            return SetError("Flow node not found.");
        }

        switch (node.Type)
        {
            case DialogFlowNodeType.Dialog:
                var dialogAsset = node.DialogAsset != null ? node.DialogAsset : _defaultDialogAsset;
                if (dialogAsset == null)
                {
                    return SetError("Dialog node has no dialog asset.");
                }

                _dialogRunner.SetAsset(dialogAsset);
                return HandleDialogEvent(_dialogRunner.Start(null));
            case DialogFlowNodeType.Choice:
                return HandleChoiceNode(node);
            case DialogFlowNodeType.Action:
                return HandleActionNode(node);
            case DialogFlowNodeType.End:
                _currentNodeId = null;
                return DialogEvent.EndEvent();
            default:
                return SetError($"Unsupported node type '{node.Type}'.");
        }
    }

    private DialogEvent HandleChoiceNode(DialogFlowNodeData node)
    {
        if (node.Choices == null || node.Choices.Count == 0)
        {
            return SetError("Choice node has no options.");
        }

        _pendingFlowChoices = new List<DialogChoiceOption>();
        for (int i = 0; i < node.Choices.Count; i++)
        {
            var choice = node.Choices[i];
            if (choice == null)
            {
                continue;
            }

            _pendingFlowChoices.Add(new DialogChoiceOption(choice.Text ?? string.Empty, null, null, choice.Id,
                choice.TargetNodeId));
        }

        if (_pendingFlowChoices.Count == 0)
        {
            return SetError("Choice node has no valid options.");
        }

        return DialogEvent.ChoicesEvent(new DialogChoiceSet(_pendingFlowChoices));
    }

    private DialogEvent HandleDialogEvent(DialogEvent dialogEvent)
    {
        if (dialogEvent.Type != DialogEventType.Outcome)
        {
            return dialogEvent;
        }

        _lastOutcome = dialogEvent.Outcome;
        var currentNode = _flow.GetNodeById(_currentNodeId);
        if (currentNode == null)
        {
            return SetError("Flow node not found.");
        }

        if (!currentNode.TryGetOutcomeTarget(_lastOutcome, out var nextNodeId))
        {
            _currentNodeId = null;
            return DialogEvent.EndEvent();
        }

        _currentNodeId = nextNodeId;
        return EnterNode();
    }

    private DialogEvent HandleActionNode(DialogFlowNodeData node)
    {
        if (string.IsNullOrWhiteSpace(node.ActionId))
        {
            return SetError("Action node has empty action id.");
        }

        var context = new DialogFlowContext(node.Id, _dialogRunner.CurrentDialog?.Id, _lastOutcome, _context);
        var request = new DialogFlowActionRequest(node.ActionId, node.Payload, context);

        if (ActionHandler == null)
        {
            return SetError("Action handler is not set.");
        }

        var result = ActionHandler.Handle(request);
        if (!result.Handled)
        {
            return SetError($"Action '{node.ActionId}' was not handled.");
        }

        if (result.EndFlow)
        {
            _currentNodeId = null;
            return DialogEvent.EndEvent();
        }

        _currentNodeId = !string.IsNullOrWhiteSpace(result.NextNodeId)
            ? result.NextNodeId
            : node.NextNodeId;

        if (string.IsNullOrWhiteSpace(_currentNodeId))
        {
            return DialogEvent.EndEvent();
        }

        return EnterNode();
    }

    private DialogEvent SetError(string message)
    {
        LastError = message;
        return DialogEvent.ErrorEvent(message);
    }
}
}
