using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Editor
{
[CreateAssetMenu(menuName = "Dialog System/Dialog Graph", fileName = "DialogGraph")]
public sealed class DialogGraphAsset : ScriptableObject
{
    [SerializeField] private string _dialogId = "main";
    [SerializeField] private string _dslPath;
    [SerializeField] private List<DialogGraphNodeData> _nodes = new();

    public string DialogId
    {
        get => _dialogId;
        set => _dialogId = value;
    }

    public string DslPath
    {
        get => _dslPath;
        set => _dslPath = value;
    }

    public List<DialogGraphNodeData> Nodes => _nodes;

    public DialogGraphNodeData GetNodeById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node != null && string.Equals(node.Id, id, StringComparison.Ordinal))
            {
                return node;
            }
        }

        return null;
    }

    public void RemoveNode(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        for (int i = _nodes.Count - 1; i >= 0; i--)
        {
            if (_nodes[i] != null && string.Equals(_nodes[i].Id, id, StringComparison.Ordinal))
            {
                _nodes.RemoveAt(i);
            }
        }
    }
}
}
