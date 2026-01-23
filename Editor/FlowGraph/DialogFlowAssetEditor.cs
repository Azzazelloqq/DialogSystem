using DialogSystem.Runtime.Flow;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor.FlowGraph
{
[CustomEditor(typeof(DialogFlowAsset))]
public sealed class DialogFlowAssetEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var asset = (DialogFlowAsset)target;

        if (GUILayout.Button("Open Flow Editor"))
        {
            DialogFlowEditorWindow.Open(asset);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Nodes", asset.Nodes.Count.ToString());
    }
}
}
