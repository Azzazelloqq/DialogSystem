using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Editor.Dsl
{
[CreateAssetMenu(menuName = "Dialog System/Dialog Draft", fileName = "DialogDraft")]
public sealed class DialogDraftAsset : ScriptableObject
{
    [SerializeField] private string _dialogId = "main";
    [SerializeField] private string _dslPath;
    [SerializeField] private List<DialogDslBlock> _blocks = new();

    public string DialogId
    {
        get => _dialogId;
        set => _dialogId = value;
    }

    public string DslPath
    {
        get => _dslPath;
        set => _dslPath = value;
    }

    public List<DialogDslBlock> Blocks => _blocks;
}
}
