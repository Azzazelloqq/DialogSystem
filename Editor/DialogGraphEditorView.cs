using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private static readonly Vector2 PasteOffset = new(30f, 30f);

    public DialogGraphEditorView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        Insert(0, new GridBackground());
        graphViewChanged = OnGraphViewChanged;
        TryBindCopyPasteHandlers();
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList()
            .Where(port => port != startPort &&
                           port.node != startPort.node &&
                           port.direction != startPort.direction)
            .ToList();
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);

        if (_asset == null)
        {
            return;
        }

        var position = this.ChangeCoordinatesTo(contentViewContainer, evt.localMousePosition);
        evt.menu.AppendSeparator();
        evt.menu.AppendAction("Add/Line", _ => CreateNode(DialogGraphNodeType.Line, position));
        evt.menu.AppendAction("Add/Choice", _ => CreateNode(DialogGraphNodeType.Choice, position));
        evt.menu.AppendAction("Add/Condition", _ => CreateNode(DialogGraphNodeType.Condition, position));
        evt.menu.AppendAction("Add/Set", _ => CreateNode(DialogGraphNodeType.Set, position));
        evt.menu.AppendAction("Add/Command", _ => CreateNode(DialogGraphNodeType.Command, position));
        evt.menu.AppendAction("Add/Jump", _ => CreateNode(DialogGraphNodeType.Jump, position));
        evt.menu.AppendAction("Add/Call", _ => CreateNode(DialogGraphNodeType.Call, position));
        evt.menu.AppendAction("Add/Return", _ => CreateNode(DialogGraphNodeType.Return, position));
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
        if (type == DialogGraphNodeType.Start)
        {
            return;
        }

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

    private string SerializeGraphElementsInternal(IEnumerable<GraphElement> elements)
    {
        if (_asset == null)
        {
            return string.Empty;
        }

        var nodeViews = elements.OfType<DialogGraphNodeView>()
            .Where(view => view.Data.Type != DialogGraphNodeType.Start)
            .ToList();
        if (nodeViews.Count == 0)
        {
            return string.Empty;
        }

        var copyData = new GraphCopyData
        {
            Nodes = nodeViews.Select(view => CloneNode(view.Data)).ToList()
        };

        return JsonUtility.ToJson(copyData);
    }

    private bool CanPasteSerializedDataInternal(string data)
    {
        return !string.IsNullOrWhiteSpace(data);
    }

    private void UnserializeAndPasteInternal(string operationName, string data)
    {
        if (_asset == null || string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        var copyData = JsonUtility.FromJson<GraphCopyData>(data);
        if (copyData == null || copyData.Nodes == null || copyData.Nodes.Count == 0)
        {
            return;
        }

        RecordUndo("Paste Dialog Nodes");
        var idMap = new Dictionary<string, string>();
        var newNodes = new List<DialogGraphNodeData>();

        foreach (var node in copyData.Nodes)
        {
            if (node == null)
            {
                continue;
            }

            var clone = CloneNode(node);
            var oldId = clone.Id;
            clone.Id = Guid.NewGuid().ToString("N");
            clone.Position += PasteOffset;
            idMap[oldId] = clone.Id;
            newNodes.Add(clone);
            _asset.Nodes.Add(clone);

            var view = CreateNodeView(clone);
            AddElement(view);
            _nodeViews[clone.Id] = view;
        }

        foreach (var node in newNodes)
        {
            node.NextNodeId = Remap(node.NextNodeId, idMap);
            node.TargetNodeId = Remap(node.TargetNodeId, idMap);
            node.TrueNodeId = Remap(node.TrueNodeId, idMap);
            node.FalseNodeId = Remap(node.FalseNodeId, idMap);

            if (node.Choices == null)
            {
                continue;
            }

            foreach (var choice in node.Choices)
            {
                if (choice == null)
                {
                    continue;
                }

                choice.TargetNodeId = Remap(choice.TargetNodeId, idMap);
            }
        }

        ConnectEdgesForNodes(newNodes);
        ClearSelection();
        foreach (var node in newNodes)
        {
            if (_nodeViews.TryGetValue(node.Id, out var view))
            {
                AddToSelection(view);
            }
        }
    }

    private void TryBindCopyPasteHandlers()
    {
        TrySetDelegate("serializeGraphElements",
            new Func<IEnumerable<GraphElement>, string>(SerializeGraphElementsInternal));
        TrySetDelegate("canPasteSerializedData",
            new Func<string, bool>(CanPasteSerializedDataInternal));
        TrySetDelegate("unserializeAndPaste",
            new Action<string, string>(UnserializeAndPasteInternal));
    }

    private void TrySetDelegate(string memberName, Delegate handler)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var field = typeof(GraphView).GetField(memberName, flags);
        if (field != null)
        {
            try
            {
                var converted = handler.GetType() == field.FieldType
                    ? handler
                    : Delegate.CreateDelegate(field.FieldType, handler.Target, handler.Method);
                field.SetValue(this, converted);
                return;
            }
            catch
            {
                return;
            }
        }

        var property = typeof(GraphView).GetProperty(memberName, flags);
        if (property != null && property.CanWrite)
        {
            try
            {
                var converted = handler.GetType() == property.PropertyType
                    ? handler
                    : Delegate.CreateDelegate(property.PropertyType, handler.Target, handler.Method);
                property.SetValue(this, converted);
            }
            catch
            {
                // Ignore if signature mismatch.
            }
        }
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

    private void ConnectEdgesForNodes(IEnumerable<DialogGraphNodeData> nodes)
    {
        foreach (var node in nodes)
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

    private static DialogGraphNodeData CloneNode(DialogGraphNodeData source)
    {
        if (source == null)
        {
            return null;
        }

        var clone = new DialogGraphNodeData
        {
            Id = source.Id,
            Type = source.Type,
            Position = source.Position,
            Label = source.Label,
            Speaker = source.Speaker,
            Text = source.Text,
            Expression = source.Expression,
            Variable = source.Variable,
            StableId = source.StableId,
            NextNodeId = source.NextNodeId,
            TargetNodeId = source.TargetNodeId,
            TargetOverride = source.TargetOverride,
            TrueNodeId = source.TrueNodeId,
            FalseNodeId = source.FalseNodeId
        };

        if (source.Tags != null)
        {
            clone.Tags = new List<string>(source.Tags);
        }

        if (source.Choices != null)
        {
            foreach (var choice in source.Choices)
            {
                if (choice == null)
                {
                    continue;
                }

                clone.Choices.Add(new DialogGraphChoiceData
                {
                    Id = choice.Id,
                    Text = choice.Text,
                    Condition = choice.Condition,
                    TargetNodeId = choice.TargetNodeId,
                    TargetOverride = choice.TargetOverride,
                    Tags = choice.Tags == null ? new List<string>() : new List<string>(choice.Tags)
                });
            }
        }

        return clone;
    }

    private static string Remap(string id, Dictionary<string, string> idMap)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return idMap.TryGetValue(id, out var newId) ? newId : null;
    }

    [Serializable]
    private sealed class GraphCopyData
    {
        public List<DialogGraphNodeData> Nodes = new();
    }
}
}
