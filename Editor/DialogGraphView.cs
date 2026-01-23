using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.Editor
{
public sealed class DialogGraphView : GraphView
{
    private const float NodeWidth = 260f;
    private const float NodeHeight = 120f;
    private const float NodeSpacingY = 160f;

    public DialogGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        Insert(0, new GridBackground());
    }

    public void ClearGraph()
    {
        var toRemove = graphElements.Where(element => element is Node || element is Edge).ToList();
        DeleteElements(toRemove);
    }

    public void LoadDialog(DialogDefinition dialog)
    {
        ClearGraph();
        if (dialog == null)
        {
            return;
        }

        dialog.BuildCaches();
        var labelsByIndex = BuildLabelMap(dialog);
        var nodes = new Dictionary<int, DialogGraphNode>();

        for (int i = 0; i < dialog.Instructions.Count; i++)
        {
            var instruction = dialog.Instructions[i];
            if (instruction == null)
            {
                continue;
            }

            var node = CreateInstructionNode(dialog, instruction, i, labelsByIndex);
            nodes[i] = node;
            AddElement(node);
        }

        var startNode = CreateStartNode(dialog);
        AddElement(startNode);

        if (nodes.TryGetValue(dialog.EntryIndex, out var entryNode))
        {
            var startEdge = startNode.AddOutput("Start");
            ConnectPorts(startEdge, entryNode.Input);
        }

        foreach (var pair in nodes)
        {
            var index = pair.Key;
            var node = pair.Value;
            var instruction = dialog.Instructions[index];
            if (instruction == null)
            {
                continue;
            }

            foreach (var edgeInfo in EnumerateEdges(dialog, instruction, index))
            {
                if (!nodes.TryGetValue(edgeInfo.TargetIndex, out var targetNode))
                {
                    continue;
                }

                var port = node.AddOutput(edgeInfo.Label);
                ConnectPorts(port, targetNode.Input);
            }
        }
    }

    private static Dictionary<int, List<string>> BuildLabelMap(DialogDefinition dialog)
    {
        var map = new Dictionary<int, List<string>>();
        foreach (var label in dialog.Labels)
        {
            if (label == null)
            {
                continue;
            }

            if (!map.TryGetValue(label.InstructionIndex, out var list))
            {
                list = new List<string>();
                map[label.InstructionIndex] = list;
            }

            list.Add(label.Name);
        }

        return map;
    }

    private DialogGraphNode CreateStartNode(DialogDefinition dialog)
    {
        var node = new DialogGraphNode("Start", $"Dialog: {dialog.Id}", isInternal: true);
        node.SetPosition(new Rect(0, 0, NodeWidth, NodeHeight));
        return node;
    }

    private DialogGraphNode CreateInstructionNode(DialogDefinition dialog, DialogInstruction instruction, int index,
        Dictionary<int, List<string>> labelsByIndex)
    {
        var title = $"{index} - {instruction.Type}";
        if (labelsByIndex.TryGetValue(index, out var labels))
        {
            title = $"{title} [{string.Join(", ", labels)}]";
        }

        var content = instruction.Type switch
        {
            DialogInstructionType.Line => string.IsNullOrWhiteSpace(instruction.Speaker)
                ? instruction.Text
                : $"{instruction.Speaker}: {instruction.Text}",
            DialogInstructionType.ChoiceGroup => $"Choices: {instruction.Choices.Count}",
            DialogInstructionType.If => $"if {instruction.Expression}",
            DialogInstructionType.Jump => instruction.Target ?? $"jump -> {instruction.JumpIndex}",
            DialogInstructionType.Call => $"call {instruction.Target}",
            DialogInstructionType.Return => "return",
            DialogInstructionType.Set => $"{instruction.Variable} = {instruction.Expression}",
            DialogInstructionType.Command => instruction.Expression,
            DialogInstructionType.Outcome => $"exit {instruction.Outcome}",
            _ => instruction.Type.ToString()
        };

        var node = new DialogGraphNode(title, content, instruction.IsInternal);
        node.SetPosition(new Rect(0, NodeSpacingY + index * NodeSpacingY, NodeWidth, NodeHeight));
        return node;
    }

    private IEnumerable<EdgeInfo> EnumerateEdges(DialogDefinition dialog, DialogInstruction instruction, int index)
    {
        switch (instruction.Type)
        {
            case DialogInstructionType.Line:
            case DialogInstructionType.Set:
            case DialogInstructionType.Command:
                if (index + 1 < dialog.Instructions.Count)
                {
                    yield return new EdgeInfo(index + 1, "Next");
                }
                yield break;
            case DialogInstructionType.Return:
                yield break;
            case DialogInstructionType.If:
                if (index + 1 < dialog.Instructions.Count)
                {
                    yield return new EdgeInfo(index + 1, "True");
                }

                if (instruction.JumpIndex >= 0)
                {
                    yield return new EdgeInfo(instruction.JumpIndex, "False");
                }
                yield break;
            case DialogInstructionType.Jump:
                if (instruction.JumpIndex >= 0 && string.IsNullOrWhiteSpace(instruction.Target))
                {
                    yield return new EdgeInfo(instruction.JumpIndex, "Jump");
                    yield break;
                }

                if (TryResolveLocalTarget(dialog, instruction.Target, out var targetIndex))
                {
                    yield return new EdgeInfo(targetIndex, "Jump");
                }
                yield break;
            case DialogInstructionType.Call:
                if (TryResolveLocalTarget(dialog, instruction.Target, out var callIndex))
                {
                    yield return new EdgeInfo(callIndex, "Call");
                }
                yield break;
            case DialogInstructionType.ChoiceGroup:
                var counter = 1;
                foreach (var choice in instruction.Choices)
                {
                    var label = $"[{counter}] {Trim(choice.Text, 16)}";
                    counter++;
                    if (TryResolveLocalTarget(dialog, choice.Target, out var choiceIndex))
                    {
                        yield return new EdgeInfo(choiceIndex, label);
                    }
                    else if (index + 1 < dialog.Instructions.Count)
                    {
                        yield return new EdgeInfo(index + 1, label);
                    }
                }
                yield break;
        }
    }

    private static bool TryResolveLocalTarget(DialogDefinition dialog, string target, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (!DialogTarget.TryParse(target, dialog.Id, out var parsed))
        {
            return false;
        }

        if (parsed.DialogId != dialog.Id)
        {
            return false;
        }

        return dialog.TryGetLabelIndex(parsed.LabelId, out index);
    }

    private void ConnectPorts(Port output, Port input)
    {
        var edge = new Edge
        {
            output = output,
            input = input
        };
        output.Connect(edge);
        input.Connect(edge);
        AddElement(edge);
    }

    private static string Trim(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Length <= max ? text : $"{text.Substring(0, max)}...";
    }

    private readonly struct EdgeInfo
    {
        public readonly int TargetIndex;
        public readonly string Label;

        public EdgeInfo(int targetIndex, string label)
        {
            TargetIndex = targetIndex;
            Label = label;
        }
    }

    private sealed class DialogGraphNode : Node
    {
        public Port Input { get; }
        private readonly List<Port> _outputs = new();

        public DialogGraphNode(string titleText, string content, bool isInternal)
        {
            title = titleText;
            var label = new Label(content ?? string.Empty)
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal
                }
            };
            mainContainer.Add(label);

            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = string.Empty;
            inputContainer.Add(Input);

            if (isInternal)
            {
                AddToClassList("dialog-node-internal");
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public Port AddOutput(string label)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            port.portName = label;
            outputContainer.Add(port);
            _outputs.Add(port);
            RefreshExpandedState();
            RefreshPorts();
            return port;
        }
    }
}
}
