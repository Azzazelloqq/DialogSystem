using System;
using System.Collections.Generic;

namespace DialogSystem.Runtime.Conditions
{
public static class DialogConditionParser
{
    public static bool TryParse(string text, out DialogConditionSpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var parenIndex = trimmed.IndexOf('(');
        if (parenIndex < 0)
        {
            spec = new DialogConditionSpec(trimmed, new DialogConditionArgs(trimmed, Array.Empty<string>()));
            return true;
        }

        if (!trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var id = trimmed.Substring(0, parenIndex).Trim();
        var argsText = trimmed.Substring(parenIndex + 1, trimmed.Length - parenIndex - 2);
        var args = ParseArgs(argsText);
        spec = new DialogConditionSpec(id, new DialogConditionArgs(argsText, args));
        return !string.IsNullOrWhiteSpace(id);
    }

    private static List<string> ParseArgs(string argsText)
    {
        var args = new List<string>();
        if (string.IsNullOrWhiteSpace(argsText))
        {
            return args;
        }

        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < argsText.Length; i++)
        {
            var c = argsText[i];
            if (c == '"' && (i == 0 || argsText[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                AddArg(args, current);
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        AddArg(args, current);
        return args;
    }

    private static void AddArg(List<string> args, System.Text.StringBuilder current)
    {
        var value = current.ToString().Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value.Substring(1, value.Length - 2);
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            args.Add(value);
        }
    }
}

public readonly struct DialogConditionSpec
{
    public string Id { get; }
    public DialogConditionArgs Args { get; }

    public DialogConditionSpec(string id, DialogConditionArgs args)
    {
        Id = id;
        Args = args;
    }
}
}
