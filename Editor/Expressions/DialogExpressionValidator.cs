using DialogSystem.Runtime.Expressions;

namespace DialogSystem.Editor.Expressions
{
public static class DialogExpressionValidator
{
    public static bool TryValidate(string expression, out string error)
    {
        return DialogExpression.TryParse(expression, out _, out error);
    }
}
}
