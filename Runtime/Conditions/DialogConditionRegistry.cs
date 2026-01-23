using System;
using System.Collections.Generic;

namespace DialogSystem.Runtime.Conditions
{
public static class DialogConditionRegistry
{
    private static readonly Dictionary<string, IDialogCondition> Conditions =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Register(IDialogCondition condition)
    {
        if (condition == null || string.IsNullOrWhiteSpace(condition.Id))
        {
            return;
        }

        Conditions[condition.Id.Trim()] = condition;
    }

    public static bool TryGet(string id, out IDialogCondition condition)
    {
        condition = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return Conditions.TryGetValue(id.Trim(), out condition);
    }

    public static IReadOnlyList<IDialogCondition> GetAll()
    {
        return new List<IDialogCondition>(Conditions.Values);
    }

    public static bool TryEvaluate(string conditionText, DialogConditionContext context, out bool result, out string error)
    {
        result = false;
        error = null;

        if (!DialogConditionParser.TryParse(conditionText, out var parsed))
        {
            error = $"Invalid condition '{conditionText}'.";
            return false;
        }

        if (!TryGet(parsed.Id, out var condition))
        {
            return false;
        }

        try
        {
            result = condition.Evaluate(context, parsed.Args);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
}
