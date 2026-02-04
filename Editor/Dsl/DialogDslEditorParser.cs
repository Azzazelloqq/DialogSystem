using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DialogSystem.Editor.Dsl
{
public static class DialogDslEditorParser
{
    public static DialogDslDocument Parse(string text)
    {
        var document = new DialogDslDocument();
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        DialogDslBlock currentChoiceBlock = null;
        var groupStack = new Stack<DialogDslBlock>();

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var trimmed = rawLine.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                currentChoiceBlock = null;
                continue;
            }

            if (trimmed.StartsWith("@dialog", StringComparison.OrdinalIgnoreCase))
            {
                var id = trimmed.Substring("@dialog".Length).Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    document.DialogId = id;
                }

                currentChoiceBlock = null;
                continue;
            }

            if (trimmed.StartsWith("@label", StringComparison.OrdinalIgnoreCase))
            {
                currentChoiceBlock = null;
                continue;
            }

            if (trimmed.StartsWith("<<if", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(">>"))
            {
                var content = trimmed.Substring(2, trimmed.Length - 4).Trim();
                var condition = content.Substring(2).Trim();
                var block = new DialogDslBlock
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = DialogDslBlockType.ConditionGroup,
                    Condition = condition
                };
                GetCurrentList(document, groupStack).Add(block);
                groupStack.Push(block);
                currentChoiceBlock = null;
                continue;
            }

            if (trimmed.StartsWith("<<endif", StringComparison.OrdinalIgnoreCase))
            {
                if (groupStack.Count > 0)
                {
                    groupStack.Pop();
                }
                else
                {
                    GetCurrentList(document, groupStack).Add(new DialogDslBlock
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Type = DialogDslBlockType.Raw,
                        Raw = rawLine
                    });
                }

                currentChoiceBlock = null;
                continue;
            }

            if (trimmed.StartsWith("<<else", StringComparison.OrdinalIgnoreCase))
            {
                GetCurrentList(document, groupStack).Add(new DialogDslBlock
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = DialogDslBlockType.Raw,
                    Raw = rawLine
                });
                currentChoiceBlock = null;
                continue;
            }

            if (trimmed.StartsWith("<<exit", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(">>"))
            {
                var content = trimmed.Substring(2, trimmed.Length - 4).Trim();
                var outcome = content.Substring(4).Trim();
                GetCurrentList(document, groupStack).Add(new DialogDslBlock
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = DialogDslBlockType.Exit,
                    Outcome = outcome
                });
                currentChoiceBlock = null;
                continue;
            }

            if (trimmed.StartsWith("*", StringComparison.Ordinal))
            {
            var choice = ParseChoice(trimmed);
                if (currentChoiceBlock == null)
                {
                    currentChoiceBlock = new DialogDslBlock
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Type = DialogDslBlockType.ChoiceGroup
                    };
                    GetCurrentList(document, groupStack).Add(currentChoiceBlock);
                }

                currentChoiceBlock.Choices.Add(choice);
                continue;
            }

            currentChoiceBlock = null;
            if (TryParseLine(trimmed, out var lineBlock))
            {
                GetCurrentList(document, groupStack).Add(lineBlock);
                continue;
            }

            GetCurrentList(document, groupStack).Add(new DialogDslBlock
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = DialogDslBlockType.Raw,
                Raw = rawLine
            });
        }

        return document;
    }

    public static void Save(DialogDslDocument document, string path)
    {
        var builder = new StringBuilder();
        var dialogId = string.IsNullOrWhiteSpace(document.DialogId) ? "main" : document.DialogId.Trim();
        builder.AppendLine($"@dialog {dialogId}");
        builder.AppendLine("@label start");
        builder.AppendLine();

        AppendBlocks(builder, document.Blocks, 0);

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
    }

    public static DialogDslDocument FromDraft(DialogDraftAsset draft)
    {
        if (draft == null)
        {
            return null;
        }

        var document = new DialogDslDocument
        {
            DialogId = string.IsNullOrWhiteSpace(draft.DialogId) ? "main" : draft.DialogId.Trim(),
            Blocks = new List<DialogDslBlock>()
        };

        if (draft.Blocks != null)
        {
            foreach (var block in draft.Blocks)
            {
                document.Blocks.Add(CloneBlock(block));
            }
        }

        return document;
    }

    public static void ApplyToDraft(DialogDraftAsset draft, DialogDslDocument document)
    {
        if (draft == null || document == null)
        {
            return;
        }

        draft.DialogId = string.IsNullOrWhiteSpace(document.DialogId) ? "main" : document.DialogId.Trim();
        draft.Blocks.Clear();
        if (document.Blocks != null)
        {
            foreach (var block in document.Blocks)
            {
                draft.Blocks.Add(CloneBlock(block));
            }
        }
    }

    private static DialogDslBlock CloneBlock(DialogDslBlock source)
    {
        if (source == null)
        {
            return null;
        }

        var clone = new DialogDslBlock
        {
            Id = source.Id,
            Type = source.Type,
            Speaker = source.Speaker,
            Text = source.Text,
            TextKey = source.TextKey,
            Condition = source.Condition,
            Outcome = source.Outcome,
            StableId = source.StableId,
            Raw = source.Raw,
            Tags = source.Tags == null ? new List<string>() : new List<string>(source.Tags),
            Choices = new List<DialogDslChoice>(),
            Children = new List<DialogDslBlock>()
        };

        if (source.Choices != null)
        {
            foreach (var choice in source.Choices)
            {
                if (choice == null)
                {
                    continue;
                }

                clone.Choices.Add(new DialogDslChoice
                {
                    Id = choice.Id,
                    Text = choice.Text,
                    TextKey = choice.TextKey,
                    Condition = choice.Condition,
                    Outcome = choice.Outcome,
                    StableId = choice.StableId,
                    Tags = choice.Tags == null ? new List<string>() : new List<string>(choice.Tags)
                });
            }
        }

        if (source.Children != null)
        {
            foreach (var child in source.Children)
            {
                clone.Children.Add(CloneBlock(child));
            }
        }

        return clone;
    }

    private static DialogDslChoice ParseChoice(string trimmed)
    {
        var content = trimmed.Substring(1).TrimStart();
        var tags = ExtractTags(content, out content, out var stableId, out var textKey);

        string text;
        string remainder;
        if (content.StartsWith("\"", StringComparison.Ordinal))
        {
            if (!TryParseQuotedText(content, out text, out remainder))
            {
                text = content.Trim('"');
                remainder = string.Empty;
            }
        }
        else
        {
            var whenIndex = IndexOfKeyword(content, "when");
            var arrowIndex = content.IndexOf("->", StringComparison.Ordinal);
            var splitIndex = whenIndex >= 0 && arrowIndex >= 0 ? Math.Min(whenIndex, arrowIndex) :
                whenIndex >= 0 ? whenIndex : arrowIndex;

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

        string condition = null;
        string outcome = null;

        if (!string.IsNullOrWhiteSpace(remainder))
        {
            if (remainder.StartsWith("when", StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder.Substring(4).Trim();
                var arrowIndex = remainder.IndexOf("->", StringComparison.Ordinal);
                if (arrowIndex >= 0)
                {
                    condition = remainder.Substring(0, arrowIndex).Trim();
                    outcome = remainder.Substring(arrowIndex + 2).Trim();
                }
                else
                {
                    condition = remainder.Trim();
                }
            }
            else if (remainder.StartsWith("->", StringComparison.Ordinal))
            {
                outcome = remainder.Substring(2).Trim();
            }
        }

        outcome = NormalizeOutcome(outcome);
        return new DialogDslChoice
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = text,
            TextKey = textKey,
            Condition = condition,
            Outcome = outcome,
            StableId = stableId,
            Tags = tags
        };
    }

    private static bool TryParseLine(string trimmed, out DialogDslBlock block)
    {
        block = null;
        var tags = ExtractTags(trimmed, out var content, out var stableId, out var textKey);
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        if (content.StartsWith("<<", StringComparison.Ordinal))
        {
            return false;
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
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        block = new DialogDslBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DialogDslBlockType.Line,
            Speaker = speaker,
            Text = text,
            TextKey = textKey,
            Condition = condition,
            StableId = stableId,
            Tags = tags
        };
        return true;
    }

    private static void AppendBlocks(StringBuilder builder, List<DialogDslBlock> blocks, int depth)
    {
        if (blocks == null)
        {
            return;
        }

        foreach (var block in blocks)
        {
            if (block == null)
            {
                continue;
            }

            switch (block.Type)
            {
                case DialogDslBlockType.Line:
                    builder.AppendLine(FormatLine(block));
                    builder.AppendLine();
                    break;
                case DialogDslBlockType.ChoiceGroup:
                    foreach (var choice in block.Choices)
                    {
                        builder.AppendLine(FormatChoice(choice));
                    }
                    builder.AppendLine();
                    break;
                case DialogDslBlockType.Exit:
                    builder.AppendLine($"<<exit {block.Outcome?.Trim()}>>");
                    builder.AppendLine();
                    break;
                case DialogDslBlockType.ConditionGroup:
                    var condition = string.IsNullOrWhiteSpace(block.Condition) ? "true" : block.Condition.Trim();
                    builder.AppendLine($"<<if {condition}>>");
                    AppendBlocks(builder, block.Children, depth + 1);
                    builder.AppendLine("<<endif>>");
                    builder.AppendLine();
                    break;
                case DialogDslBlockType.Raw:
                    builder.AppendLine(block.Raw ?? string.Empty);
                    builder.AppendLine();
                    break;
            }
        }
    }

    private static List<DialogDslBlock> GetCurrentList(DialogDslDocument document, Stack<DialogDslBlock> stack)
    {
        return stack.Count > 0 ? stack.Peek().Children : document.Blocks;
    }

    private static string FormatLine(DialogDslBlock block)
    {
        var line = string.IsNullOrWhiteSpace(block.Speaker)
            ? block.Text
            : $"{block.Speaker}: {block.Text}";
        if (!string.IsNullOrWhiteSpace(block.Condition))
        {
            line += $" when {block.Condition}";
        }

        return AppendTags(line, block.StableId, block.Tags, block.TextKey);
    }

    private static string FormatChoice(DialogDslChoice choice)
    {
        var line = $"* \"{Escape(choice.Text)}\"";
        if (!string.IsNullOrWhiteSpace(choice.Condition))
        {
            line += $" when {choice.Condition}";
        }

        if (!string.IsNullOrWhiteSpace(choice.Outcome))
        {
            line += $" -> exit:{choice.Outcome}";
        }

        return AppendTags(line, choice.StableId, choice.Tags, choice.TextKey);
    }

    private static List<string> ExtractTags(string content, out string cleaned, out string stableId, out string textKey)
    {
        var tags = new List<string>();
        stableId = null;
        textKey = null;
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
                stableId ??= token.Substring(4);
            }
            else if (token.StartsWith("#loc:", StringComparison.OrdinalIgnoreCase))
            {
                textKey ??= token.Substring(5);
            }
            else if (token.StartsWith("#tag:", StringComparison.OrdinalIgnoreCase))
            {
                tags.Add(token.Substring(5));
            }
            else
            {
                tags.Add(token.Substring(1));
            }

            endIndex = tokenStart;
        }

        tags.Reverse();
        cleaned = content.Substring(0, endIndex).TrimEnd();
        return tags;
    }

    private static string AppendTags(string line, string stableId, List<string> tags, string textKey)
    {
        if (!string.IsNullOrWhiteSpace(stableId))
        {
            line += $" #id:{stableId}";
        }

        if (!string.IsNullOrWhiteSpace(textKey))
        {
            line += $" #loc:{textKey}";
        }

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    line += $" #tag:{tag}";
                }
            }
        }

        return line;
    }

    private static bool TryParseQuotedText(string content, out string text, out string remainder)
    {
        text = string.Empty;
        remainder = string.Empty;
        if (!content.StartsWith("\"", StringComparison.Ordinal))
        {
            return false;
        }

        var buffer = new StringBuilder();
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
                buffer.Append(content[index + 1]);
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
            return index + 1;
        }

        if (content.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return -1;
    }

    private static string NormalizeOutcome(string outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return null;
        }

        var trimmed = outcome.Trim();
        const string exitPrefix = "exit:";
        const string outcomePrefix = "outcome:";
        if (trimmed.StartsWith(exitPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(exitPrefix.Length).Trim();
        }

        if (trimmed.StartsWith(outcomePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(outcomePrefix.Length).Trim();
        }

        return trimmed;
    }

    private static string Escape(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\"", "\\\"");
    }
}
}
