using System.Collections.Generic;

namespace DialogSystem.Runtime
{
public interface IDialogView
{
    void ShowLine(DialogLine line);
    void ShowChoices(IReadOnlyList<DialogChoiceOption> choices);
    void OnDialogEnded();
    void OnDialogError(string message);
}
}
