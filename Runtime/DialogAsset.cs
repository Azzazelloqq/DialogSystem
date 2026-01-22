using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime
{
[CreateAssetMenu(menuName = "Dialog System/Dialog Asset", fileName = "DialogAsset")]
public sealed class DialogAsset : ScriptableObject
{
    [SerializeField] private string _sourcePath;
    [SerializeField] private List<DialogDefinition> _dialogs = new();
    [SerializeField] private List<DialogParserError> _parseErrors = new();

    public string SourcePath => _sourcePath;
    public IReadOnlyList<DialogDefinition> Dialogs => _dialogs;
    public IReadOnlyList<DialogParserError> ParseErrors => _parseErrors;

    public bool TryGetDialog(string dialogId, out DialogDefinition dialog)
    {
        dialog = null;
        if (string.IsNullOrWhiteSpace(dialogId))
        {
            return false;
        }

        for (int i = 0; i < _dialogs.Count; i++)
        {
            var candidate = _dialogs[i];
            if (candidate != null && string.Equals(candidate.Id, dialogId, StringComparison.OrdinalIgnoreCase))
            {
                dialog = candidate;
                return true;
            }
        }

        return false;
    }

    public DialogDefinition GetDefaultDialog()
    {
        return _dialogs.Count > 0 ? _dialogs[0] : null;
    }

    public void SetData(string sourcePath, List<DialogDefinition> dialogs, List<DialogParserError> errors)
    {
        _sourcePath = sourcePath;
        _dialogs = dialogs ?? new List<DialogDefinition>();
        _parseErrors = errors ?? new List<DialogParserError>();

        for (int i = 0; i < _dialogs.Count; i++)
        {
            _dialogs[i]?.BuildCaches();
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < _dialogs.Count; i++)
        {
            _dialogs[i]?.BuildCaches();
        }
    }
}
}
