using DialogSystem.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DialogSystem.Editor
{
[CustomEditor(typeof(DialogAsset))]
public sealed class DialogAssetEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var asset = (DialogAsset)target;
        var sourcePath = string.IsNullOrWhiteSpace(asset.SourcePath)
            ? AssetDatabase.GetAssetPath(asset)
            : asset.SourcePath;

        EditorGUILayout.LabelField("Source", sourcePath);
        EditorGUILayout.LabelField("Dialogs", asset.Dialogs.Count.ToString());

        if (asset.ParseErrors != null && asset.ParseErrors.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Parse Errors", EditorStyles.boldLabel);

            foreach (var error in asset.ParseErrors)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Line {error.Line}: {error.Message}", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Open", GUILayout.Width(60)))
                    {
                        InternalEditorUtility.OpenFileAtLineExternal(sourcePath, error.Line);
                    }
                }
            }
        }
    }
}
}
