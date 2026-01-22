using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor
{
public static class DialogGraphExportUtility
{
    public static bool Export(DialogGraphAsset asset, out List<string> warnings, out string error)
    {
        warnings = new List<string>();
        error = null;

        if (asset == null)
        {
            error = "Dialog graph asset is null.";
            return false;
        }

        var path = asset.DslPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = GetDefaultDslPath(asset);
            asset.DslPath = path;
            EditorUtility.SetDirty(asset);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "DSL path is empty.";
            return false;
        }

        if (!path.StartsWith("Assets/") && !path.StartsWith("Assets\\"))
        {
            error = "DSL path must be inside the Assets folder.";
            return false;
        }

        var dsl = DialogGraphCompiler.Compile(asset, out warnings);
        File.WriteAllText(path, dsl, new UTF8Encoding(true));
        AssetDatabase.ImportAsset(path);
        return true;
    }

    public static string GetDefaultDslPath(DialogGraphAsset asset)
    {
        var assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(assetPath);
        var name = Path.GetFileNameWithoutExtension(assetPath);
        return $"{directory}/{name}.dlg";
    }
}
}
