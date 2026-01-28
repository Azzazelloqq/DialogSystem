using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime.Localization
{
public enum DialogLocalizationKeyMode
{
    None,
    Generate
}

[Serializable]
public sealed class DialogLocaleInfo
{
    public string Code = "en";
    public string DisplayName = "English";
}

[CreateAssetMenu(menuName = "Dialog System/Dialog Localization Settings", fileName = "DialogLocalizationSettings")]
public sealed class DialogLocalizationSettings : ScriptableObject
{
    [SerializeField] private bool _enableLocalization;
    [SerializeField] private DialogLocalizationKeyMode _keyMode = DialogLocalizationKeyMode.Generate;
    [SerializeField] private string _keyFormat = "{dialogId}.{blockId}";
    [SerializeField] private string _defaultLocale = "en";
    [SerializeField] private List<DialogLocaleInfo> _locales = new();
    [SerializeField] private string _localizedDslSuffix = ".{locale}";
    [SerializeField] private bool _cloneBaseOnAdd = true;

    public bool EnableLocalization
    {
        get => _enableLocalization;
        set => _enableLocalization = value;
    }

    public DialogLocalizationKeyMode KeyMode
    {
        get => _keyMode;
        set => _keyMode = value;
    }

    public string KeyFormat
    {
        get => _keyFormat;
        set => _keyFormat = value;
    }

    public string DefaultLocale
    {
        get => _defaultLocale;
        set => _defaultLocale = value;
    }

    public List<DialogLocaleInfo> Locales => _locales;

    public string LocalizedDslSuffix
    {
        get => _localizedDslSuffix;
        set => _localizedDslSuffix = value;
    }

    public bool CloneBaseOnAdd
    {
        get => _cloneBaseOnAdd;
        set => _cloneBaseOnAdd = value;
    }
}
}
