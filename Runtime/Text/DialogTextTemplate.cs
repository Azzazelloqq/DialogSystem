using System;
using System.Text;
using DialogSystem.Runtime.Expressions;
using DialogSystem.Runtime.Localization;

namespace DialogSystem.Runtime.Text
{
public static class DialogTextTemplate
{
    public static bool TryResolve(string textKey, string fallback, IDialogContext context,
        out string result, out string error)
    {
        var text = fallback ?? string.Empty;
        if (context != null && !string.IsNullOrWhiteSpace(textKey) &&
            context.TryGetService<IDialogLocalizationProvider>(out var provider))
        {
            text = DialogLocalizationResolver.Resolve(textKey, text, provider);
        }

        return TryInterpolate(text, context, out result, out error);
    }

    public static bool TryInterpolate(string text, IDialogContext context,
        out string result, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(text))
        {
            result = text ?? string.Empty;
            return true;
        }

        var startIndex = text.IndexOf("${", StringComparison.Ordinal);
        if (startIndex < 0)
        {
            result = text;
            return true;
        }

        if (context == null)
        {
            result = text;
            error = "Dialog context is null.";
            return false;
        }

        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var openIndex = text.IndexOf("${", index, StringComparison.Ordinal);
            if (openIndex < 0)
            {
                builder.Append(text, index, text.Length - index);
                break;
            }

            builder.Append(text, index, openIndex - index);
            var closeIndex = text.IndexOf('}', openIndex + 2);
            if (closeIndex < 0)
            {
                result = text;
                error = "Text placeholder is missing a closing '}'.";
                return false;
            }

            var expressionText = text.Substring(openIndex + 2, closeIndex - openIndex - 2).Trim();
            if (expressionText.Length > 0)
            {
                if (!DialogExpressionCache.TryGet(expressionText, out var expression, out error))
                {
                    result = text;
                    return false;
                }

                var value = expression.Evaluate(context, out error);
                if (error != null)
                {
                    result = text;
                    return false;
                }

                builder.Append(value?.ToString() ?? string.Empty);
            }

            index = closeIndex + 1;
        }

        result = builder.ToString();
        return true;
    }
}
}
