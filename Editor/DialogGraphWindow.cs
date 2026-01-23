using System.Collections.Generic;
using System.IO;
using DialogSystem.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.Editor
{
public sealed class DialogGraphWindow : EditorWindow
{
    private DialogGraphView _graphView;
    private ObjectField _assetField;
    private PopupField<string> _dialogPopup;
    private HelpBox _infoBox;
    private Button _createGraphButton;
    private DialogAsset _asset;
    private string _dialogId;

    public static void Open(DialogAsset asset)
    {
        var window = GetWindow<DialogGraphWindow>("Dialog Graph");
        window.SetAsset(asset);
        window.Show();
    }

    [MenuItem("Window/Dialog System/Dialog Graph")]
    public static void OpenWindow()
    {
        var window = GetWindow<DialogGraphWindow>("Dialog Graph");
        window.Show();
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();

        var toolbar = new Toolbar();
        _assetField = new ObjectField("Dialog Asset")
        {
            objectType = typeof(DialogAsset)
        };
        _assetField.RegisterValueChangedCallback(evt =>
        {
            SetAsset(evt.newValue as DialogAsset);
        });
        toolbar.Add(_assetField);

        _dialogPopup = new PopupField<string>("Dialog", new List<string>(), 0);
        _dialogPopup.RegisterValueChangedCallback(evt =>
        {
            _dialogId = evt.newValue;
            RebuildGraph();
        });
        toolbar.Add(_dialogPopup);

        root.Add(toolbar);

        _infoBox = new HelpBox(
            "Это окно только для просмотра. Для редактирования используйте Dialog Graph Editor.",
            HelpBoxMessageType.Info);
        root.Add(_infoBox);

        _createGraphButton = new Button(CreateGraphFromDialogAsset)
        {
            text = "Создать визуальный граф"
        };
        root.Add(_createGraphButton);

        _graphView = new DialogGraphView();
        _graphView.StretchToParentSize();
        root.Add(_graphView);

        if (_asset != null)
        {
            _assetField.SetValueWithoutNotify(_asset);
            RefreshDialogList();
            RebuildGraph();
        }
    }

    private void SetAsset(DialogAsset asset)
    {
        _asset = asset;
        _dialogId = null;
        if (_assetField != null)
        {
            _assetField.SetValueWithoutNotify(_asset);
        }

        if (_createGraphButton != null)
        {
            _createGraphButton.SetEnabled(_asset != null);
        }

        RefreshDialogList();
        RebuildGraph();
    }

    private void RefreshDialogList()
    {
        if (_dialogPopup == null)
        {
            return;
        }

        var dialogIds = new List<string>();
        if (_asset != null)
        {
            foreach (var dialog in _asset.Dialogs)
            {
                if (dialog != null)
                {
                    dialogIds.Add(dialog.Id);
                }
            }
        }

        if (dialogIds.Count == 0)
        {
            dialogIds.Add("(none)");
        }

        _dialogPopup.choices = dialogIds;
        _dialogId ??= dialogIds[0];
        _dialogPopup.SetValueWithoutNotify(_dialogId);
    }

    private void RebuildGraph()
    {
        if (_graphView == null)
        {
            return;
        }

        if (_asset == null || string.IsNullOrWhiteSpace(_dialogId) || _dialogId == "(none)")
        {
            _graphView.ClearGraph();
            return;
        }

        if (!_asset.TryGetDialog(_dialogId, out var dialog))
        {
            _graphView.ClearGraph();
            return;
        }

        _graphView.LoadDialog(dialog);
    }

    private void CreateGraphFromDialogAsset()
    {
        if (_asset == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_asset.SourcePath))
        {
            EditorUtility.DisplayDialog("Dialog Graph", "Не найден путь исходного .dlg файла.", "OK");
            return;
        }

        var defaultDirectory = Path.GetDirectoryName(_asset.SourcePath);
        var defaultName = $"{_asset.name}Graph";
        var path = EditorUtility.SaveFilePanelInProject("Create Dialog Graph", defaultName, "asset",
            "Create a visual dialog graph asset.", defaultDirectory);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var graph = ScriptableObject.CreateInstance<DialogGraphAsset>();
        graph.DialogId = _asset.GetDefaultDialog()?.Id ?? "main";
        graph.DslPath = _asset.SourcePath;
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
