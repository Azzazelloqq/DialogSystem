using System.IO;
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

        if (GUILayout.Button("Open Graph (Read-only)"))
        {
            DialogGraphWindow.Open(asset);
        }

        if (GUILayout.Button("Create Visual Graph"))
        {
            CreateGraphFromDialogAsset(asset, sourcePath);
        }

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

    private static void CreateGraphFromDialogAsset(DialogAsset asset, string sourcePath)
    {
        if (asset == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            EditorUtility.DisplayDialog("Dialog Graph", "Не найден путь исходного .dlg файла.", "OK");
            return;
        }

        var defaultDirectory = Path.GetDirectoryName(sourcePath);
        var defaultName = $"{asset.name}Graph";
        var path = EditorUtility.SaveFilePanelInProject("Create Dialog Graph", defaultName, "asset",
            "Create a visual dialog graph asset.", defaultDirectory);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var graph = ScriptableObject.CreateInstance<DialogGraphAsset>();
        graph.DialogId = asset.GetDefaultDialog()?.Id ?? "main";
        graph.DslPath = sourcePath;
        AssetDatabase.CreateAsset(graph, path);
        AssetDatabase.SaveAssets();

        if (!DialogGraphImportUtility.Import(graph, out var error))
        {
            EditorUtility.DisplayDialog("Dialog Graph", error ?? "Import failed.", "OK");
        }

        DialogGraphEditorWindow.Open(graph);
    }
}
}
