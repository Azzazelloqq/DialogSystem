namespace DialogSystem.Runtime.Localization
{
public interface IDialogLocalizationProvider
{
    bool TryGet(string key, out string text);
}
}
