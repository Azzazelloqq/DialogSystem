using System.IO;
using DialogSystem.Runtime;
using DialogSystem.Runtime.Dsl;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DialogSystem.Editor
{
[ScriptedImporter(1, "dlg")]
public sealed class DialogImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var text = File.ReadAllText(ctx.assetPath);
        var result = DialogDslParser.Parse(text, ctx.assetPath);

        var asset = ScriptableObject.CreateInstance<DialogAsset>();
        asset.SetData(ctx.assetPath, result.Dialogs, result.Errors);

        ctx.AddObjectToAsset("DialogAsset", asset);
        ctx.SetMainObject(asset);

        foreach (var error in result.Errors)
        {
            ctx.LogImportError($"{error.Message} (line {error.Line})");
        }
    }
}
}
