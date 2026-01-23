using System;
using System.Collections.Generic;
using DialogSystem.Editor.Dsl;
using DialogSystem.Runtime.Conditions;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor
{
public sealed class DialogDslEditorWindow : EditorWindow
{
    private const float LeftPanelWidth = 260f;
    private const string DragDataKey = "DialogDslDrag";

    private DialogDraftAsset _draft;
    private DialogDslDocument _document;
    private Vector2 _scroll;
    private bool _blocksFoldout = true;
    private bool _conditionsFoldout = true;
    private bool _advancedFoldout;

    private readonly List<DropTarget> _dropTargets = new();
    private static int s_conditionsVersion = -1;
    private static string[] s_conditionOptions;
    private static Dictionary<string, int> s_conditionIndexById;

    [MenuItem("Window/Dialog System/Dialog DSL Editor")]
    public static void OpenWindow()
    {
        var window = GetWindow<DialogDslEditorWindow>("Dialog DSL Editor");
        window.Show();
    }

    public static void Open(DialogDraftAsset asset)
    {
        var window = GetWindow<DialogDslEditorWindow>("Dialog DSL Editor");
        window.SetDraft(asset);
        window.Show();
    }

    private void OnGUI()
    {
        DrawHeader();

        if (_document == null)
        {
            EditorGUILayout.HelpBox("Select a Dialog Draft to start editing.", MessageType.Info);
            return;
        }

        _document.DialogId = EditorGUILayout.TextField("Dialog Id", _document.DialogId);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal();
        var newDraft = (DialogDraftAsset)EditorGUILayout.ObjectField("Dialog Draft", _draft, typeof(DialogDraftAsset), false);
        if (newDraft != _draft)
        {
            SetDraft(newDraft);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        var dslPath = _draft != null ? _draft.DslPath : string.Empty;
        EditorGUILayout.LabelField("DSL Path", string.IsNullOrWhiteSpace(dslPath) ? "(none)" : dslPath);
        if (GUILayout.Button("Load", GUILayout.Width(80)))
        {
            LoadFromDraft();
        }
        if (GUILayout.Button("Export", GUILayout.Width(80)))
        {
            SaveToDsl();
        }
        if (GUILayout.Button("Pick", GUILayout.Width(60)))
        {
            PickDslPath();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

        _blocksFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_blocksFoldout, "Blocks");
        if (_blocksFoldout)
        {
            DrawPaletteItem("Line", new DragPayload(DragKind.PaletteLine));
            DrawPaletteItem("Choice Group", new DragPayload(DragKind.PaletteChoiceGroup));
            DrawPaletteItem("Condition Group", new DragPayload(DragKind.PaletteConditionGroup));
            DrawPaletteItem("Exit (Outcome)", new DragPayload(DragKind.PaletteExit));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        _conditionsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_conditionsFoldout, "Conditions");
        if (_conditionsFoldout)
        {
            var conditions = DialogConditionCatalog.GetConditions();
            if (conditions.Count == 0)
            {
                EditorGUILayout.LabelField("No conditions found.");
            }
            else
            {
                foreach (var condition in conditions)
                {
                    DrawPaletteItem(condition.DisplayName,
                        new DragPayload(DragKind.ConditionItem) { ConditionId = condition.Id });
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        _advancedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_advancedFoldout, "Advanced");
        if (_advancedFoldout)
        {
            DrawPaletteItem("Raw DSL Block", new DragPayload(DragKind.PaletteRaw));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.EndVertical();
    }

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        _dropTargets.Clear();
        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;
            if (_document.Blocks.Count == 0)
            {
                var emptyRect = GUILayoutUtility.GetRect(0, 200, GUILayout.ExpandWidth(true));
                GUI.Box(emptyRect, "Перетащите блок из палитры сюда", EditorStyles.helpBox);
                _dropTargets.Add(new DropTarget(emptyRect, DropTargetKind.Insert, _document.Blocks, 0, null, null));
            }
            else
            {
                DrawBlockList(_document.Blocks, 0);
            }

            HandleDragAndDrop(Event.current);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBlockList(List<DialogDslBlock> blocks, int depth)
    {
        if (blocks == null)
        {
            return;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            DrawInsertTarget(blocks, i, depth);
            DrawBlock(blocks[i], blocks, i, depth);
        }

        DrawInsertTarget(blocks, blocks.Count, depth);
    }

    private void DrawBlock(DialogDslBlock block, List<DialogDslBlock> ownerList, int index, int depth)
    {
        if (block == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(block.Id))
        {
            block.Id = Guid.NewGuid().ToString("N");
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        DrawDragHandle(block, ownerList);
        EditorGUILayout.LabelField(block.Type.ToString(), EditorStyles.boldLabel);
        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            ownerList.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (block.Type == DialogDslBlockType.Line)
        {
            var dropRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop condition here", EditorStyles.helpBox);
            _dropTargets.Add(new DropTarget(dropRect, DropTargetKind.Condition, null, -1, block, null));
        }

        EditorGUI.indentLevel = depth;

        switch (block.Type)
        {
            case DialogDslBlockType.Line:
                block.Speaker = EditorGUILayout.TextField("Speaker", block.Speaker);
                block.Text = EditorGUILayout.TextField("Text", block.Text);
                block.Condition = DrawConditionField("Condition", block.Condition, block, null);
                block.StableId = EditorGUILayout.TextField("Line Id", block.StableId);
                DrawTags(block.Tags);
                break;
            case DialogDslBlockType.ChoiceGroup:
                DrawChoiceGroup(block);
                break;
            case DialogDslBlockType.ConditionGroup:
                DrawConditionGroup(block, depth);
                break;
            case DialogDslBlockType.Exit:
                block.Outcome = EditorGUILayout.TextField("Outcome", block.Outcome);
                break;
            case DialogDslBlockType.Raw:
                block.Raw = EditorGUILayout.TextArea(block.Raw, GUILayout.MinHeight(40));
                break;
        }

        EditorGUI.indentLevel = 0;
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DrawChoiceGroup(DialogDslBlock block)
    {
        if (block.Choices == null)
        {
            block.Choices = new List<DialogDslChoice>();
        }

        for (int i = 0; i < block.Choices.Count; i++)
        {
            var choice = block.Choices[i];
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Choice {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                block.Choices.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            var dropRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop condition here", EditorStyles.helpBox);
            _dropTargets.Add(new DropTarget(dropRect, DropTargetKind.Condition, null, -1, null, choice));

            choice.Text = EditorGUILayout.TextField("Text", choice.Text);
            choice.Condition = DrawConditionField("Condition", choice.Condition, null, choice);
            choice.Outcome = EditorGUILayout.TextField("Outcome", choice.Outcome);
            choice.StableId = EditorGUILayout.TextField("Choice Id", choice.StableId);
            DrawTags(choice.Tags);
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Add Choice"))
        {
            block.Choices.Add(new DialogDslChoice());
        }
    }

    private void DrawConditionGroup(DialogDslBlock block, int depth)
    {
        if (block.Children == null)
        {
            block.Children = new List<DialogDslBlock>();
        }

        block.Condition = DrawConditionField("Condition", block.Condition, block, null);

        var dropRect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drop blocks here to add into group", EditorStyles.helpBox);
        _dropTargets.Add(new DropTarget(dropRect, DropTargetKind.Group, block.Children, block.Children.Count, block, null));

        EditorGUILayout.Space();
        DrawBlockList(block.Children, depth + 1);
    }

    private void DrawInsertTarget(List<DialogDslBlock> list, int index, int depth)
    {
        var rect = GUILayoutUtility.GetRect(0, 10, GUILayout.ExpandWidth(true));
        if (IsDragOver(rect))
        {
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.6f, 0.9f, 0.4f));
        }

        _dropTargets.Add(new DropTarget(rect, DropTargetKind.Insert, list, index, null, null));
    }

    private string DrawConditionField(string label, string value, DialogDslBlock targetBlock, DialogDslChoice targetChoice)
    {
        EditorGUILayout.BeginHorizontal();
        var rect = EditorGUILayout.GetControlRect();
        value = EditorGUI.TextField(rect, label, value);
        _dropTargets.Add(new DropTarget(rect, DropTargetKind.Condition, null, -1, targetBlock, targetChoice));

        EnsureConditionCache();
        if (s_conditionOptions != null && s_conditionOptions.Length > 1)
        {
            var selected = GetConditionPopupIndex(value);
            var newSelected = EditorGUILayout.Popup(selected, s_conditionOptions, GUILayout.Width(160));
            if (newSelected > 0 && newSelected - 1 < s_conditionIndexById.Count)
            {
                value = GetConditionIdByIndex(newSelected - 1);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrWhiteSpace(value) &&
            DialogConditionParser.TryParse(value, out var spec) &&
            !LooksLikeExpression(value))
        {
            var argsText = spec.Args.Raw ?? string.Empty;
            var newArgs = EditorGUILayout.TextField("Args", argsText);
            if (!string.Equals(argsText, newArgs))
            {
                value = string.IsNullOrWhiteSpace(newArgs)
                    ? spec.Id
                    : $"{spec.Id}({newArgs})";
            }
        }

        return value;
    }

    private void DrawTags(List<string> tags)
    {
        if (tags == null)
        {
            return;
        }

        var tagsLine = EditorGUILayout.TextField("Tags (comma)", string.Join(", ", tags));
        tags.Clear();
        if (!string.IsNullOrWhiteSpace(tagsLine))
        {
            foreach (var part in tagsLine.Split(','))
            {
                var tag = part.Trim();
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    tags.Add(tag);
                }
            }
        }
    }

    private void DrawPaletteItem(string label, DragPayload payload)
    {
        var rect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
        GUI.Box(rect, label, EditorStyles.helpBox);

        var evt = Event.current;
        if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
        {
            StartDrag(label, payload);
            evt.Use();
        }
    }

    private void DrawDragHandle(DialogDslBlock block, List<DialogDslBlock> ownerList)
    {
        var rect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
        GUI.Label(rect, "≡");
        var evt = Event.current;
        if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
        {
            StartDrag("Move", new DragPayload(DragKind.ExistingBlock)
            {
                Block = block,
                SourceList = ownerList
            });
            evt.Use();
        }
    }

    private void HandleDragAndDrop(Event evt)
    {
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
        {
            return;
        }

        var payload = DragAndDrop.GetGenericData(DragDataKey) as DragPayload;
        if (payload == null)
        {
            return;
        }

        if (!TryGetDropTarget(evt.mousePosition, out var target))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            return;
        }

        var canAccept = CanAcceptDrop(payload, target);
        if (!canAccept)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            return;
        }

        DragAndDrop.visualMode = payload.Kind == DragKind.ExistingBlock
            ? DragAndDropVisualMode.Move
            : DragAndDropVisualMode.Copy;

        if (evt.type == EventType.DragPerform)
        {
            ApplyDrop(payload, target);
            DragAndDrop.AcceptDrag();
            evt.Use();
        }
    }

    private bool CanAcceptDrop(DragPayload payload, DropTarget target)
    {
        if (payload == null)
        {
            return false;
        }

        switch (target.Kind)
        {
            case DropTargetKind.Insert:
                return payload.Kind == DragKind.ExistingBlock ||
                       payload.Kind == DragKind.PaletteLine ||
                       payload.Kind == DragKind.PaletteChoiceGroup ||
                       payload.Kind == DragKind.PaletteConditionGroup ||
                       payload.Kind == DragKind.PaletteExit ||
                       payload.Kind == DragKind.PaletteRaw ||
                       payload.Kind == DragKind.ConditionItem;
            case DropTargetKind.Condition:
                return payload.Kind == DragKind.ConditionItem;
            case DropTargetKind.Group:
                return payload.Kind == DragKind.ExistingBlock ||
                       payload.Kind == DragKind.PaletteLine ||
                       payload.Kind == DragKind.PaletteChoiceGroup ||
                       payload.Kind == DragKind.PaletteConditionGroup ||
                       payload.Kind == DragKind.PaletteExit ||
                       payload.Kind == DragKind.PaletteRaw;
            default:
                return false;
        }
    }

    private void ApplyDrop(DragPayload payload, DropTarget target)
    {
        if (payload == null)
        {
            return;
        }

        if (payload.Kind == DragKind.ConditionItem)
        {
            if (target.Kind == DropTargetKind.Condition && target.TargetBlock != null)
            {
                target.TargetBlock.Condition = payload.ConditionId;
                return;
            }

            if (target.Kind == DropTargetKind.Condition && target.TargetChoice != null)
            {
                target.TargetChoice.Condition = payload.ConditionId;
                return;
            }

            if (target.Kind == DropTargetKind.Insert)
            {
                var group = CreateBlock(DialogDslBlockType.ConditionGroup);
                group.Condition = payload.ConditionId;
                target.List.Insert(target.Index, group);
                return;
            }
        }

        if (payload.Kind == DragKind.ExistingBlock && payload.Block != null)
        {
            MoveBlock(payload.Block, payload.SourceList, target);
            return;
        }

        var created = CreateBlock(GetBlockType(payload.Kind));
        if (created == null)
        {
            return;
        }

        if (target.Kind == DropTargetKind.Group)
        {
            target.List.Add(created);
        }
        else if (target.Kind == DropTargetKind.Insert)
        {
            target.List.Insert(target.Index, created);
        }
    }

    private void MoveBlock(DialogDslBlock block, List<DialogDslBlock> sourceList, DropTarget target)
    {
        if (block == null || sourceList == null)
        {
            return;
        }

        if (target.Kind == DropTargetKind.Condition)
        {
            return;
        }

        var index = sourceList.IndexOf(block);
        if (index >= 0)
        {
            sourceList.RemoveAt(index);
        }

        if (target.List == null)
        {
            return;
        }

        var insertIndex = target.Index;
        if (target.List == sourceList && index >= 0 && index < insertIndex)
        {
            insertIndex = Mathf.Max(0, insertIndex - 1);
        }

        if (target.Kind == DropTargetKind.Group)
        {
            target.List.Add(block);
        }
        else
        {
            insertIndex = Mathf.Clamp(insertIndex, 0, target.List.Count);
            target.List.Insert(insertIndex, block);
        }
    }

    private static DialogDslBlockType GetBlockType(DragKind kind)
    {
        return kind switch
        {
            DragKind.PaletteLine => DialogDslBlockType.Line,
            DragKind.PaletteChoiceGroup => DialogDslBlockType.ChoiceGroup,
            DragKind.PaletteConditionGroup => DialogDslBlockType.ConditionGroup,
            DragKind.PaletteExit => DialogDslBlockType.Exit,
            DragKind.PaletteRaw => DialogDslBlockType.Raw,
            _ => DialogDslBlockType.Line
        };
    }

    private static DialogDslBlock CreateBlock(DialogDslBlockType type)
    {
        var block = new DialogDslBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type
        };

        if (type == DialogDslBlockType.ChoiceGroup)
        {
            block.Choices.Add(new DialogDslChoice());
        }

        if (type == DialogDslBlockType.ConditionGroup)
        {
            block.Children = new List<DialogDslBlock>();
        }

        return block;
    }

    private static bool LooksLikeExpression(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.IndexOfAny(new[] { ' ', '+', '-', '*', '/', '=', '>', '<', '&', '|', '!' }) >= 0;
    }

    private static bool IsDragOver(Rect rect)
    {
        if (DragAndDrop.GetGenericData(DragDataKey) == null)
        {
            return false;
        }

        return rect.Contains(Event.current.mousePosition);
    }

    private void StartDrag(string label, DragPayload payload)
    {
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
        DragAndDrop.SetGenericData(DragDataKey, payload);
        DragAndDrop.StartDrag(label);
    }

    private bool TryGetDropTarget(Vector2 mousePosition, out DropTarget target)
    {
        target = default;
        var found = false;
        var bestPriority = -1;
        var bestArea = float.MaxValue;

        for (int i = 0; i < _dropTargets.Count; i++)
        {
            var candidate = _dropTargets[i];
            if (!candidate.Rect.Contains(mousePosition))
            {
                continue;
            }

            var priority = GetPriority(candidate.Kind);
            var area = candidate.Rect.width * candidate.Rect.height;
            if (!found || priority > bestPriority || (priority == bestPriority && area < bestArea))
            {
                target = candidate;
                found = true;
                bestPriority = priority;
                bestArea = area;
            }
        }

        return found;
    }

    private static int GetPriority(DropTargetKind kind)
    {
        return kind switch
        {
            DropTargetKind.Condition => 3,
            DropTargetKind.Insert => 2,
            DropTargetKind.Group => 1,
            _ => 0
        };
    }

    private static void EnsureConditionCache()
    {
        var conditions = DialogConditionCatalog.GetConditions();
        if (DialogConditionCatalog.Version == s_conditionsVersion && s_conditionOptions != null)
        {
            return;
        }

        s_conditionsVersion = DialogConditionCatalog.Version;
        s_conditionOptions = null;
        s_conditionIndexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (conditions == null || conditions.Count == 0)
        {
            s_conditionOptions = new[] { "<None>" };
            return;
        }

        s_conditionOptions = new string[conditions.Count + 1];
        s_conditionOptions[0] = "<Pick>";
        for (int i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            s_conditionOptions[i + 1] = condition.DisplayName;
            if (!string.IsNullOrWhiteSpace(condition.Id))
            {
                s_conditionIndexById[condition.Id] = i;
            }
        }
    }

    private static int GetConditionPopupIndex(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || s_conditionIndexById == null)
        {
            return 0;
        }

        if (DialogConditionParser.TryParse(value, out var spec) &&
            !string.IsNullOrWhiteSpace(spec.Id) &&
            s_conditionIndexById.TryGetValue(spec.Id, out var index))
        {
            return index + 1;
        }

        if (s_conditionIndexById.TryGetValue(value.Trim(), out var directIndex))
        {
            return directIndex + 1;
        }

        return 0;
    }

    private static string GetConditionIdByIndex(int index)
    {
        if (s_conditionIndexById == null)
        {
            return null;
        }

        foreach (var pair in s_conditionIndexById)
        {
            if (pair.Value == index)
            {
                return pair.Key;
            }
        }

        return null;
    }

    private void LoadFromDraft()
    {
        if (_draft == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_draft.DslPath))
        {
            _document = DialogDslEditorParser.FromDraft(_draft);
            return;
        }

        var text = System.IO.File.ReadAllText(_draft.DslPath);
        _document = DialogDslEditorParser.Parse(text);
        Undo.RecordObject(_draft, "Import Dialog DSL");
        DialogDslEditorParser.ApplyToDraft(_draft, _document);
        EditorUtility.SetDirty(_draft);
    }

    private void SetDraft(DialogDraftAsset asset)
    {
        _draft = asset;
        _document = asset != null ? DialogDslEditorParser.FromDraft(asset) : null;
    }

    private void SaveToDsl()
    {
        if (_draft == null || _document == null)
        {
            return;
        }

        Undo.RecordObject(_draft, "Save Dialog Draft");
        DialogDslEditorParser.ApplyToDraft(_draft, _document);
        EditorUtility.SetDirty(_draft);

        if (string.IsNullOrWhiteSpace(_draft.DslPath))
        {
            PickDslPath();
            if (string.IsNullOrWhiteSpace(_draft.DslPath))
            {
                return;
            }
        }

        DialogDslEditorParser.Save(_document, _draft.DslPath);
        AssetDatabase.ImportAsset(_draft.DslPath);
    }

    private void PickDslPath()
    {
        if (_draft == null)
        {
            return;
        }

        var path = EditorUtility.SaveFilePanelInProject("Dialog DSL", _draft.name, "dlg",
            "Choose where to save the dialog DSL file.");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Undo.RecordObject(_draft, "Set DSL Path");
        _draft.DslPath = path;
        EditorUtility.SetDirty(_draft);
    }

    private enum DragKind
    {
        PaletteLine,
        PaletteChoiceGroup,
        PaletteConditionGroup,
        PaletteExit,
        PaletteRaw,
        ConditionItem,
        ExistingBlock
    }

    private sealed class DragPayload
    {
        public DragKind Kind { get; }
        public string ConditionId { get; set; }
        public DialogDslBlock Block { get; set; }
        public List<DialogDslBlock> SourceList { get; set; }

        public DragPayload(DragKind kind)
        {
            Kind = kind;
        }
    }

    private enum DropTargetKind
    {
        Insert,
        Condition,
        Group
    }

    private readonly struct DropTarget
    {
        public Rect Rect { get; }
        public DropTargetKind Kind { get; }
        public List<DialogDslBlock> List { get; }
        public int Index { get; }
        public DialogDslBlock TargetBlock { get; }
        public DialogDslChoice TargetChoice { get; }

        public DropTarget(Rect rect, DropTargetKind kind, List<DialogDslBlock> list, int index,
            DialogDslBlock targetBlock, DialogDslChoice targetChoice)
        {
            Rect = rect;
            Kind = kind;
            List = list;
            Index = index;
            TargetBlock = targetBlock;
            TargetChoice = targetChoice;
        }
    }
}
}
