using System.Collections.Generic;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Speakers;
using UnityEngine;

namespace DialogSystem.Editor.Dsl
{
[CreateAssetMenu(menuName = "Dialog System/Dialog Draft", fileName = "DialogDraft")]
public sealed class DialogDraftAsset : ScriptableObject
{
    [SerializeField] private string _dialogId = "main";
    [SerializeField] private string _dslPath;
    [SerializeField] private List<DialogDslBlock> _blocks = new();
    [SerializeField] private DialogLocalizationSettings _localizationSettings;
    [SerializeField] private List<DialogLocalizationVariant> _localizations = new();
    [SerializeField] private DialogSpeakerCatalog _speakerCatalog;
    [SerializeField] private List<string> _dialogSpeakers = new();

    public string DialogId
    {
        get => _dialogId;
        set => _dialogId = value;
    }

    public string DslPath
    {
        get => _dslPath;
        set => _dslPath = value;
    }

    public List<DialogDslBlock> Blocks => _blocks;

    public DialogLocalizationSettings LocalizationSettings
    {
        get => _localizationSettings;
        set => _localizationSettings = value;
    }

    public List<DialogLocalizationVariant> Localizations => _localizations;

    public DialogSpeakerCatalog SpeakerCatalog
    {
        get => _speakerCatalog;
        set => _speakerCatalog = value;
    }

    public List<string> DialogSpeakers
    {
        get => _dialogSpeakers;
        set => _dialogSpeakers = value;
    }
}
}
