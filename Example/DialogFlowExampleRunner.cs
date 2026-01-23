using DialogSystem.Runtime;
using DialogSystem.Runtime.Flow;
using UnityEngine;

namespace DialogSystem.Example
{
public sealed class DialogFlowExampleRunner : MonoBehaviour, IDialogFlowActionHandler
{
    [SerializeField] private DialogFlowAsset _flow;
    [SerializeField] private DialogAsset _dialogAsset;

    private DialogFlowRunner _runner;
    private DialogChoiceSet _pendingChoices;

    private void Start()
    {
        _runner = new DialogFlowRunner(_flow, _dialogAsset);
        _runner.ActionHandler = this;
        HandleEvent(_runner.Start());
    }

    private void Update()
    {
        if (_runner == null)
        {
            return;
        }

        if (_pendingChoices != null)
        {
            for (int i = 0; i < _pendingChoices.Options.Count; i++)
            {
                var key = (KeyCode)((int)KeyCode.Alpha1 + i);
                if (Input.GetKeyDown(key))
                {
                    HandleEvent(_runner.Choose(i));
                    return;
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleEvent(_runner.Advance());
        }
    }

    private void HandleEvent(DialogEvent dialogEvent)
    {
        switch (dialogEvent.Type)
        {
            case DialogEventType.Line:
                _pendingChoices = null;
                var line = dialogEvent.Line;
                var speaker = string.IsNullOrWhiteSpace(line.Speaker) ? "Narrator" : line.Speaker;
                Debug.Log($"[{speaker}] {line.Text}");
                break;
            case DialogEventType.Choices:
                _pendingChoices = dialogEvent.Choices;
                for (int i = 0; i < _pendingChoices.Options.Count; i++)
                {
                    Debug.Log($"{i + 1}. {_pendingChoices.Options[i].Text}");
                }
                break;
            case DialogEventType.End:
                _pendingChoices = null;
                Debug.Log("[DialogFlow] End.");
                break;
            case DialogEventType.Error:
                _pendingChoices = null;
                Debug.LogError($"[DialogFlow] Error: {dialogEvent.Error}");
                break;
        }
    }

    public DialogFlowActionResult Handle(DialogFlowActionRequest request)
    {
        Debug.Log($"[DialogFlow] Action: {request.ActionId} ({request.Payload})");
        return DialogFlowActionResult.Continue(null);
    }
}
}
