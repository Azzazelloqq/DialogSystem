using System;

namespace DialogSystem.Runtime
{
public readonly struct DialogTarget
{
    public readonly string DialogId;
    public readonly string LabelId;

    public DialogTarget(string dialogId, string labelId)
    {
        DialogId = dialogId;
        LabelId = labelId;
    }

    public static bool TryParse(string raw, string defaultDialogId, out DialogTarget target)
    {
        target = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        var dotIndex = trimmed.IndexOf('.');
        if (dotIndex > 0 && dotIndex < trimmed.Length - 1)
        {
            var dialogId = trimmed.Substring(0, dotIndex).Trim();
            var labelId = trimmed.Substring(dotIndex + 1).Trim();
            if (string.IsNullOrWhiteSpace(dialogId) || string.IsNullOrWhiteSpace(labelId))
            {
                return false;
            }

            target = new DialogTarget(dialogId, labelId);
            return true;
        }

        if (string.IsNullOrWhiteSpace(defaultDialogId))
        {
            return false;
        }

        target = new DialogTarget(defaultDialogId, trimmed);
        return true;
    }
}
}
