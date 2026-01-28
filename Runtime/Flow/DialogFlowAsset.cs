using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime.Flow
{
public enum DialogFlowNodeType
{
    Start,
    Dialog,
    Choice,
    Action,
    End
}

[Serializable]
public sealed class DialogFlowOutcomeData
{
    public string Outcome;
    public string TargetNodeId;
}

[Serializable]
public sealed class DialogFlowNodeData
{
    public string Id;
    public DialogFlowNodeType Type;
    public Vector2 Position;

    public DialogAsset DialogAsset;
    public string ActionId;
    public string Payload;
    public string NextNodeId;

    public List<DialogFlowOutcomeData> Outcomes = new();
    public List<DialogFlowChoiceData> Choices = new();

    public bool TryGetOutcomeTarget(string outcome, out string targetNodeId)
    {
        targetNodeId = null;
        if (Outcomes == null || string.IsNullOrWhiteSpace(outcome))
        {
            return false;
        }

        for (int i = 0; i < Outcomes.Count; i++)
        {
            var item = Outcomes[i];
            if (item != null && string.Equals(item.Outcome, outcome, StringComparison.OrdinalIgnoreCase))
            {
                targetNodeId = item.TargetNodeId;
                return !string.IsNullOrWhiteSpace(targetNodeId);
            }
        }

        return false;
    }
}

[Serializable]
public sealed class DialogFlowChoiceData
{
    public string Id;
    public string Text;
    public string TargetNodeId;
}

[CreateAssetMenu(menuName = "Dialog System/Dialog Flow", fileName = "DialogFlow")]
public sealed class DialogFlowAsset : ScriptableObject
{
    [SerializeField] private List<DialogFlowNodeData> _nodes = new();

    public List<DialogFlowNodeData> Nodes => _nodes;

    public DialogFlowNodeData GetNodeById(string id)
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

    public DialogFlowNodeData GetStartNode()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node != null && node.Type == DialogFlowNodeType.Start)
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
