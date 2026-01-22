using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.Editor
{
public sealed class DialogGraphEditorWindow : EditorWindow
{
    private DialogGraphEditorView _graphView;
    private DialogGraphAsset _asset;
    private ObjectField _assetField;
    private TextField _dialogIdField;
    private TextField _dslPathField;
    private Label _warningLabel;

    [MenuItem("Window/Dialog System/Dialog Graph Editor")]
    public static void OpenWindow()
    {
        var window = GetWindow<DialogGraphEditorWindow>("Dialog Graph Editor");
        window.Show();
    }

    public static void Open(DialogGraphAsset asset)
    {
        var window = GetWindow<DialogGraphEditorWindow>("Dialog Graph Editor");
        window.SetAsset(asset);
        window.Show();
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();

        var toolbar = new Toolbar();
        _assetField = new ObjectField("Graph")
        {
            objectType = typeof(DialogGraphAsset)
        };
        _assetField.RegisterValueChangedCallback(evt => SetAsset(evt.newValue as DialogGraphAsset));
        toolbar.Add(_assetField);

        _dialogIdField = new TextField("Dialog Id");
        _dialogIdField.RegisterValueChangedCallback(evt =>
        {
            if (_asset == null)
            {
                return;
            }

            Undo.RecordObject(_asset, "Change Dialog Id");
            _asset.DialogId = evt.newValue;
            EditorUtility.SetDirty(_asset);
        });
        toolbar.Add(_dialogIdField);

        _dslPathField = new TextField("DSL Path");
        _dslPathField.isReadOnly = true;
        toolbar.Add(_dslPathField);

        var pickButton = new Button(() =>
        {
            if (_asset == null)
            {
                return;
            }

            var defaultPath = DialogGraphExportUtility.GetDefaultDslPath(_asset);
            var path = EditorUtility.SaveFilePanelInProject("Dialog DSL", "Dialog", "dlg",
                "Choose where to save the dialog DSL file.", defaultPath);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Undo.RecordObject(_asset, "Change DSL Path");
            _asset.DslPath = path;
            _dslPathField.SetValueWithoutNotify(path);
            EditorUtility.SetDirty(_asset);
        })
        {
            text = "Pick"
        };
        toolbar.Add(pickButton);

        var addMenu = new ToolbarMenu { text = "Add Node" };
        addMenu.menu.AppendAction("Line", _ => _graphView?.CreateNode(DialogGraphNodeType.Line));
        addMenu.menu.AppendAction("Choice", _ => _graphView?.CreateNode(DialogGraphNodeType.Choice));
        addMenu.menu.AppendAction("Condition", _ => _graphView?.CreateNode(DialogGraphNodeType.Condition));
        addMenu.menu.AppendAction("Set", _ => _graphView?.CreateNode(DialogGraphNodeType.Set));
        addMenu.menu.AppendAction("Command", _ => _graphView?.CreateNode(DialogGraphNodeType.Command));
        addMenu.menu.AppendAction("Jump", _ => _graphView?.CreateNode(DialogGraphNodeType.Jump));
        addMenu.menu.AppendAction("Call", _ => _graphView?.CreateNode(DialogGraphNodeType.Call));
        addMenu.menu.AppendAction("Return", _ => _graphView?.CreateNode(DialogGraphNodeType.Return));
        toolbar.Add(addMenu);

        var exportButton = new Button(ExportDsl) { text = "Export .dlg" };
        toolbar.Add(exportButton);

        var importButton = new Button(ImportDsl) { text = "Import .dlg" };
        toolbar.Add(importButton);

        root.Add(toolbar);

        _warningLabel = new Label(string.Empty)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Italic,
                color = new StyleColor(new Color(0.9f, 0.7f, 0.3f))
            }
        };
        root.Add(_warningLabel);

        _graphView = new DialogGraphEditorView();
        _graphView.StretchToParentSize();
        root.Add(_graphView);

        if (_asset != null)
        {
            SetAsset(_asset);
        }
    }

    private void SetAsset(DialogGraphAsset asset)
    {
        _asset = asset;
        if (_assetField != null)
        {
            _assetField.SetValueWithoutNotify(asset);
        }

        if (_dialogIdField != null)
        {
            _dialogIdField.SetValueWithoutNotify(_asset != null ? _asset.DialogId : string.Empty);
        }

        if (_dslPathField != null)
        {
            _dslPathField.SetValueWithoutNotify(_asset != null ? _asset.DslPath : string.Empty);
        }

        if (_graphView != null)
        {
            _graphView.Load(_asset);
        }
    }

    private void ExportDsl()
    {
        _warningLabel.text = string.Empty;
        if (_asset == null)
        {
            return;
        }

        if (!DialogGraphExportUtility.Export(_asset, out var warnings, out var error))
        {
            EditorUtility.DisplayDialog("Dialog Export", error ?? "Export failed.", "OK");
            return;
        }

        _dslPathField.SetValueWithoutNotify(_asset.DslPath);
        if (warnings.Count > 0)
        {
            _warningLabel.text = $"Warnings: {warnings.Count}. Check Console.";
            foreach (var warning in warnings)
            {
                Debug.LogWarning($"[DialogGraph] {warning}", _asset);
            }
        }
    }

    private void ImportDsl()
    {
        _warningLabel.text = string.Empty;
        if (_asset == null)
        {
            return;
        }

        if (!DialogGraphImportUtility.Import(_asset, out var error))
        {
            EditorUtility.DisplayDialog("Dialog Import", error ?? "Import failed.", "OK");
            return;
        }

        _dialogIdField.SetValueWithoutNotify(_asset.DialogId);
        _dslPathField.SetValueWithoutNotify(_asset.DslPath);
        _graphView.Load(_asset);
    }
}
}
