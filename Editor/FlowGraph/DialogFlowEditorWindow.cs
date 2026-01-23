using DialogSystem.Runtime.Flow;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DialogSystem.Editor.FlowGraph
{
public sealed class DialogFlowEditorWindow : EditorWindow
{
    private DialogFlowGraphView _graphView;
    private ObjectField _assetField;
    private DialogFlowAsset _asset;

    [MenuItem("Window/Dialog System/Dialog Flow Editor")]
    public static void OpenWindow()
    {
        var window = GetWindow<DialogFlowEditorWindow>("Dialog Flow Editor");
        window.Show();
    }

    public static void Open(DialogFlowAsset asset)
    {
        var window = GetWindow<DialogFlowEditorWindow>("Dialog Flow Editor");
        window.SetAsset(asset);
        window.Show();
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();

        var toolbar = new Toolbar();
        _assetField = new ObjectField("Flow")
        {
            objectType = typeof(DialogFlowAsset)
        };
        _assetField.RegisterValueChangedCallback(evt =>
        {
            SetAsset(evt.newValue as DialogFlowAsset);
        });
        toolbar.Add(_assetField);

        var addMenu = new ToolbarMenu { text = "Add Node" };
        addMenu.menu.AppendAction("Dialog", _ => _graphView?.CreateNode(DialogFlowNodeType.Dialog));
        addMenu.menu.AppendAction("Action", _ => _graphView?.CreateNode(DialogFlowNodeType.Action));
        addMenu.menu.AppendAction("End", _ => _graphView?.CreateNode(DialogFlowNodeType.End));
        toolbar.Add(addMenu);

        root.Add(toolbar);

        _graphView = new DialogFlowGraphView();
        _graphView.StretchToParentSize();
        root.Add(_graphView);

        if (_asset != null)
        {
            _assetField.SetValueWithoutNotify(_asset);
            _graphView.Load(_asset);
        }
    }

    private void SetAsset(DialogFlowAsset asset)
    {
        _asset = asset;
        if (_assetField != null)
        {
            _assetField.SetValueWithoutNotify(_asset);
        }

        _graphView?.Load(_asset);
    }
}
}
