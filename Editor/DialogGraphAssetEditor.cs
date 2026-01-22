using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor
{
[CustomEditor(typeof(DialogGraphAsset))]
public sealed class DialogGraphAssetEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var asset = (DialogGraphAsset)target;

        EditorGUILayout.LabelField("Dialog Id", asset.DialogId);
        EditorGUILayout.LabelField("DSL Path", string.IsNullOrWhiteSpace(asset.DslPath) ? "(auto)" : asset.DslPath);
        EditorGUILayout.Space();

        if (GUILayout.Button("Open Visual Editor"))
        {
            DialogGraphEditorWindow.Open(asset);
        }

        if (GUILayout.Button("Export .dlg"))
        {
            if (!DialogGraphExportUtility.Export(asset, out var warnings, out var error))
            {
                EditorUtility.DisplayDialog("Dialog Export", error ?? "Export failed.", "OK");
                return;
            }

            if (warnings.Count > 0)
            {
                foreach (var warning in warnings)
                {
                    Debug.LogWarning($"[DialogGraph] {warning}", asset);
                }
            }
        }

        if (GUILayout.Button("Import .dlg"))
        {
            if (!DialogGraphImportUtility.Import(asset, out var error))
            {
                EditorUtility.DisplayDialog("Dialog Import", error ?? "Import failed.", "OK");
            }
        }
    }
}
}
