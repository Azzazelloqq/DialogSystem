using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.Editor
{
public sealed class DialogGraphEditorView : GraphView
{
    private DialogGraphAsset _asset;
    private readonly Dictionary<string, DialogGraphNodeView> _nodeViews = new();

    public DialogGraphEditorView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        Insert(0, new GridBackground());
        graphViewChanged = OnGraphViewChanged;
    }

    public void Load(DialogGraphAsset asset)
    {
        _asset = asset;
        Rebuild();
    }

    public void CreateNode(DialogGraphNodeType type)
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

    private void CreateNode(DialogGraphNodeType type, Vector2 position)
    {
        RecordUndo("Create Dialog Node");
        var node = new DialogGraphNodeData
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            Position = position
        };

        if (type == DialogGraphNodeType.Choice)
        {
            node.Choices.Add(new DialogGraphChoiceData
            {
                Id = Guid.NewGuid().ToString("N"),
                Text = "Choice"
            });
        }

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

    private DialogGraphNodeView CreateNodeView(DialogGraphNodeData data)
    {
        var view = new DialogGraphNodeView(data, this);
        view.SetPosition(new Rect(data.Position, view.DefaultSize));
        return view;
    }

    private void EnsureStartNode()
    {
        var hasStart = _asset.Nodes.Any(node => node != null && node.Type == DialogGraphNodeType.Start);
        if (hasStart)
        {
            return;
        }

        RecordUndo("Create Start Node");
        var start = new DialogGraphNodeData
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DialogGraphNodeType.Start,
            Position = Vector2.zero
        };

        _asset.Nodes.Add(start);

        var hasOther = _asset.Nodes.Any(node => node != null && node.Type != DialogGraphNodeType.Start);
        if (!hasOther)
        {
            var line = new DialogGraphNodeData
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = DialogGraphNodeType.Line,
                Position = new Vector2(300f, 0f),
                Text = "..."
            };

            start.NextNodeId = line.Id;
            _asset.Nodes.Add(line);
        }
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
                case DialogGraphNodeType.Start:
                    Connect(view, DialogPortKind.Entry, node.NextNodeId);
                    break;
                case DialogGraphNodeType.Line:
                case DialogGraphNodeType.Set:
                case DialogGraphNodeType.Command:
                    Connect(view, DialogPortKind.Next, node.NextNodeId);
                    break;
                case DialogGraphNodeType.Jump:
                    Connect(view, DialogPortKind.Jump, node.TargetNodeId);
                    break;
                case DialogGraphNodeType.Call:
                    Connect(view, DialogPortKind.CallTarget, node.TargetNodeId);
                    Connect(view, DialogPortKind.Next, node.NextNodeId);
                    break;
                case DialogGraphNodeType.Condition:
                    Connect(view, DialogPortKind.True, node.TrueNodeId);
                    Connect(view, DialogPortKind.False, node.FalseNodeId);
                    break;
                case DialogGraphNodeType.Choice:
                    if (node.Choices != null)
                    {
                        for (int i = 0; i < node.Choices.Count; i++)
                        {
                            var choice = node.Choices[i];
                            Connect(view, DialogPortKind.Choice, choice?.TargetNodeId, i);
                        }
                    }

                    Connect(view, DialogPortKind.Default, node.NextNodeId);
                    break;
            }
        }
    }

    private void Connect(DialogGraphNodeView fromView, DialogPortKind kind, string targetNodeId, int choiceIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            return;
        }

        if (!_nodeViews.TryGetValue(targetNodeId, out var targetView))
        {
            return;
        }

        var output = fromView.GetOutputPort(kind, choiceIndex);
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
            RecordUndo("Move Dialog Node");
            foreach (var element in change.movedElements)
            {
                if (element is DialogGraphNodeView nodeView)
                {
                    nodeView.Data.Position = nodeView.GetPosition().position;
                }
            }
        }

        if (change.edgesToCreate != null)
        {
            RecordUndo("Connect Dialog Nodes");
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
                        RecordUndo("Disconnect Dialog Nodes");
                        RemoveEdge(edge);
                        break;
                    case DialogGraphNodeView nodeView:
                        RecordUndo("Remove Dialog Node");
                        RemoveNode(nodeView);
                        break;
                }
            }
        }

        return change;
    }

    private void ApplyEdge(Edge edge)
    {
        if (edge?.output?.node is not DialogGraphNodeView fromView ||
            edge.input?.node is not DialogGraphNodeView toView)
        {
            return;
        }

        if (edge.output.userData is not DialogPortData portData)
        {
            return;
        }

        switch (portData.Kind)
        {
            case DialogPortKind.Entry:
            case DialogPortKind.Next:
            case DialogPortKind.Default:
                fromView.Data.NextNodeId = toView.Data.Id;
                break;
            case DialogPortKind.Jump:
            case DialogPortKind.CallTarget:
                fromView.Data.TargetNodeId = toView.Data.Id;
                break;
            case DialogPortKind.True:
                fromView.Data.TrueNodeId = toView.Data.Id;
                break;
            case DialogPortKind.False:
                fromView.Data.FalseNodeId = toView.Data.Id;
                break;
            case DialogPortKind.Choice:
                if (fromView.Data.Choices != null &&
                    portData.ChoiceIndex >= 0 &&
                    portData.ChoiceIndex < fromView.Data.Choices.Count)
                {
                    fromView.Data.Choices[portData.ChoiceIndex].TargetNodeId = toView.Data.Id;
                }
                break;
        }
    }

    private void RemoveEdge(Edge edge)
    {
        if (edge?.output?.node is not DialogGraphNodeView fromView)
        {
            return;
        }

        if (edge.output.userData is not DialogPortData portData)
        {
            return;
        }

        switch (portData.Kind)
        {
            case DialogPortKind.Entry:
            case DialogPortKind.Next:
            case DialogPortKind.Default:
                fromView.Data.NextNodeId = null;
                break;
            case DialogPortKind.Jump:
            case DialogPortKind.CallTarget:
                fromView.Data.TargetNodeId = null;
                break;
            case DialogPortKind.True:
                fromView.Data.TrueNodeId = null;
                break;
            case DialogPortKind.False:
                fromView.Data.FalseNodeId = null;
                break;
            case DialogPortKind.Choice:
                if (fromView.Data.Choices != null &&
                    portData.ChoiceIndex >= 0 &&
                    portData.ChoiceIndex < fromView.Data.Choices.Count)
                {
                    fromView.Data.Choices[portData.ChoiceIndex].TargetNodeId = null;
                }
                break;
        }
    }

    private void RemoveNode(DialogGraphNodeView nodeView)
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

            if (node.TargetNodeId == nodeId)
            {
                node.TargetNodeId = null;
            }

            if (node.TrueNodeId == nodeId)
            {
                node.TrueNodeId = null;
            }

            if (node.FalseNodeId == nodeId)
            {
                node.FalseNodeId = null;
            }

            if (node.Choices == null)
            {
                continue;
            }

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
