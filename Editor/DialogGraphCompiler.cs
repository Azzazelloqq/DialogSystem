using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DialogSystem.Editor
{
public static class DialogGraphCompiler
{
    public static string Compile(DialogGraphAsset asset, out List<string> warnings)
    {
        warnings = new List<string>();
        if (asset == null)
        {
            warnings.Add("Dialog graph asset is null.");
            return string.Empty;
        }

        var dialogId = string.IsNullOrWhiteSpace(asset.DialogId) ? "main" : asset.DialogId.Trim();
        var nodes = asset.Nodes ?? new List<DialogGraphNodeData>();
        var nodeLookup = nodes.Where(node => node != null && !string.IsNullOrWhiteSpace(node.Id))
            .ToDictionary(node => node.Id, StringComparer.Ordinal);

        var labels = BuildLabelMap(nodes);
        var entryNode = FindEntryNode(nodes, nodeLookup, warnings);
        var orderedNodes = OrderNodes(nodes, entryNode);

        var sb = new StringBuilder();
        sb.AppendLine($"@dialog {dialogId}");

        foreach (var node in orderedNodes)
        {
            if (node.Type == DialogGraphNodeType.Start)
            {
                continue;
            }

            var label = labels[node.Id];
            sb.AppendLine($"@label {label}");

            switch (node.Type)
            {
                case DialogGraphNodeType.Line:
                    WriteLineNode(sb, node);
                    WriteJumpOrReturn(sb, labels, node.NextNodeId, node, warnings);
                    break;
                case DialogGraphNodeType.Choice:
                    WriteChoiceNode(sb, node, labels, warnings);
                    WriteJumpOrReturn(sb, labels, node.NextNodeId, node, warnings);
                    break;
                case DialogGraphNodeType.Condition:
                    WriteConditionNode(sb, node, labels, warnings);
                    break;
                case DialogGraphNodeType.Set:
                    WriteSetNode(sb, node, warnings);
                    WriteJumpOrReturn(sb, labels, node.NextNodeId, node, warnings);
                    break;
                case DialogGraphNodeType.Command:
                    WriteCommandNode(sb, node, warnings);
                    WriteJumpOrReturn(sb, labels, node.NextNodeId, node, warnings);
                    break;
                case DialogGraphNodeType.Jump:
                    WriteJumpNode(sb, node, labels, warnings);
                    break;
                case DialogGraphNodeType.Call:
                    WriteCallNode(sb, node, labels, warnings);
                    WriteJumpOrReturn(sb, labels, node.NextNodeId, node, warnings);
                    break;
                case DialogGraphNodeType.Return:
                    sb.AppendLine("<<return>>");
                    break;
                default:
                    warnings.Add($"Unsupported node type '{node.Type}'.");
                    break;
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static DialogGraphNodeData FindEntryNode(List<DialogGraphNodeData> nodes,
        Dictionary<string, DialogGraphNodeData> nodeLookup, List<string> warnings)
    {
        var start = nodes.FirstOrDefault(node => node != null && node.Type == DialogGraphNodeType.Start);
        if (start != null && !string.IsNullOrWhiteSpace(start.NextNodeId) &&
            nodeLookup.TryGetValue(start.NextNodeId, out var entry))
        {
            return entry;
        }

        var firstNonStart = nodes.FirstOrDefault(node => node != null && node.Type != DialogGraphNodeType.Start);
        if (start == null)
        {
            warnings.Add("Start node is missing. Using the first node as entry.");
        }
        else if (string.IsNullOrWhiteSpace(start.NextNodeId))
        {
            warnings.Add("Start node has no entry connection. Using the first node as entry.");
        }

        return firstNonStart;
    }

    private static List<DialogGraphNodeData> OrderNodes(List<DialogGraphNodeData> nodes,
        DialogGraphNodeData entryNode)
    {
        var ordered = new List<DialogGraphNodeData>();
        if (entryNode != null)
        {
            ordered.Add(entryNode);
        }

        var remaining = nodes.Where(node => node != null && node.Type != DialogGraphNodeType.Start && node != entryNode)
            .OrderBy(node => node.Position.y)
            .ThenBy(node => node.Position.x)
            .ToList();
        ordered.AddRange(remaining);
        return ordered;
    }

    private static Dictionary<string, string> BuildLabelMap(List<DialogGraphNodeData> nodes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id) || node.Type == DialogGraphNodeType.Start)
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(node.Label) ? node.Id : node.Label.Trim();
            label = SanitizeLabel(label);
            if (string.IsNullOrWhiteSpace(label))
            {
                label = $"node_{node.Id}";
            }

            var unique = label;
            var counter = 1;
            while (used.Contains(unique))
            {
                unique = $"{label}_{counter}";
                counter++;
            }

            used.Add(unique);
            map[node.Id] = unique;
        }

        return map;
    }

    private static string SanitizeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(label.Length);
        for (int i = 0; i < label.Length; i++)
        {
            var c = label[i];
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c) || c == '-')
            {
                sb.Append('_');
            }
        }

        return sb.ToString();
    }

    private static void WriteLineNode(StringBuilder sb, DialogGraphNodeData node)
    {
        var line = string.IsNullOrWhiteSpace(node.Speaker)
            ? node.Text ?? string.Empty
            : $"{node.Speaker}: {node.Text}";
        line = AppendTags(line, node.StableId, node.Tags);
        sb.AppendLine(line);
    }

    private static void WriteChoiceNode(StringBuilder sb, DialogGraphNodeData node,
        Dictionary<string, string> labels, List<string> warnings)
    {
        if (node.Choices == null || node.Choices.Count == 0)
        {
            warnings.Add($"Choice node '{node.Label ?? node.Id}' has no options.");
            sb.AppendLine("* \"(empty choice)\"");
            return;
        }

        for (int i = 0; i < node.Choices.Count; i++)
        {
            var choice = node.Choices[i];
            if (choice == null)
            {
                continue;
            }

            var text = string.IsNullOrWhiteSpace(choice.Text) ? "(empty choice)" : choice.Text;
            var line = $"* \"{EscapeQuotes(text)}\"";

            if (!string.IsNullOrWhiteSpace(choice.Condition))
            {
                line += $" when {choice.Condition}";
            }

            var target = ResolveTarget(choice.TargetNodeId, choice.TargetOverride, labels);
            if (!string.IsNullOrWhiteSpace(target))
            {
                line += $" -> {target}";
            }

            line = AppendTags(line, choice.Id, choice.Tags);
            sb.AppendLine(line);
        }
    }

    private static void WriteConditionNode(StringBuilder sb, DialogGraphNodeData node,
        Dictionary<string, string> labels, List<string> warnings)
    {
        var expression = string.IsNullOrWhiteSpace(node.Expression) ? "false" : node.Expression;
        if (string.IsNullOrWhiteSpace(node.Expression))
        {
            warnings.Add($"Condition node '{node.Label ?? node.Id}' has empty expression.");
        }

        sb.AppendLine($"<<if {expression}>>");
        WriteJumpOrReturn(sb, labels, node.TrueNodeId, node, warnings, prefix: string.Empty);
        sb.AppendLine("<<else>>");
        WriteJumpOrReturn(sb, labels, node.FalseNodeId, node, warnings, prefix: string.Empty);
        sb.AppendLine("<<endif>>");
    }

    private static void WriteSetNode(StringBuilder sb, DialogGraphNodeData node, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(node.Variable) || string.IsNullOrWhiteSpace(node.Expression))
        {
            warnings.Add($"Set node '{node.Label ?? node.Id}' requires variable and expression.");
        }

        var variable = string.IsNullOrWhiteSpace(node.Variable) ? "Var" : node.Variable;
        var expression = string.IsNullOrWhiteSpace(node.Expression) ? "0" : node.Expression;
        sb.AppendLine($"<<set {variable} = {expression}>>");
    }

    private static void WriteCommandNode(StringBuilder sb, DialogGraphNodeData node, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(node.Expression))
        {
            warnings.Add($"Command node '{node.Label ?? node.Id}' has empty expression.");
        }

        var expression = string.IsNullOrWhiteSpace(node.Expression) ? "/* expression */" : node.Expression;
        sb.AppendLine($"<<do {expression}>>");
    }

    private static void WriteJumpNode(StringBuilder sb, DialogGraphNodeData node,
        Dictionary<string, string> labels, List<string> warnings)
    {
        var target = ResolveTarget(node.TargetNodeId, node.TargetOverride, labels);
        if (string.IsNullOrWhiteSpace(target))
        {
            warnings.Add($"Jump node '{node.Label ?? node.Id}' has no target.");
            sb.AppendLine("<<return>>");
            return;
        }

        sb.AppendLine($"<<jump {target}>>");
    }

    private static void WriteCallNode(StringBuilder sb, DialogGraphNodeData node,
        Dictionary<string, string> labels, List<string> warnings)
    {
        var target = ResolveTarget(node.TargetNodeId, node.TargetOverride, labels);
        if (string.IsNullOrWhiteSpace(target))
        {
            warnings.Add($"Call node '{node.Label ?? node.Id}' has no target.");
            sb.AppendLine("<<return>>");
            return;
        }

        sb.AppendLine($"<<call {target}>>");
    }

    private static void WriteJumpOrReturn(StringBuilder sb, Dictionary<string, string> labels, string targetNodeId,
        DialogGraphNodeData node, List<string> warnings, string prefix = "<<jump ")
    {
        var target = ResolveTarget(targetNodeId, null, labels);
        if (string.IsNullOrWhiteSpace(target))
        {
            sb.AppendLine("<<return>>");
            return;
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            sb.AppendLine($"<<jump {target}>>");
            return;
        }

        sb.AppendLine($"{prefix}{target}>>");
    }

    private static string ResolveTarget(string nodeId, string overrideTarget, Dictionary<string, string> labels)
    {
        if (!string.IsNullOrWhiteSpace(overrideTarget))
        {
            return overrideTarget.Trim();
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        return labels.TryGetValue(nodeId, out var label) ? label : null;
    }

    private static string AppendTags(string line, string stableId, List<string> tags)
    {
        var sb = new StringBuilder(line);
        if (!string.IsNullOrWhiteSpace(stableId))
        {
            sb.Append($" #id:{stableId.Trim()}");
        }

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                sb.Append($" #tag:{tag.Trim()}");
            }
        }

        return sb.ToString();
    }

    private static string EscapeQuotes(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\"", "\\\"");
    }
}
}
