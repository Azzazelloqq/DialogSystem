using System;
using System.Collections.Generic;
using System.IO;
using DialogSystem.Runtime;
using DialogSystem.Runtime.Expressions;

namespace DialogSystem.Runtime.Dsl
{
public static class DialogDslParser
{
    private sealed class IfBlock
    {
        public int IfIndex;
        public int ElseJumpIndex = -1;
        public int Line;
        public bool HasElse;
    }

    private readonly struct TagInfo
    {
        public readonly string Id;
        public readonly List<string> Tags;

        public TagInfo(string id, List<string> tags)
        {
            Id = id;
            Tags = tags;
        }
    }

    public static DialogParseResult Parse(string text, string source)
    {
        var result = new DialogParseResult(source);
        var instructions = new List<DialogInstruction>();
        var labels = new List<DialogLabel>();
        var ifStack = new Stack<IfBlock>();
        var pendingTargets = new List<PendingTarget>();
        var currentDialogId = string.Empty;
        var entryIndex = 0;
        var hasAnyDialog = false;
        var hasAnyLabel = false;
        var choiceGroupOpen = false;

        using var reader = new StringReader(text ?? string.Empty);
        string line;
        var lineNumber = 0;

        void EnsureDialog()
        {
            if (!hasAnyDialog)
            {
                currentDialogId = "main";
                hasAnyDialog = true;
            }
        }

        void FinalizeDialog()
        {
            if (!hasAnyDialog)
            {
                return;
            }

            if (ifStack.Count > 0)
            {
                while (ifStack.Count > 0)
                {
                    var ifBlock = ifStack.Pop();
                    result.AddError(ifBlock.Line, "Unclosed if block. Missing <<endif>>.", string.Empty);
                    if (ifBlock.IfIndex >= 0 && ifBlock.IfIndex < instructions.Count)
                    {
                        instructions[ifBlock.IfIndex] = DialogInstruction.If(
                            instructions[ifBlock.IfIndex].Expression,
                            instructions.Count,
                            true);
                    }
                }
            }

            if (!hasAnyLabel && instructions.Count > 0)
            {
                entryIndex = 0;
            }

            var definition = new DialogDefinition();
            definition.SetData(currentDialogId, entryIndex, new List<DialogInstruction>(instructions),
                new List<DialogLabel>(labels));
            result.Dialogs.Add(definition);
            ValidateTargets(definition, pendingTargets, result);

            instructions.Clear();
            labels.Clear();
            pendingTargets.Clear();
            entryIndex = 0;
            hasAnyLabel = false;
        }

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var rawLine = line;
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                choiceGroupOpen = false;
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                choiceGroupOpen = false;
                continue;
            }

            if (trimmed.StartsWith("@dialog", StringComparison.OrdinalIgnoreCase))
            {
                FinalizeDialog();
                var dialogId = trimmed.Substring("@dialog".Length).Trim();
                if (string.IsNullOrWhiteSpace(dialogId))
                {
                    result.AddError(lineNumber, "Dialog id is missing after @dialog.", rawLine);
                    dialogId = "main";
                }

                currentDialogId = dialogId;
                hasAnyDialog = true;
                continue;
            }

            EnsureDialog();

            if (trimmed.StartsWith("@label", StringComparison.OrdinalIgnoreCase))
            {
                choiceGroupOpen = false;
                var labelId = trimmed.Substring("@label".Length).Trim();
                if (string.IsNullOrWhiteSpace(labelId))
                {
                    result.AddError(lineNumber, "Label id is missing after @label.", rawLine);
                    continue;
                }

                hasAnyLabel = true;
                if (labels.Count == 0)
                {
                    entryIndex = instructions.Count;
                }

                labels.Add(new DialogLabel(labelId, instructions.Count));
                continue;
            }

            if (trimmed.StartsWith("<<", StringComparison.Ordinal))
            {
                choiceGroupOpen = false;
                ParseCommand(trimmed, lineNumber, rawLine, instructions, ifStack, pendingTargets, result);
                continue;
            }

            if (trimmed.StartsWith("*", StringComparison.Ordinal))
            {
                ParseChoice(trimmed, lineNumber, rawLine, instructions, pendingTargets, result, ref choiceGroupOpen);
                continue;
            }

            choiceGroupOpen = false;
            ParseLine(trimmed, lineNumber, rawLine, instructions, result);
        }

        FinalizeDialog();

        return result;
    }

    private static void ParseCommand(string trimmed, int lineNumber, string rawLine,
        List<DialogInstruction> instructions, Stack<IfBlock> ifStack, List<PendingTarget> pendingTargets,
        DialogParseResult result)
    {
        if (!trimmed.EndsWith(">>", StringComparison.Ordinal))
        {
            result.AddError(lineNumber, "Command is missing closing '>>'.", rawLine);
            return;
        }

        var content = trimmed.Substring(2, trimmed.Length - 4).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            result.AddError(lineNumber, "Empty command.", rawLine);
            return;
        }

        var spaceIndex = content.IndexOf(' ');
        var command = spaceIndex > 0 ? content.Substring(0, spaceIndex) : content;
        var args = spaceIndex > 0 ? content.Substring(spaceIndex + 1).Trim() : string.Empty;
        command = command.ToLowerInvariant();

        switch (command)
        {
            case "if":
                if (string.IsNullOrWhiteSpace(args))
                {
                    result.AddError(lineNumber, "If command requires an expression.", rawLine);
                    return;
                }

                ValidateExpression(args, lineNumber, rawLine, result);
                instructions.Add(DialogInstruction.If(args, -1, true));
                ifStack.Push(new IfBlock { IfIndex = instructions.Count - 1, Line = lineNumber });
                break;
            case "else":
                if (ifStack.Count == 0)
                {
                    result.AddError(lineNumber, "Else without matching if.", rawLine);
                    return;
                }

                var block = ifStack.Pop();
                if (block.HasElse)
                {
                    result.AddError(lineNumber, "Multiple else blocks for the same if.", rawLine);
                    return;
                }

                instructions.Add(DialogInstruction.Jump(null, -1, true));
                block.ElseJumpIndex = instructions.Count - 1;
                block.HasElse = true;
                instructions[block.IfIndex] = DialogInstruction.If(instructions[block.IfIndex].Expression,
                    instructions.Count, true);
                ifStack.Push(block);
                break;
            case "endif":
                if (ifStack.Count == 0)
                {
                    result.AddError(lineNumber, "Endif without matching if.", rawLine);
                    return;
                }

                var finished = ifStack.Pop();
                if (finished.HasElse)
                {
                    instructions[finished.ElseJumpIndex] =
                        DialogInstruction.Jump(null, instructions.Count, true);
                }
                else
                {
                    instructions[finished.IfIndex] = DialogInstruction.If(instructions[finished.IfIndex].Expression,
                        instructions.Count, true);
                }
                break;
            case "jump":
                if (string.IsNullOrWhiteSpace(args))
                {
                    result.AddError(lineNumber, "Jump command requires a target label.", rawLine);
                    return;
                }

                instructions.Add(DialogInstruction.Jump(args, -1, false));
                pendingTargets.Add(new PendingTarget(args, lineNumber, rawLine));
                break;
            case "call":
                if (string.IsNullOrWhiteSpace(args))
                {
                    result.AddError(lineNumber, "Call command requires a target label.", rawLine);
                    return;
                }

                instructions.Add(DialogInstruction.Call(args));
                pendingTargets.Add(new PendingTarget(args, lineNumber, rawLine));
                break;
            case "return":
                instructions.Add(DialogInstruction.Return());
                break;
            case "set":
                ParseSet(args, lineNumber, rawLine, instructions, result);
                break;
            case "do":
                if (string.IsNullOrWhiteSpace(args))
                {
                    result.AddError(lineNumber, "Do command requires an expression.", rawLine);
                    return;
                }

                ValidateExpression(args, lineNumber, rawLine, result);
                instructions.Add(DialogInstruction.Command(args));
                break;
            case "exit":
                if (string.IsNullOrWhiteSpace(args))
                {
                    result.AddError(lineNumber, "Exit command requires an outcome name.", rawLine);
                    return;
                }

                instructions.Add(DialogInstruction.OutcomeInstruction(args));
                break;
            default:
                result.AddError(lineNumber, $"Unknown command '{command}'.", rawLine);
                break;
        }
    }

    private static void ParseSet(string args, int lineNumber, string rawLine, List<DialogInstruction> instructions,
        DialogParseResult result)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            result.AddError(lineNumber, "Set command requires variable and expression.", rawLine);
            return;
        }

        string variable;
        string expression;
        var eqIndex = args.IndexOf('=');
        if (eqIndex >= 0)
        {
            variable = args.Substring(0, eqIndex).Trim();
            expression = args.Substring(eqIndex + 1).Trim();
        }
        else
        {
            var spaceIndex = args.IndexOf(' ');
            if (spaceIndex < 0)
            {
                result.AddError(lineNumber, "Set command requires variable and expression.", rawLine);
                return;
            }

            variable = args.Substring(0, spaceIndex).Trim();
            expression = args.Substring(spaceIndex + 1).Trim();
        }

        if (string.IsNullOrWhiteSpace(variable) || string.IsNullOrWhiteSpace(expression))
        {
            result.AddError(lineNumber, "Set command requires variable and expression.", rawLine);
            return;
        }

        ValidateExpression(expression, lineNumber, rawLine, result);
        instructions.Add(DialogInstruction.Set(variable, expression));
    }

    private static void ParseChoice(string trimmed, int lineNumber, string rawLine, List<DialogInstruction> instructions,
        List<PendingTarget> pendingTargets, DialogParseResult result, ref bool choiceGroupOpen)
    {
        var content = trimmed.Substring(1).TrimStart();
        var tagInfo = ExtractTags(content, lineNumber, rawLine, result, out content);
        if (string.IsNullOrWhiteSpace(content))
        {
            result.AddError(lineNumber, "Choice text is empty.", rawLine);
            return;
        }

        string text;
        string remainder;
        if (content.StartsWith("\"", StringComparison.Ordinal))
        {
            if (!TryParseQuotedText(content, out text, out remainder))
            {
                result.AddError(lineNumber, "Unterminated quoted choice text.", rawLine);
                return;
            }
        }
        else
        {
            var whenIndex = IndexOfKeyword(content, "when");
            var arrowIndex = content.IndexOf("->", StringComparison.Ordinal);
            var splitIndex = -1;
            if (whenIndex >= 0 && arrowIndex >= 0)
            {
                splitIndex = Math.Min(whenIndex, arrowIndex);
            }
            else
            {
                splitIndex = whenIndex >= 0 ? whenIndex : arrowIndex;
            }

            if (splitIndex >= 0)
            {
                text = content.Substring(0, splitIndex).Trim();
                remainder = content.Substring(splitIndex).Trim();
            }
            else
            {
                text = content.Trim();
                remainder = string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            result.AddError(lineNumber, "Choice text is empty.", rawLine);
            return;
        }

        string condition = null;
        string target = null;
        if (!string.IsNullOrWhiteSpace(remainder))
        {
            if (remainder.StartsWith("when", StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder.Substring(4).Trim();
                var arrowIndex = remainder.IndexOf("->", StringComparison.Ordinal);
                if (arrowIndex >= 0)
                {
                    condition = remainder.Substring(0, arrowIndex).Trim();
                    target = remainder.Substring(arrowIndex + 2).Trim();
                }
                else
                {
                    condition = remainder.Trim();
                }
            }
            else if (remainder.StartsWith("->", StringComparison.Ordinal))
            {
                target = remainder.Substring(2).Trim();
            }
            else
            {
                result.AddError(lineNumber, $"Unexpected choice tail: '{remainder}'.", rawLine);
            }
        }

        if (!string.IsNullOrWhiteSpace(condition))
        {
            ValidateCondition(condition, lineNumber, rawLine, result);
        }

        if (!string.IsNullOrWhiteSpace(target))
        {
            if (!IsOutcomeTarget(target))
            {
                pendingTargets.Add(new PendingTarget(target, lineNumber, rawLine));
            }
        }

        var choice = new DialogChoice(text, condition, target, tagInfo.Id, tagInfo.Tags);
        if (!choiceGroupOpen || instructions.Count == 0 ||
            instructions[instructions.Count - 1].Type != DialogInstructionType.ChoiceGroup)
        {
            instructions.Add(DialogInstruction.ChoiceGroup(new List<DialogChoice> { choice }));
            choiceGroupOpen = true;
            return;
        }

        if (instructions[instructions.Count - 1].Choices is List<DialogChoice> choiceList)
        {
            choiceList.Add(choice);
        }
        else
        {
            var newChoices = new List<DialogChoice>(instructions[instructions.Count - 1].Choices) { choice };
            instructions[instructions.Count - 1] = DialogInstruction.ChoiceGroup(newChoices);
        }
    }

    private static void ParseLine(string trimmed, int lineNumber, string rawLine,
        List<DialogInstruction> instructions, DialogParseResult result)
    {
        var tagInfo = ExtractTags(trimmed, lineNumber, rawLine, result, out var content);
        if (string.IsNullOrWhiteSpace(content))
        {
            result.AddError(lineNumber, "Line is empty.", rawLine);
            return;
        }

        string speaker = null;
        string text = content;
        var colonIndex = content.IndexOf(':');
        if (colonIndex > 0)
        {
            speaker = content.Substring(0, colonIndex).Trim();
            text = content.Substring(colonIndex + 1).Trim();
        }

        string condition = null;
        var whenIndex = IndexOfKeyword(text, "when");
        if (whenIndex >= 0)
        {
            condition = text.Substring(whenIndex + 4).Trim();
            text = text.Substring(0, whenIndex).Trim();
            if (string.IsNullOrWhiteSpace(condition))
            {
                result.AddError(lineNumber, "Line condition is empty.", rawLine);
                return;
            }

            ValidateCondition(condition, lineNumber, rawLine, result);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            result.AddError(lineNumber, "Line text is empty.", rawLine);
            return;
        }

        instructions.Add(DialogInstruction.Line(speaker, text, tagInfo.Id, tagInfo.Tags, condition));
    }

    private static void ValidateExpression(string expression, int lineNumber, string rawLine, DialogParseResult result)
    {
        if (!DialogExpressionCache.TryGet(expression, out _, out var error))
        {
            result.AddError(lineNumber, $"Expression error: {error}", rawLine);
        }
    }

    private static void ValidateCondition(string condition, int lineNumber, string rawLine, DialogParseResult result)
    {
        if (DialogSystem.Runtime.Conditions.DialogConditionParser.TryParse(condition, out _))
        {
            return;
        }

        ValidateExpression(condition, lineNumber, rawLine, result);
    }

    private static TagInfo ExtractTags(string content, int lineNumber, string rawLine,
        DialogParseResult result, out string cleaned)
    {
        var tags = new List<string>();
        string id = null;
        var endIndex = content.Length;
        var index = endIndex - 1;

        while (index >= 0)
        {
            while (index >= 0 && char.IsWhiteSpace(content[index]))
            {
                index--;
            }

            if (index < 0)
            {
                break;
            }

            var tokenEnd = index;
            while (index >= 0 && !char.IsWhiteSpace(content[index]))
            {
                index--;
            }

            var tokenStart = index + 1;
            var token = content.Substring(tokenStart, tokenEnd - tokenStart + 1);
            if (!token.StartsWith("#", StringComparison.Ordinal))
            {
                break;
            }

            if (token.StartsWith("#id:", StringComparison.OrdinalIgnoreCase))
            {
                var value = token.Substring(4);
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.AddError(lineNumber, "Tag #id requires a value.", rawLine);
                }
                else if (id == null)
                {
                    id = value;
                }
            }
            else if (token.StartsWith("#tag:", StringComparison.OrdinalIgnoreCase))
            {
                var value = token.Substring(5);
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.AddError(lineNumber, "Tag #tag requires a value.", rawLine);
                }
                else
                {
                    tags.Add(value);
                }
            }
            else
            {
                var value = token.Substring(1);
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.AddError(lineNumber, "Tag requires a value.", rawLine);
                }
                else
                {
                    tags.Add(value);
                }
            }

            endIndex = tokenStart;
        }

        tags.Reverse();
        cleaned = content.Substring(0, endIndex).TrimEnd();
        return new TagInfo(id, tags);
    }

    private static bool TryParseQuotedText(string content, out string text, out string remainder)
    {
        text = string.Empty;
        remainder = string.Empty;
        if (content.Length == 0 || content[0] != '"')
        {
            return false;
        }

        var buffer = new System.Text.StringBuilder();
        var index = 1;
        while (index < content.Length)
        {
            var c = content[index];
            if (c == '"')
            {
                text = buffer.ToString();
                remainder = content.Substring(index + 1).Trim();
                return true;
            }

            if (c == '\\' && index + 1 < content.Length)
            {
                var next = content[index + 1];
                buffer.Append(next switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => next
                });
                index += 2;
                continue;
            }

            buffer.Append(c);
            index++;
        }

        return false;
    }

    private static int IndexOfKeyword(string content, string keyword)
    {
        var index = content.IndexOf($" {keyword} ", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return index;
        }

        if (content.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return -1;
    }

    private static bool IsOutcomeTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var trimmed = target.Trim();
        return trimmed.StartsWith("exit:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("outcome:", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateTargets(DialogDefinition dialog, List<PendingTarget> pendingTargets,
        DialogParseResult result)
    {
        if (dialog == null)
        {
            return;
        }

        foreach (var pending in pendingTargets)
        {
            if (string.IsNullOrWhiteSpace(pending.Target))
            {
                continue;
            }

            if (!DialogTarget.TryParse(pending.Target, dialog.Id, out var target))
            {
                result.AddError(pending.Line, $"Invalid target '{pending.Target}'.", pending.RawLine);
                continue;
            }

            if (target.DialogId == dialog.Id && !dialog.TryGetLabelIndex(target.LabelId, out _))
            {
                result.AddError(pending.Line, $"Unknown label '{target.LabelId}'.", pending.RawLine);
            }
        }
    }

    private readonly struct PendingTarget
    {
        public readonly string Target;
        public readonly int Line;
        public readonly string RawLine;

        public PendingTarget(string target, int line, string rawLine)
        {
            Target = target;
            Line = line;
            RawLine = rawLine;
        }
    }
}
}
