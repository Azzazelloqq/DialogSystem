using DialogSystem.Runtime;
using UnityEngine;

namespace DialogSystem.Example
{
public sealed class DialogExampleRunner : MonoBehaviour
{
    [SerializeField] private DialogAsset _asset;

    private DialogRunner _runner;
    private DialogContext _context;
    private DialogChoiceSet _pendingChoices;

    private void Start()
    {
        _context = new DialogContext();
        _context.RegisterFunction("GiveItem", args =>
        {
            var item = args.Count > 0 ? args[0]?.ToString() : "(unknown)";
            Debug.Log($"[Dialog] GiveItem: {item}");
            return null;
        });

        _runner = new DialogRunner(_asset, _context);
        HandleEvent(_runner.Start("main"));
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
                Debug.Log("[Dialog] End.");
                break;
            case DialogEventType.Error:
                _pendingChoices = null;
                Debug.LogError($"[Dialog] Error: {dialogEvent.Error}");
                break;
        }
    }
}
}
