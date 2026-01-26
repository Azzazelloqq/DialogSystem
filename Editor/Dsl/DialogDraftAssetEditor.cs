using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor.Dsl
{
[CustomEditor(typeof(DialogDraftAsset))]
public sealed class DialogDraftAssetEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var asset = (DialogDraftAsset)target;

        EditorGUILayout.LabelField("Dialog Id", asset.DialogId);
        EditorGUILayout.LabelField("DSL Folder", string.IsNullOrWhiteSpace(asset.DslPath) ? "(none)" : asset.DslPath);
        asset.LocalizationSettings = (DialogSystem.Runtime.Localization.DialogLocalizationSettings)EditorGUILayout.ObjectField(
            "Localization Settings", asset.LocalizationSettings, typeof(DialogSystem.Runtime.Localization.DialogLocalizationSettings), false);
        asset.SpeakerCatalog = (DialogSystem.Runtime.Speakers.DialogSpeakerCatalog)EditorGUILayout.ObjectField(
            "Speaker Catalog", asset.SpeakerCatalog, typeof(DialogSystem.Runtime.Speakers.DialogSpeakerCatalog), false);
        EditorGUILayout.Space();

        if (GUILayout.Button("Open Dialog Editor"))
        {
            DialogDslEditorWindow.Open(asset);
        }
    }
}
}
