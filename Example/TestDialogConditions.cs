using System.Globalization;
using DialogSystem.Runtime;
using DialogSystem.Runtime.Conditions;
using UnityEngine;

namespace DialogSystem.Example
{
[DialogCondition("AlwaysTrue", "Always True")]
public sealed class AlwaysTrueCondition : IDialogCondition
{
    public string Id => "AlwaysTrue";
    public string DisplayName => "Always True";

    public bool Evaluate(DialogConditionContext context, DialogConditionArgs args) => true;
}

[DialogCondition("AlwaysFalse", "Always False")]
public sealed class AlwaysFalseCondition : IDialogCondition
{
    public string Id => "AlwaysFalse";
    public string DisplayName => "Always False";

    public bool Evaluate(DialogConditionContext context, DialogConditionArgs args) => false;
}

[DialogCondition("HasFlag", "Has Flag")]
public sealed class HasFlagCondition : IDialogCondition
{
    public string Id => "HasFlag";
    public string DisplayName => "Has Flag";

    public bool Evaluate(DialogConditionContext context, DialogConditionArgs args)
    {
        var flagName = args.Args.Count > 0 ? args.Args[0] : string.Empty;
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return false;
        }

        if (context.Context.TryGetVariable(flagName, out var value))
        {
            return value is bool flag && flag;
        }

        return false;
    }
}

[DialogCondition("MinValue", "Min Value")]
public sealed class MinValueCondition : IDialogCondition
{
    public string Id => "MinValue";
    public string DisplayName => "Min Value";

    public bool Evaluate(DialogConditionContext context, DialogConditionArgs args)
    {
        if (args.Args.Count < 2)
        {
            return false;
        }

        var varName = args.Args[0];
        if (!context.Context.TryGetVariable(varName, out var value))
        {
            return false;
        }

        if (!double.TryParse(args.Args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var min))
        {
            return false;
        }

        return TryToDouble(value, out var actual) && actual >= min;
    }

    private static bool TryToDouble(object value, out double number)
    {
        switch (value)
        {
            case null:
                number = 0;
                return false;
            case double d:
                number = d;
                return true;
            case float f:
                number = f;
                return true;
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }
}

public static class DialogExampleConditionsBootstrap
{
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterInEditor()
    {
        Register();
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterInRuntime()
    {
        Register();
    }

    private static void Register()
    {
        DialogConditionRegistry.Register(new AlwaysTrueCondition());
        DialogConditionRegistry.Register(new AlwaysFalseCondition());
        DialogConditionRegistry.Register(new HasFlagCondition());
        DialogConditionRegistry.Register(new MinValueCondition());
    }
}
}
