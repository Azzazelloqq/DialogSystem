using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.Runtime;
using DialogSystem.Runtime.Dsl;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor
{
public static class DialogGraphImportUtility
{
    public static bool Import(DialogGraphAsset asset, out string error)
    {
        error = null;
        if (asset == null)
        {
            error = "Dialog graph asset is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(asset.DslPath) || !File.Exists(asset.DslPath))
        {
            error = "DSL file not found. Set DSL Path first.";
            return false;
        }

        var text = File.ReadAllText(asset.DslPath);
        var result = DialogDslParser.Parse(text, asset.DslPath);
        if (result.Dialogs.Count == 0)
        {
            error = "No dialogs found in DSL file.";
            return false;
        }

        var dialog = result.Dialogs.FirstOrDefault(d => d.Id == asset.DialogId) ?? result.Dialogs[0];
        if (dialog == null)
        {
            error = "Failed to read dialog from DSL.";
            return false;
        }

        Undo.RecordObject(asset, "Import Dialog DSL");
        asset.DialogId = dialog.Id;
        asset.Nodes.Clear();

        var labels = BuildLabelMap(dialog);
        var keptIndices = GetKeptIndices(dialog);
        var indexToNodeId = new Dictionary<int, string>();
        var nodes = new List<DialogGraphNodeData>();

        for (int i = 0; i < keptIndices.Count; i++)
        {
            var index = keptIndices[i];
            var instruction = dialog.Instructions[index];
            var node = CreateNodeFromInstruction(instruction);
            node.Id = System.Guid.NewGuid().ToString("N");
            node.Position = new Vector2(0f, i * 180f);
            if (labels.TryGetValue(index, out var label))
            {
                node.Label = label;
            }

            indexToNodeId[index] = node.Id;
            nodes.Add(node);
        }

        var startNode = new DialogGraphNodeData
        {
            Id = System.Guid.NewGuid().ToString("N"),
            Type = DialogGraphNodeType.Start,
            Position = Vector2.zero
        };

        if (indexToNodeId.TryGetValue(dialog.EntryIndex, out var entryNodeId))
        {
            startNode.NextNodeId = entryNodeId;
        }

        asset.Nodes.Add(startNode);
        asset.Nodes.AddRange(nodes);

        for (int i = 0; i < keptIndices.Count; i++)
        {
            var index = keptIndices[i];
            var instruction = dialog.Instructions[index];
            var node = nodes[i];

            switch (instruction.Type)
            {
                case DialogInstructionType.Line:
                case DialogInstructionType.Set:
                case DialogInstructionType.Command:
                case DialogInstructionType.Call:
                case DialogInstructionType.ChoiceGroup:
                    node.NextNodeId = ResolveNextNodeId(keptIndices, indexToNodeId, index);
                    break;
            }

            switch (instruction.Type)
            {
                case DialogInstructionType.If:
                    node.TrueNodeId = ResolveNextNodeId(keptIndices, indexToNodeId, index);
                    node.FalseNodeId = ResolveJumpTarget(dialog, instruction.JumpIndex, keptIndices, indexToNodeId);
                    break;
                case DialogInstructionType.Jump:
                case DialogInstructionType.Call:
                    var jumpTarget = ResolveTarget(dialog, instruction.Target, keptIndices, indexToNodeId);
                    node.TargetNodeId = jumpTarget.TargetNodeId;
                    node.TargetOverride = jumpTarget.Override;
                    break;
                case DialogInstructionType.ChoiceGroup:
                    if (node.Choices == null)
                    {
                        node.Choices = new List<DialogGraphChoiceData>();
                    }

                    node.Choices.Clear();
                    foreach (var choice in instruction.Choices)
                    {
                        var choiceData = new DialogGraphChoiceData
                        {
                            Id = choice.Id,
                            Text = choice.Text,
                            Condition = choice.Condition,
                            Tags = choice.Tags == null ? new List<string>() : new List<string>(choice.Tags)
                        };

                        var choiceTarget = ResolveTarget(dialog, choice.Target, keptIndices, indexToNodeId);
                        choiceData.TargetNodeId = choiceTarget.TargetNodeId;
                        choiceData.TargetOverride = choiceTarget.Override;
                        node.Choices.Add(choiceData);
                    }
                    break;
            }
        }

        EditorUtility.SetDirty(asset);
        return true;
    }

    private static List<int> GetKeptIndices(DialogDefinition dialog)
    {
        var list = new List<int>();
        for (int i = 0; i < dialog.Instructions.Count; i++)
        {
            var instruction = dialog.Instructions[i];
            if (instruction == null)
            {
                continue;
            }

            if (instruction.Type == DialogInstructionType.Jump && instruction.IsInternal)
            {
                continue;
            }

            list.Add(i);
        }

        return list;
    }

    private static Dictionary<int, string> BuildLabelMap(DialogDefinition dialog)
    {
        var map = new Dictionary<int, string>();
        foreach (var label in dialog.Labels)
        {
            if (label == null || string.IsNullOrWhiteSpace(label.Name))
            {
                continue;
            }

            if (!map.ContainsKey(label.InstructionIndex))
            {
                map[label.InstructionIndex] = label.Name;
            }
        }

        return map;
    }

    private static DialogGraphNodeData CreateNodeFromInstruction(DialogInstruction instruction)
    {
        var node = new DialogGraphNodeData
        {
            Type = instruction.Type switch
            {
                DialogInstructionType.Line => DialogGraphNodeType.Line,
                DialogInstructionType.ChoiceGroup => DialogGraphNodeType.Choice,
                DialogInstructionType.If => DialogGraphNodeType.Condition,
                DialogInstructionType.Jump => DialogGraphNodeType.Jump,
                DialogInstructionType.Call => DialogGraphNodeType.Call,
                DialogInstructionType.Return => DialogGraphNodeType.Return,
                DialogInstructionType.Set => DialogGraphNodeType.Set,
                DialogInstructionType.Command => DialogGraphNodeType.Command,
                _ => DialogGraphNodeType.Line
            },
            Speaker = instruction.Speaker,
            Text = instruction.Text,
            Expression = instruction.Expression,
            Variable = instruction.Variable,
            StableId = instruction.Id,
            Tags = instruction.Tags == null ? new List<string>() : new List<string>(instruction.Tags)
        };

        return node;
    }

    private static string ResolveNextNodeId(List<int> keptIndices, Dictionary<int, string> indexToNodeId, int index)
    {
        var nextIndex = keptIndices.FirstOrDefault(i => i > index);
        return indexToNodeId.TryGetValue(nextIndex, out var nodeId) ? nodeId : null;
    }

    private static string ResolveJumpTarget(DialogDefinition dialog, int jumpIndex, List<int> keptIndices,
        Dictionary<int, string> indexToNodeId)
    {
        if (jumpIndex < 0)
        {
            return null;
        }

        var nextIndex = keptIndices.FirstOrDefault(i => i >= jumpIndex);
        return indexToNodeId.TryGetValue(nextIndex, out var nodeId) ? nodeId : null;
    }

    private static (string TargetNodeId, string Override) ResolveTarget(DialogDefinition dialog, string rawTarget,
        List<int> keptIndices, Dictionary<int, string> indexToNodeId)
    {
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return (null, null);
        }

        if (!DialogTarget.TryParse(rawTarget, dialog.Id, out var target))
        {
            return (null, rawTarget);
        }

        if (!string.Equals(target.DialogId, dialog.Id, System.StringComparison.OrdinalIgnoreCase))
        {
            return (null, rawTarget);
        }

        if (!dialog.TryGetLabelIndex(target.LabelId, out var index))
        {
            return (null, rawTarget);
        }

        var nodeId = indexToNodeId.TryGetValue(index, out var resolved) ? resolved : null;
        return (nodeId, nodeId == null ? rawTarget : null);
    }
}
}
