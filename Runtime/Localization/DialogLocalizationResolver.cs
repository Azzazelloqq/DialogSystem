namespace DialogSystem.Runtime.Localization
{
public static class DialogLocalizationResolver
{
    public static string Resolve(string key, string fallback, IDialogLocalizationProvider provider)
    {
        if (provider != null && !string.IsNullOrWhiteSpace(key) && provider.TryGet(key, out var text))
        {
            return text;
        }

        return fallback;
    }
}
}
