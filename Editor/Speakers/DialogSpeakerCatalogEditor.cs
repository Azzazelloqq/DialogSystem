using System;
using DialogSystem.Runtime.Speakers;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor.Speakers
{
[CustomEditor(typeof(DialogSpeakerCatalog))]
public sealed class DialogSpeakerCatalogEditor : UnityEditor.Editor
{
    private string _search;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Speakers", EditorStyles.boldLabel);
        _search = EditorGUILayout.TextField("Search", _search);

        var speakersProp = serializedObject.FindProperty("_speakers");
        if (GUILayout.Button("Add Speaker"))
        {
            var index = speakersProp.arraySize;
            speakersProp.arraySize++;
            var element = speakersProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("Id").stringValue = $"speaker_{index + 1}";
        }

        EditorGUILayout.Space();
        for (int i = 0; i < speakersProp.arraySize; i++)
        {
            var element = speakersProp.GetArrayElementAtIndex(i);
            var idProp = element.FindPropertyRelative("Id");
            var nameProp = element.FindPropertyRelative("DisplayName");
            var descProp = element.FindPropertyRelative("Description");

            if (!MatchesSearch(_search, idProp.stringValue, nameProp.stringValue, descProp.stringValue))
            {
                continue;
            }

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(idProp);
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                speakersProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(nameProp);
            EditorGUILayout.PropertyField(descProp);
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool MatchesSearch(string search, params string[] values)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var needle = search.Trim();
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
}
