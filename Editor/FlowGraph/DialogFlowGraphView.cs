using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Flow;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.Editor.FlowGraph
{
public sealed class DialogFlowGraphView : GraphView
{
    private DialogFlowAsset _asset;
    private readonly Dictionary<string, DialogFlowNodeView> _nodeViews = new();

    public DialogFlowGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        Insert(0, new GridBackground());
        graphViewChanged = OnGraphViewChanged;
    }

    public void Load(DialogFlowAsset asset)
    {
        _asset = asset;
        Rebuild();
    }

    public void CreateNode(DialogFlowNodeType type)
    {
        if (_asset == null)
        {
            return;
        }

        var position = contentViewContainer.WorldToLocal(worldBound.center);
        CreateNode(type, position);
    }

    internal void RecordUndo(string action)
    {
        if (_asset == null)
        {
            return;
        }

        Undo.RecordObject(_asset, action);
        EditorUtility.SetDirty(_asset);
    }

    internal void RemoveConnections(Port port)
    {
        if (port == null)
        {
            return;
        }

        var edges = port.connections.ToList();
        if (edges.Count == 0)
        {
            return;
        }

        DeleteElements(edges);
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList()
            .Where(port => port != startPort &&
                           port.node != startPort.node &&
                           port.direction != startPort.direction)
            .ToList();
    }

    private void CreateNode(DialogFlowNodeType type, Vector2 position)
    {
        if (type == DialogFlowNodeType.Start)
        {
            return;
        }

        RecordUndo("Create Flow Node");
        var node = new DialogFlowNodeData
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            Position = position
        };

        _asset.Nodes.Add(node);
        var view = CreateNodeView(node);
        AddElement(view);
        _nodeViews[node.Id] = view;
    }

    private void Rebuild()
    {
        ClearGraph();
        _nodeViews.Clear();

        if (_asset == null)
        {
            return;
        }

        EnsureStartNode();

        foreach (var node in _asset.Nodes)
        {
            if (node == null)
            {
                continue;
            }

            var view = CreateNodeView(node);
            AddElement(view);
            _nodeViews[node.Id] = view;
        }

        ConnectEdges();
    }

    private DialogFlowNodeView CreateNodeView(DialogFlowNodeData data)
    {
        var view = new DialogFlowNodeView(data, this);
        view.SetPosition(new Rect(data.Position, view.DefaultSize));
        return view;
    }

    private void EnsureStartNode()
    {
        var hasStart = _asset.Nodes.Any(node => node != null && node.Type == DialogFlowNodeType.Start);
        if (hasStart)
        {
            return;
        }

        RecordUndo("Create Start Node");
        var start = new DialogFlowNodeData
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DialogFlowNodeType.Start,
            Position = Vector2.zero
        };

        _asset.Nodes.Add(start);
    }

    private void ClearGraph()
    {
        var elements = graphElements.Where(element => element is Node || element is Edge).ToList();
        DeleteElements(elements);
    }

    private void ConnectEdges()
    {
        foreach (var node in _asset.Nodes)
        {
            if (node == null || !_nodeViews.TryGetValue(node.Id, out var view))
            {
                continue;
            }

            switch (node.Type)
            {
                case DialogFlowNodeType.Start:
                    Connect(view, DialogFlowPortKind.Entry, node.NextNodeId);
                    break;
                case DialogFlowNodeType.Action:
                    Connect(view, DialogFlowPortKind.Next, node.NextNodeId);
                    break;
                case DialogFlowNodeType.Dialog:
                    if (node.Outcomes != null)
                    {
                        for (int i = 0; i < node.Outcomes.Count; i++)
                        {
                            var outcome = node.Outcomes[i];
                            Connect(view, DialogFlowPortKind.Outcome, outcome?.TargetNodeId, i);
                        }
                    }
                    break;
                case DialogFlowNodeType.Choice:
                    if (node.Choices != null)
                    {
                        for (int i = 0; i < node.Choices.Count; i++)
                        {
                            var choice = node.Choices[i];
                            Connect(view, DialogFlowPortKind.Choice, choice?.TargetNodeId, i);
                        }
                    }
                    break;
            }
        }
    }

    private void Connect(DialogFlowNodeView fromView, DialogFlowPortKind kind, string targetNodeId, int outcomeIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            return;
        }

        if (!_nodeViews.TryGetValue(targetNodeId, out var targetView))
        {
            return;
        }

        var output = fromView.GetOutputPort(kind, outcomeIndex);
        var input = targetView.InputPort;
        if (output == null || input == null)
        {
            return;
        }

        var edge = new Edge
        {
            output = output,
            input = input
        };
        output.Connect(edge);
        input.Connect(edge);
        AddElement(edge);
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        if (_asset == null)
        {
            return change;
        }

        if (change.movedElements != null)
        {
            RecordUndo("Move Flow Node");
            foreach (var element in change.movedElements)
            {
                if (element is DialogFlowNodeView nodeView)
                {
                    nodeView.Data.Position = nodeView.GetPosition().position;
                }
            }
        }

        if (change.edgesToCreate != null)
        {
            RecordUndo("Connect Flow Nodes");
            foreach (var edge in change.edgesToCreate)
            {
                ApplyEdge(edge);
            }
        }

        if (change.elementsToRemove != null)
        {
            foreach (var element in change.elementsToRemove)
            {
                switch (element)
                {
                    case Edge edge:
                        RecordUndo("Disconnect Flow Nodes");
                        RemoveEdge(edge);
                        break;
                    case DialogFlowNodeView nodeView:
                        RecordUndo("Remove Flow Node");
                        RemoveNode(nodeView);
                        break;
                }
            }
        }

        return change;
    }

    private void ApplyEdge(Edge edge)
    {
        if (edge?.output?.node is not DialogFlowNodeView fromView ||
            edge.input?.node is not DialogFlowNodeView toView)
        {
            return;
        }

        if (edge.output.userData is not DialogFlowPortData portData)
        {
            return;
        }

        switch (portData.Kind)
        {
            case DialogFlowPortKind.Entry:
            case DialogFlowPortKind.Next:
                fromView.Data.NextNodeId = toView.Data.Id;
                break;
            case DialogFlowPortKind.Outcome:
                if (fromView.Data.Outcomes != null &&
                    portData.Index >= 0 &&
                    portData.Index < fromView.Data.Outcomes.Count)
                {
                    fromView.Data.Outcomes[portData.Index].TargetNodeId = toView.Data.Id;
                }
                break;
            case DialogFlowPortKind.Choice:
                if (fromView.Data.Choices != null &&
                    portData.Index >= 0 &&
                    portData.Index < fromView.Data.Choices.Count)
                {
                    fromView.Data.Choices[portData.Index].TargetNodeId = toView.Data.Id;
                }
                break;
        }
    }

    private void RemoveEdge(Edge edge)
    {
        if (edge?.output?.node is not DialogFlowNodeView fromView)
        {
            return;
        }

        if (edge.output.userData is not DialogFlowPortData portData)
        {
            return;
        }

        switch (portData.Kind)
        {
            case DialogFlowPortKind.Entry:
            case DialogFlowPortKind.Next:
                fromView.Data.NextNodeId = null;
                break;
            case DialogFlowPortKind.Outcome:
                if (fromView.Data.Outcomes != null &&
                    portData.Index >= 0 &&
                    portData.Index < fromView.Data.Outcomes.Count)
                {
                    fromView.Data.Outcomes[portData.Index].TargetNodeId = null;
                }
                break;
            case DialogFlowPortKind.Choice:
                if (fromView.Data.Choices != null &&
                    portData.Index >= 0 &&
                    portData.Index < fromView.Data.Choices.Count)
                {
                    fromView.Data.Choices[portData.Index].TargetNodeId = null;
                }
                break;
        }
    }

    private void RemoveNode(DialogFlowNodeView nodeView)
    {
        if (nodeView == null)
        {
            return;
        }

        var nodeId = nodeView.Data.Id;
        _asset.RemoveNode(nodeId);
        _nodeViews.Remove(nodeId);
        ClearReferences(nodeId);
    }

    private void ClearReferences(string nodeId)
    {
        foreach (var node in _asset.Nodes)
        {
            if (node == null)
            {
                continue;
            }

            if (node.NextNodeId == nodeId)
            {
                node.NextNodeId = null;
            }

            if (node.Outcomes != null)
            {
                foreach (var outcome in node.Outcomes)
                {
                    if (outcome != null && outcome.TargetNodeId == nodeId)
                    {
                        outcome.TargetNodeId = null;
                    }
                }
            }

            if (node.Choices != null)
            {
                foreach (var choice in node.Choices)
                {
                    if (choice != null && choice.TargetNodeId == nodeId)
                    {
                        choice.TargetNodeId = null;
                    }
                }
            }
        }
    }
}
}
