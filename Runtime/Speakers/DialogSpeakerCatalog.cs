using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime.Speakers
{
[Serializable]
public sealed class DialogSpeakerEntry
{
    public string Id;
    public string DisplayName;

    [TextArea(2, 4)]
    public string Description;
}

[CreateAssetMenu(menuName = "Dialog System/Dialog Speaker Catalog", fileName = "DialogSpeakerCatalog")]
public sealed class DialogSpeakerCatalog : ScriptableObject
{
    [SerializeField] private List<DialogSpeakerEntry> _speakers = new();

    public List<DialogSpeakerEntry> Speakers => _speakers;
}
}
