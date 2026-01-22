using System.Collections.Generic;

namespace DialogSystem.Runtime.Expressions
{
internal static class DialogExpressionCache
{
    private static readonly Dictionary<string, DialogExpression> Cache = new();
    private static readonly object LockObject = new();

    public static bool TryGet(string expressionText, out DialogExpression expression, out string error)
    {
        expression = null;
        error = null;

        if (string.IsNullOrWhiteSpace(expressionText))
        {
            error = "Expression is empty.";
            return false;
        }

        lock (LockObject)
        {
            if (Cache.TryGetValue(expressionText, out expression))
            {
                return true;
            }

            if (!DialogExpression.TryParse(expressionText, out expression, out error))
            {
                return false;
            }

            Cache[expressionText] = expression;
            return true;
        }
    }
}
}
