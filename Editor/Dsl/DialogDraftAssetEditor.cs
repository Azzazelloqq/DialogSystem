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
        EditorGUILayout.LabelField("DSL Path", string.IsNullOrWhiteSpace(asset.DslPath) ? "(none)" : asset.DslPath);
        EditorGUILayout.Space();

        if (GUILayout.Button("Open Dialog Editor"))
        {
            DialogDslEditorWindow.Open(asset);
        }
    }
}
}
