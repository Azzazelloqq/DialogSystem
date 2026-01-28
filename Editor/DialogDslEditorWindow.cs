using System;
using System.Collections.Generic;
using DialogSystem.Editor.Dsl;
using DialogSystem.Runtime.Conditions;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Speakers;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Editor
{
public sealed class DialogDslEditorWindow : EditorWindow
{
    private const float LeftPanelWidth = 260f;
    private const string DragDataKey = "DialogDslDrag";
    private const float LineTextMinHeight = 60f;

    private DialogDraftAsset _draft;
    private DialogDslDocument _document;
    private Vector2 _scroll;
    private string _activeLocale;
    private int _localePopupIndex;
    private bool _blocksFoldout = true;
    private bool _conditionsFoldout = true;
    private bool _advancedFoldout;
    private bool _speakersFoldout;
    private bool _settingsFoldout = true;
    private bool _showPalette = true;
    private bool _showSettings = true;
    private string _speakerSearch;
    private string _catalogSearch;
    private string _newSpeakerName;
    private readonly Dictionary<string, bool> _advancedBlockMeta = new();
    private static Dictionary<DialogDslBlockType, GUIStyle> s_blockHeaderStyles;

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

        EditorGUILayout.Space();
        if (_showPalette)
        {
            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            DrawRightPanel();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        var newDraft = (DialogDraftAsset)EditorGUILayout.ObjectField("Dialog Draft", _draft, typeof(DialogDraftAsset), false);
        if (newDraft != _draft)
        {
            SetDraft(newDraft);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        var dslPath = _draft != null ? _draft.DslPath : string.Empty;
        EditorGUILayout.LabelField("DSL Folder", string.IsNullOrWhiteSpace(dslPath) ? "(none)" : dslPath);
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
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _showSettings = GUILayout.Toggle(_showSettings, "Settings", EditorStyles.toolbarButton);
        _showPalette = GUILayout.Toggle(_showPalette, "Palette", EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();

        if (_showSettings)
        {
            _settingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_settingsFoldout, "Dialog Settings");
            if (_settingsFoldout)
            {
                EditorGUILayout.BeginVertical("box");
                if (_document != null)
                {
                    _document.DialogId = EditorGUILayout.TextField("Dialog Id", _document.DialogId);
                }

                DrawLocalizationHeader();
                DrawSpeakerSection();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }

    private void DrawLocalizationHeader()
    {
        if (_draft == null)
        {
            return;
        }

        var settings = _draft.LocalizationSettings;
        EditorGUILayout.BeginHorizontal();
        var newSettings = (DialogLocalizationSettings)EditorGUILayout.ObjectField(
            "Localization Settings", settings, typeof(DialogLocalizationSettings), false);
        if (newSettings != settings)
        {
            Undo.RecordObject(_draft, "Change Localization Settings");
            _draft.LocalizationSettings = newSettings;
            EditorUtility.SetDirty(_draft);
            _activeLocale = null;
            _localePopupIndex = 0;
        }
        EditorGUILayout.EndHorizontal();

        if (settings == null || !settings.EnableLocalization)
        {
            _activeLocale = null;
            _localePopupIndex = 0;
            return;
        }

        EnsureLocalizationKeys(_document);

        var locales = settings.Locales;
        var options = new List<string> { "Base" };
        foreach (var locale in locales)
        {
            if (locale == null || string.IsNullOrWhiteSpace(locale.Code))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(locale.DisplayName)
                ? locale.Code
                : $"{locale.DisplayName} ({locale.Code})";
            options.Add(label);
        }

        if (_localePopupIndex >= options.Count)
        {
            SetLocaleByIndex(0, settings);
        }

        if (options.Count <= 7)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var toolbarIndex = GUILayout.Toolbar(_localePopupIndex, options.ToArray(), EditorStyles.toolbarButton);
            if (toolbarIndex != _localePopupIndex)
            {
                SetLocaleByIndex(toolbarIndex, settings);
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(_localePopupIndex == 0, "Base", EditorStyles.toolbarButton))
            {
                SetLocaleByIndex(0, settings);
            }

            var defaultIndex = GetLocaleIndexByCode(settings, settings.DefaultLocale);
            if (defaultIndex >= 0)
            {
                var optionIndex = defaultIndex + 1;
                var label = $"Default ({settings.DefaultLocale})";
                if (GUILayout.Toggle(_localePopupIndex == optionIndex, label, EditorStyles.toolbarButton))
                {
                    SetLocaleByIndex(optionIndex, settings);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Locale", GUILayout.Width(50));
            var newIndex = EditorGUILayout.Popup(_localePopupIndex, options.ToArray());
            if (newIndex != _localePopupIndex)
            {
                SetLocaleByIndex(newIndex, settings);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawSpeakerSection()
    {
        if (_draft == null)
        {
            return;
        }

        _speakersFoldout = EditorGUILayout.Foldout(_speakersFoldout, "Speakers", true);
        if (!_speakersFoldout)
        {
            return;
        }

        EditorGUILayout.BeginVertical("box");
        var newCatalog = (DialogSpeakerCatalog)EditorGUILayout.ObjectField(
            "Speaker Catalog", _draft.SpeakerCatalog, typeof(DialogSpeakerCatalog), false);
        if (newCatalog != _draft.SpeakerCatalog)
        {
            Undo.RecordObject(_draft, "Change Speaker Catalog");
            _draft.SpeakerCatalog = newCatalog;
            EditorUtility.SetDirty(_draft);
        }

        DrawSpeakerCatalogList(newCatalog);
        DrawDialogSpeakersList();
        EditorGUILayout.EndVertical();
    }

    private void DrawSpeakerCatalogList(DialogSpeakerCatalog catalog)
    {
        EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
        _catalogSearch = EditorGUILayout.TextField("Search", _catalogSearch);

        if (catalog == null || catalog.Speakers == null || catalog.Speakers.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a speaker catalog to add from it.", MessageType.Info);
            return;
        }

        foreach (var entry in catalog.Speakers)
        {
            if (entry == null)
            {
                continue;
            }

            if (!MatchesSearch(_catalogSearch, entry.Id, entry.DisplayName, entry.Description))
            {
                continue;
            }

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetSpeakerLabel(entry));
            if (GUILayout.Button("Add", GUILayout.Width(60)))
            {
                AddDialogSpeaker(entry);
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                EditorGUILayout.LabelField(entry.Description, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawDialogSpeakersList()
    {
        EnsureDialogSpeakersList();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dialog Speakers", EditorStyles.boldLabel);
        _speakerSearch = EditorGUILayout.TextField("Search", _speakerSearch);

        for (int i = 0; i < _draft.DialogSpeakers.Count; i++)
        {
            var speaker = _draft.DialogSpeakers[i];
            if (!MatchesSearch(_speakerSearch, speaker))
            {
                continue;
            }

            EditorGUILayout.BeginHorizontal();
            var updated = EditorGUILayout.TextField(speaker);
            if (!string.Equals(updated, speaker, StringComparison.Ordinal))
            {
                _draft.DialogSpeakers[i] = updated;
                MarkDraftDirty("Edit Dialog Speaker");
            }

            if (GUILayout.Button("Up", GUILayout.Width(32)) && i > 0)
            {
                SwapDialogSpeakers(i, i - 1);
            }
            if (GUILayout.Button("Down", GUILayout.Width(50)) && i < _draft.DialogSpeakers.Count - 1)
            {
                SwapDialogSpeakers(i, i + 1);
            }
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                _draft.DialogSpeakers.RemoveAt(i);
                MarkDraftDirty("Remove Dialog Speaker");
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        _newSpeakerName = EditorGUILayout.TextField("Add Speaker", _newSpeakerName);
        if (GUILayout.Button("Add", GUILayout.Width(60)))
        {
            TryAddDialogSpeaker(_newSpeakerName);
            _newSpeakerName = string.Empty;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void EnsureDialogSpeakersList()
    {
        if (_draft.DialogSpeakers != null)
        {
            return;
        }

        _draft.DialogSpeakers = new List<string>();
        MarkDraftDirty("Init Dialog Speakers");
    }

    private void AddDialogSpeaker(DialogSpeakerEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        var name = !string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.DisplayName.Trim() : entry.Id?.Trim();
        TryAddDialogSpeaker(name);
    }

    private void TryAddDialogSpeaker(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        EnsureDialogSpeakersList();
        foreach (var speaker in _draft.DialogSpeakers)
        {
            if (string.Equals(speaker, name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        _draft.DialogSpeakers.Add(name.Trim());
        MarkDraftDirty("Add Dialog Speaker");
    }

    private void SwapDialogSpeakers(int indexA, int indexB)
    {
        if (indexA < 0 || indexB < 0 || indexA >= _draft.DialogSpeakers.Count || indexB >= _draft.DialogSpeakers.Count)
        {
            return;
        }

        var temp = _draft.DialogSpeakers[indexA];
        _draft.DialogSpeakers[indexA] = _draft.DialogSpeakers[indexB];
        _draft.DialogSpeakers[indexB] = temp;
        MarkDraftDirty("Reorder Dialog Speakers");
    }

    private string DrawSpeakerField(string label, string value)
    {
        var speakers = _draft != null ? _draft.DialogSpeakers : null;
        if (speakers == null || speakers.Count == 0)
        {
            return EditorGUILayout.TextField(label, value);
        }

        var options = new string[speakers.Count + 1];
        options[0] = "(manual)";
        for (int i = 0; i < speakers.Count; i++)
        {
            options[i + 1] = speakers[i];
        }

        var selectedIndex = GetSpeakerIndex(value, speakers);
        var newIndex = EditorGUILayout.Popup(label, selectedIndex, options);
        if (newIndex != selectedIndex && newIndex > 0)
        {
            value = speakers[newIndex - 1];
        }

        if (newIndex == 0)
        {
            value = EditorGUILayout.TextField("Custom Speaker", value);
        }

        return value;
    }

    private static int GetSpeakerIndex(string value, List<string> speakers)
    {
        if (speakers == null || speakers.Count == 0 || string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        for (int i = 0; i < speakers.Count; i++)
        {
            if (string.Equals(value, speakers[i], StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }

        return 0;
    }

    private static string GetSpeakerLabel(DialogSpeakerEntry entry)
    {
        if (entry == null)
        {
            return "(none)";
        }

        if (string.IsNullOrWhiteSpace(entry.DisplayName))
        {
            return entry.Id ?? "(unnamed)";
        }

        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            return entry.DisplayName;
        }

        return $"{entry.DisplayName} ({entry.Id})";
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

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

        _blocksFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_blocksFoldout, "Blocks");
        if (_blocksFoldout)
        {
            DrawPaletteItem("Line", new DragPayload(DragKind.PaletteLine));
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

        DrawRightToolbar();
        _dropTargets.Clear();
        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;
            if (IsLocalizationActive)
            {
                EnsureLocalizationVariant(_activeLocale, false);
            }
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
        EditorGUILayout.BeginHorizontal(GetBlockHeaderStyle(block.Type));
        DrawDragHandle(block, ownerList);
        var foldoutRect = GUILayoutUtility.GetRect(14, 18, GUILayout.Width(14));
        block.IsCollapsed = !EditorGUI.Foldout(foldoutRect, !block.IsCollapsed, GUIContent.none, true);
        EditorGUILayout.LabelField(block.Type.ToString(), EditorStyles.boldLabel, GUILayout.Width(110));
        var preview = BuildBlockPreview(block);
        if (!string.IsNullOrWhiteSpace(preview))
        {
            GUILayout.Label(preview, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
        }
        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            ownerList.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (block.Type == DialogDslBlockType.Line && !block.IsCollapsed)
        {
            var dropRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            var target = new DropTarget(dropRect, DropTargetKind.Condition, null, -1, block, null);
            if (ShouldHighlight(target))
            {
                EditorGUI.DrawRect(dropRect, new Color(0.3f, 0.6f, 0.9f, 0.4f));
            }
            GUI.Box(dropRect, "Drop condition here", EditorStyles.helpBox);
            _dropTargets.Add(target);
        }

        if (block.IsCollapsed)
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            return;
        }

        EditorGUI.indentLevel = depth;

        switch (block.Type)
        {
            case DialogDslBlockType.Line:
                if (IsLocalizationActive)
                {
                    EditorGUILayout.LabelField("Base", $"{block.Speaker}: {block.Text}");
                    var entry = GetOrCreateLineLocalization(block);
                    EditorGUI.BeginChangeCheck();
                    entry.Speaker = DrawSpeakerField("Speaker", entry.Speaker);
                    entry.Text = EditorGUILayout.TextArea(entry.Text, GUILayout.MinHeight(LineTextMinHeight));
                    if (EditorGUI.EndChangeCheck())
                    {
                        MarkDraftDirty("Edit Localization");
                    }
                }
                else
                {
                    block.Speaker = DrawSpeakerField("Speaker", block.Speaker);
                    block.Text = EditorGUILayout.TextArea(block.Text, GUILayout.MinHeight(LineTextMinHeight));
                }
                block.Condition = DrawConditionField("Condition", block.Condition, block, null);

                var showMeta = GetBlockMetaFoldout(block.Id);
                showMeta = EditorGUILayout.Foldout(showMeta, "Advanced", true);
                SetBlockMetaFoldout(block.Id, showMeta);
                if (showMeta)
                {
                    DrawLocalizationKeyField(block);
                    block.StableId = EditorGUILayout.TextField("Line Id", block.StableId);
                    DrawTags(block.Tags);
                }
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

    private void DrawRightToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Add Line", EditorStyles.toolbarButton))
        {
            AddBlock(DialogDslBlockType.Line);
        }
        if (GUILayout.Button("Add Condition", EditorStyles.toolbarButton))
        {
            AddBlock(DialogDslBlockType.ConditionGroup);
        }
        if (GUILayout.Button("Add Exit", EditorStyles.toolbarButton))
        {
            AddBlock(DialogDslBlockType.Exit);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Collapse All", EditorStyles.toolbarButton))
        {
            SetAllCollapsed(true);
        }
        if (GUILayout.Button("Expand All", EditorStyles.toolbarButton))
        {
            SetAllCollapsed(false);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawChoiceGroup(DialogDslBlock block)
    {
        EditorGUILayout.HelpBox("Choice blocks are managed in the Flow Graph. Remove this block or edit choices there.",
            MessageType.Info);

        if (block.Choices == null || block.Choices.Count == 0)
        {
            EditorGUILayout.LabelField("(empty choice group)");
            return;
        }

        for (int i = 0; i < block.Choices.Count; i++)
        {
            var choice = block.Choices[i];
            if (choice == null)
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(choice.Text) ? "(empty)" : choice.Text;
            if (!string.IsNullOrWhiteSpace(choice.Outcome))
            {
                label = $"{label} -> {choice.Outcome}";
            }

            EditorGUILayout.LabelField($"- {label}");
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
        var target = new DropTarget(dropRect, DropTargetKind.Group, block.Children, block.Children.Count, block, null);
        if (ShouldHighlight(target))
        {
            EditorGUI.DrawRect(dropRect, new Color(0.3f, 0.6f, 0.9f, 0.4f));
        }
        GUI.Box(dropRect, "Drop blocks here to add into group", EditorStyles.helpBox);
        _dropTargets.Add(target);

        EditorGUILayout.Space();
        if (!block.IsCollapsed)
        {
            DrawBlockList(block.Children, depth + 1);
        }
    }

    private void DrawInsertTarget(List<DialogDslBlock> list, int index, int depth)
    {
        var rect = GUILayoutUtility.GetRect(0, 10, GUILayout.ExpandWidth(true));
        var target = new DropTarget(rect, DropTargetKind.Insert, list, index, null, null);
        if (ShouldHighlight(target))
        {
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.6f, 0.9f, 0.4f));
        }

        _dropTargets.Add(target);
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
            DragAndDrop.SetGenericData(DragDataKey, null);
            evt.Use();
            Repaint();
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
            block.Choices.Add(new DialogDslChoice
            {
                Id = Guid.NewGuid().ToString("N")
            });
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

    private void AddBlock(DialogDslBlockType type)
    {
        if (_document == null)
        {
            return;
        }

        _document.Blocks.Add(CreateBlock(type));
        Repaint();
    }

    private void SetAllCollapsed(bool collapsed)
    {
        if (_document?.Blocks == null)
        {
            return;
        }

        foreach (var block in _document.Blocks)
        {
            SetCollapsedRecursive(block, collapsed);
        }

        Repaint();
    }

    private void SetCollapsedRecursive(DialogDslBlock block, bool collapsed)
    {
        if (block == null)
        {
            return;
        }

        block.IsCollapsed = collapsed;
        if (block.Children == null)
        {
            return;
        }

        foreach (var child in block.Children)
        {
            SetCollapsedRecursive(child, collapsed);
        }
    }

    private bool GetBlockMetaFoldout(string blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return false;
        }

        return _advancedBlockMeta.TryGetValue(blockId, out var value) && value;
    }

    private void SetBlockMetaFoldout(string blockId, bool value)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return;
        }

        _advancedBlockMeta[blockId] = value;
    }

    private static string BuildLinePreview(DialogDslBlock block)
    {
        if (block == null)
        {
            return string.Empty;
        }

        var speaker = block.Speaker;
        var text = block.Text;
        var preview = string.IsNullOrWhiteSpace(speaker) ? text : $"{speaker}: {text}";
        return Truncate(preview, 60);
    }

    private static string BuildBlockPreview(DialogDslBlock block)
    {
        if (block == null)
        {
            return string.Empty;
        }

        return block.Type switch
        {
            DialogDslBlockType.Line => BuildLinePreview(block),
            DialogDslBlockType.ConditionGroup => Truncate(string.IsNullOrWhiteSpace(block.Condition)
                ? "if true"
                : $"if {block.Condition}", 60),
            DialogDslBlockType.Exit => Truncate(string.IsNullOrWhiteSpace(block.Outcome)
                ? "exit"
                : $"exit {block.Outcome}", 60),
            DialogDslBlockType.Raw => Truncate(block.Raw, 60),
            _ => string.Empty
        };
    }

    private static GUIStyle GetBlockHeaderStyle(DialogDslBlockType type)
    {
        s_blockHeaderStyles ??= new Dictionary<DialogDslBlockType, GUIStyle>();
        if (s_blockHeaderStyles.TryGetValue(type, out var style))
        {
            return style;
        }

        var baseStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(6, 6, 4, 4)
        };
        baseStyle.normal.background = CreateColorTexture(GetBlockHeaderColor(type));
        s_blockHeaderStyles[type] = baseStyle;
        return baseStyle;
    }

    private static Color GetBlockHeaderColor(DialogDslBlockType type)
    {
        return type switch
        {
            DialogDslBlockType.Line => new Color(0.22f, 0.45f, 0.75f, 0.35f),
            DialogDslBlockType.ConditionGroup => new Color(0.75f, 0.55f, 0.15f, 0.35f),
            DialogDslBlockType.Exit => new Color(0.6f, 0.3f, 0.7f, 0.35f),
            DialogDslBlockType.Raw => new Color(0.35f, 0.35f, 0.35f, 0.35f),
            _ => new Color(0.25f, 0.25f, 0.25f, 0.3f)
        };
    }

    private static Texture2D CreateColorTexture(Color color)
    {
        var tex = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength - 1) + "…";
    }

    private bool IsLocalizationActive =>
        _draft != null &&
        _draft.LocalizationSettings != null &&
        _draft.LocalizationSettings.EnableLocalization &&
        !string.IsNullOrWhiteSpace(_activeLocale);

    private string GetLocaleCodeByIndex(DialogLocalizationSettings settings, int index)
    {
        if (settings == null || settings.Locales == null)
        {
            return null;
        }

        var filtered = new List<DialogLocaleInfo>();
        foreach (var locale in settings.Locales)
        {
            if (locale != null && !string.IsNullOrWhiteSpace(locale.Code))
            {
                filtered.Add(locale);
            }
        }

        if (index < 0 || index >= filtered.Count)
        {
            return null;
        }

        return filtered[index].Code;
    }

    private int GetLocaleIndexByCode(DialogLocalizationSettings settings, string code)
    {
        if (settings == null || settings.Locales == null || string.IsNullOrWhiteSpace(code))
        {
            return -1;
        }

        var index = 0;
        foreach (var locale in settings.Locales)
        {
            if (locale == null || string.IsNullOrWhiteSpace(locale.Code))
            {
                continue;
            }

            if (string.Equals(locale.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private void SetLocaleByIndex(int index, DialogLocalizationSettings settings)
    {
        _localePopupIndex = Mathf.Max(0, index);
        if (index <= 0)
        {
            _activeLocale = null;
            return;
        }

        var localeCode = GetLocaleCodeByIndex(settings, index - 1);
        if (string.IsNullOrWhiteSpace(localeCode))
        {
            _activeLocale = null;
            _localePopupIndex = 0;
            return;
        }

        _activeLocale = localeCode;
        EnsureLocalizationVariant(localeCode, settings.CloneBaseOnAdd);
    }

    private void EnsureLocalizationVariant(string locale, bool cloneBase)
    {
        if (_draft == null || string.IsNullOrWhiteSpace(locale))
        {
            return;
        }

        var variant = GetOrCreateVariant(locale);
        SyncLocalizationVariant(variant, cloneBase);
    }

    private DialogLocalizationVariant GetLocalizationVariant(string locale)
    {
        if (_draft == null || string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        foreach (var variant in _draft.Localizations)
        {
            if (variant != null && string.Equals(variant.Locale, locale, StringComparison.OrdinalIgnoreCase))
            {
                return variant;
            }
        }

        return null;
    }

    private DialogLocalizationVariant GetOrCreateVariant(string locale)
    {
        var existing = GetLocalizationVariant(locale);
        if (existing != null)
        {
            return existing;
        }

        var variant = new DialogLocalizationVariant
        {
            Locale = locale
        };
        _draft.Localizations.Add(variant);
        EditorUtility.SetDirty(_draft);
        return variant;
    }

    private void SyncLocalizationVariant(DialogLocalizationVariant variant, bool cloneBase)
    {
        if (variant == null || _document == null)
        {
            return;
        }

        var blocks = new List<DialogDslBlock>();
        CollectBlocks(_document.Blocks, blocks);
        var changed = false;
        foreach (var block in blocks)
        {
            if (block.Type == DialogDslBlockType.Line)
            {
                var entry = GetOrCreateLineEntry(variant, GetLineLocalizationKey(block));
                if (cloneBase)
                {
                    if (string.IsNullOrWhiteSpace(entry.Speaker))
                    {
                        entry.Speaker = block.Speaker;
                        changed = true;
                    }

                    if (string.IsNullOrWhiteSpace(entry.Text))
                    {
                        entry.Text = block.Text;
                        changed = true;
                    }
                }
            }

            if (block.Type == DialogDslBlockType.ChoiceGroup && block.Choices != null)
            {
                foreach (var choice in block.Choices)
                {
                    if (choice == null)
                    {
                        continue;
                    }

                    var entry = GetOrCreateChoiceEntry(variant, GetChoiceLocalizationKey(choice));
                    if (cloneBase && string.IsNullOrWhiteSpace(entry.Text))
                    {
                        entry.Text = choice.Text;
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            MarkDraftDirty("Sync Localization");
        }
    }

    private void CollectBlocks(List<DialogDslBlock> source, List<DialogDslBlock> output)
    {
        if (source == null)
        {
            return;
        }

        foreach (var block in source)
        {
            if (block == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(block.Id))
            {
                block.Id = Guid.NewGuid().ToString("N");
            }

            output.Add(block);
            if (block.Children != null && block.Children.Count > 0)
            {
                CollectBlocks(block.Children, output);
            }
        }
    }

    private DialogLocalizedLine GetOrCreateLineLocalization(DialogDslBlock block)
    {
        if (!IsLocalizationActive || block == null)
        {
            return null;
        }

        var variant = GetOrCreateVariant(_activeLocale);
        var key = GetLineLocalizationKey(block);
        return GetOrCreateLineEntry(variant, key);
    }

    private DialogLocalizedChoice GetOrCreateChoiceLocalization(DialogDslChoice choice)
    {
        if (!IsLocalizationActive || choice == null)
        {
            return null;
        }

        var variant = GetOrCreateVariant(_activeLocale);
        var key = GetChoiceLocalizationKey(choice);
        return GetOrCreateChoiceEntry(variant, key);
    }

    private DialogLocalizedLine GetOrCreateLineEntry(DialogLocalizationVariant variant, string blockId)
    {
        var existing = FindLineLocalization(variant, blockId);
        if (existing != null)
        {
            return existing;
        }

        var entry = new DialogLocalizedLine
        {
            BlockId = blockId
        };
        variant.Lines.Add(entry);
        EditorUtility.SetDirty(_draft);
        return entry;
    }

    private DialogLocalizedChoice GetOrCreateChoiceEntry(DialogLocalizationVariant variant, string choiceId)
    {
        var existing = FindChoiceLocalization(variant, choiceId);
        if (existing != null)
        {
            return existing;
        }

        var entry = new DialogLocalizedChoice
        {
            ChoiceId = choiceId
        };
        variant.Choices.Add(entry);
        EditorUtility.SetDirty(_draft);
        return entry;
    }

    private DialogLocalizedLine FindLineLocalization(DialogLocalizationVariant variant, string blockId)
    {
        if (variant == null)
        {
            return null;
        }

        foreach (var entry in variant.Lines)
        {
            if (entry != null && string.Equals(entry.BlockId, blockId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private DialogLocalizedChoice FindChoiceLocalization(DialogLocalizationVariant variant, string choiceId)
    {
        if (variant == null)
        {
            return null;
        }

        foreach (var entry in variant.Choices)
        {
            if (entry != null && string.Equals(entry.ChoiceId, choiceId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private string GetLineLocalizationKey(DialogDslBlock block)
    {
        if (block == null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(block.TextKey) ? block.Id : block.TextKey;
    }

    private string GetChoiceLocalizationKey(DialogDslChoice choice)
    {
        if (choice == null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(choice.TextKey) ? choice.Id : choice.TextKey;
    }

    private void DrawLocalizationKeyField(DialogDslBlock block)
    {
        var settings = _draft?.LocalizationSettings;
        if (settings == null || !settings.EnableLocalization || settings.KeyMode == DialogLocalizationKeyMode.None)
        {
            return;
        }

        using (new EditorGUI.DisabledScope(settings.KeyMode == DialogLocalizationKeyMode.Generate))
        {
            block.TextKey = EditorGUILayout.TextField("Loc Key", block.TextKey);
        }
    }

    private void DrawLocalizationKeyField(DialogDslChoice choice)
    {
        var settings = _draft?.LocalizationSettings;
        if (settings == null || !settings.EnableLocalization || settings.KeyMode == DialogLocalizationKeyMode.None)
        {
            return;
        }

        using (new EditorGUI.DisabledScope(settings.KeyMode == DialogLocalizationKeyMode.Generate))
        {
            choice.TextKey = EditorGUILayout.TextField("Loc Key", choice.TextKey);
        }
    }

    private DialogDslBlock CloneBlock(DialogDslBlock source)
    {
        if (source == null)
        {
            return null;
        }

        var clone = new DialogDslBlock
        {
            Id = source.Id,
            Type = source.Type,
            IsCollapsed = source.IsCollapsed,
            Speaker = source.Speaker,
            Text = source.Text,
            TextKey = source.TextKey,
            Condition = source.Condition,
            Outcome = source.Outcome,
            StableId = source.StableId,
            Raw = source.Raw,
            Tags = source.Tags == null ? new List<string>() : new List<string>(source.Tags),
            Choices = new List<DialogDslChoice>(),
            Children = new List<DialogDslBlock>()
        };

        if (source.Choices != null)
        {
            foreach (var choice in source.Choices)
            {
                if (choice == null)
                {
                    continue;
                }

                clone.Choices.Add(new DialogDslChoice
                {
                    Id = choice.Id,
                    Text = choice.Text,
                    TextKey = choice.TextKey,
                    Condition = choice.Condition,
                    Outcome = choice.Outcome,
                    StableId = choice.StableId,
                    Tags = choice.Tags == null ? new List<string>() : new List<string>(choice.Tags)
                });
            }
        }

        if (source.Children != null)
        {
            foreach (var child in source.Children)
            {
                clone.Children.Add(CloneBlock(child));
            }
        }

        return clone;
    }

    private bool ShouldHighlight(DropTarget target)
    {
        var payload = DragAndDrop.GetGenericData(DragDataKey) as DragPayload;
        if (payload == null)
        {
            return false;
        }

        if (!target.Rect.Contains(Event.current.mousePosition))
        {
            return false;
        }

        return CanAcceptDrop(payload, target);
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

        var basePath = GetExportPath(null);
        if (string.IsNullOrWhiteSpace(basePath) || !System.IO.File.Exists(basePath))
        {
            _document = DialogDslEditorParser.FromDraft(_draft);
            return;
        }

        var text = System.IO.File.ReadAllText(basePath);
        _document = DialogDslEditorParser.Parse(text);
        Undo.RecordObject(_draft, "Import Dialog DSL");
        DialogDslEditorParser.ApplyToDraft(_draft, _document);
        EditorUtility.SetDirty(_draft);
    }

    private void SetDraft(DialogDraftAsset asset)
    {
        _draft = asset;
        _document = asset != null ? DialogDslEditorParser.FromDraft(asset) : null;
        _activeLocale = null;
        _localePopupIndex = 0;
    }

    private void SaveToDsl()
    {
        if (_draft == null || _document == null)
        {
            return;
        }

        EnsureLocalizationKeys(_document);
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

        ExportAllLanguages();
    }

    private void PickDslPath()
    {
        if (_draft == null)
        {
            return;
        }

        var startFolder = ResolveFolderAbsolute(_draft.DslPath);
        var chosen = EditorUtility.OpenFolderPanel("Dialog DSL Folder", startFolder, string.Empty);
        if (string.IsNullOrWhiteSpace(chosen))
        {
            return;
        }

        var relative = FileUtil.GetProjectRelativePath(chosen);
        if (string.IsNullOrWhiteSpace(relative))
        {
            EditorUtility.DisplayDialog("Invalid Folder",
                "Please choose a folder inside the Unity project (e.g., under Assets).", "OK");
            return;
        }

        Undo.RecordObject(_draft, "Set DSL Path");
        _draft.DslPath = relative;
        EditorUtility.SetDirty(_draft);
    }

    private void MarkDraftDirty(string action)
    {
        if (_draft == null)
        {
            return;
        }

        Undo.RecordObject(_draft, action);
        EditorUtility.SetDirty(_draft);
    }

    private void ExportAllLanguages()
    {
        var settings = _draft.LocalizationSettings;
        if (settings == null || !settings.EnableLocalization)
        {
            ExportSingle(null);
            return;
        }

        ExportSingle(null);
        foreach (var locale in GetLocaleCodes(settings))
        {
            ExportSingle(locale);
        }
    }

    private void ExportSingle(string locale)
    {
        var exportDocument = BuildExportDocument(locale);
        var exportPath = GetExportPath(locale);
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            return;
        }

        EnsureExportFolder();
        DialogDslEditorParser.Save(exportDocument, exportPath);
        AssetDatabase.ImportAsset(exportPath);
    }

    private DialogDslDocument BuildExportDocument(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return CloneDocument(_document);
        }

        return BuildLocalizedDocument(locale);
    }

    private string GetExportPath(string locale)
    {
        if (_draft == null)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(locale))
        {
            return BuildExportPathWithSuffix(null);
        }

        var settings = _draft.LocalizationSettings;
        var suffix = settings != null && !string.IsNullOrWhiteSpace(settings.LocalizedDslSuffix)
            ? settings.LocalizedDslSuffix
            : ".{locale}";
        if (!suffix.Contains("{locale}", StringComparison.OrdinalIgnoreCase))
        {
            suffix += ".{locale}";
        }
        suffix = suffix.Replace("{locale}", locale);

        return BuildExportPathWithSuffix(suffix);
    }

    private string BuildExportPathWithSuffix(string suffix)
    {
        if (_draft == null)
        {
            return string.Empty;
        }

        var folder = _draft.DslPath;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return string.Empty;
        }

        var dialogId = _document != null && !string.IsNullOrWhiteSpace(_document.DialogId)
            ? _document.DialogId.Trim()
            : "main";
        var fileName = SanitizeFileName(dialogId);
        var extension = ".dlg";
        var finalName = string.IsNullOrWhiteSpace(suffix) ? fileName : $"{fileName}{suffix}";
        var relative = System.IO.Path.Combine(folder, $"{finalName}{extension}");
        return ToProjectRelativePath(relative);
    }

    private void EnsureExportFolder()
    {
        if (_draft == null || string.IsNullOrWhiteSpace(_draft.DslPath))
        {
            return;
        }

        var fullPath = System.IO.Path.GetFullPath(_draft.DslPath);
        if (!System.IO.Directory.Exists(fullPath))
        {
            System.IO.Directory.CreateDirectory(fullPath);
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "dialog";
        }

        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var ch in name)
        {
            builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }

        return builder.ToString();
    }

    private static string ResolveFolderAbsolute(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return System.IO.Path.GetFullPath("Assets");
        }

        return System.IO.Path.IsPathRooted(folder) ? folder : System.IO.Path.GetFullPath(folder);
    }

    private static string ToProjectRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var full = System.IO.Path.GetFullPath(path);
        var relative = FileUtil.GetProjectRelativePath(full);
        return string.IsNullOrWhiteSpace(relative) ? path : relative;
    }

    private List<string> GetLocaleCodes(DialogLocalizationSettings settings)
    {
        var result = new List<string>();
        if (settings == null || settings.Locales == null)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var locale in settings.Locales)
        {
            if (locale == null || string.IsNullOrWhiteSpace(locale.Code))
            {
                continue;
            }

            var code = locale.Code.Trim();
            if (code.Length == 0 || !seen.Add(code))
            {
                continue;
            }

            result.Add(code);
        }

        return result;
    }

    private void EnsureLocalizationKeys(DialogDslDocument document)
    {
        if (document == null || _draft == null)
        {
            return;
        }

        var settings = _draft.LocalizationSettings;
        if (settings == null || settings.KeyMode != DialogLocalizationKeyMode.Generate)
        {
            return;
        }

        EnsureKeysInBlocks(document.Blocks, document.DialogId, settings);
    }

    private void EnsureKeysInBlocks(List<DialogDslBlock> blocks, string dialogId, DialogLocalizationSettings settings)
    {
        if (blocks == null)
        {
            return;
        }

        foreach (var block in blocks)
        {
            if (block == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(block.Id))
            {
                block.Id = Guid.NewGuid().ToString("N");
            }

            if (block.Type == DialogDslBlockType.Line && string.IsNullOrWhiteSpace(block.TextKey))
            {
                block.TextKey = GenerateKey(settings, dialogId, block.Id, null, "line");
            }

            if (block.Type == DialogDslBlockType.ChoiceGroup && block.Choices != null)
            {
                foreach (var choice in block.Choices)
                {
                    if (choice == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(choice.Id))
                    {
                        choice.Id = Guid.NewGuid().ToString("N");
                    }

                    if (string.IsNullOrWhiteSpace(choice.TextKey))
                    {
                        choice.TextKey = GenerateKey(settings, dialogId, block.Id, choice.Id, "choice");
                    }
                }
            }

            if (block.Children != null && block.Children.Count > 0)
            {
                EnsureKeysInBlocks(block.Children, dialogId, settings);
            }
        }
    }

    private static string GenerateKey(DialogLocalizationSettings settings, string dialogId, string blockId,
        string choiceId, string type)
    {
        var format = settings != null && !string.IsNullOrWhiteSpace(settings.KeyFormat)
            ? settings.KeyFormat
            : "{dialogId}.{blockId}";

        return format
            .Replace("{dialogId}", dialogId ?? string.Empty)
            .Replace("{blockId}", blockId ?? string.Empty)
            .Replace("{choiceId}", choiceId ?? string.Empty)
            .Replace("{type}", type ?? string.Empty);
    }

    private DialogDslDocument BuildLocalizedDocument(string locale)
    {
        var localized = new DialogDslDocument
        {
            DialogId = _document.DialogId,
            Blocks = CloneBlocksWithLocalization(_document.Blocks, locale)
        };
        return localized;
    }

    private List<DialogDslBlock> CloneBlocksWithLocalization(List<DialogDslBlock> source, string locale)
    {
        var result = new List<DialogDslBlock>();
        var variant = GetLocalizationVariant(locale);

        foreach (var block in source)
        {
            if (block == null)
            {
                continue;
            }

            var clone = CloneBlock(block);
            if (block.Type == DialogDslBlockType.Line && variant != null)
            {
                var entry = FindLineLocalization(variant, GetLineLocalizationKey(block));
                if (entry != null)
                {
                    clone.Speaker = string.IsNullOrWhiteSpace(entry.Speaker) ? block.Speaker : entry.Speaker;
                    clone.Text = string.IsNullOrWhiteSpace(entry.Text) ? block.Text : entry.Text;
                }
            }

            if (block.Type == DialogDslBlockType.ChoiceGroup && variant != null && clone.Choices != null)
            {
                foreach (var choice in clone.Choices)
                {
                    if (choice == null)
                    {
                        continue;
                    }

                    var entry = FindChoiceLocalization(variant, GetChoiceLocalizationKey(choice));
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Text))
                    {
                        choice.Text = entry.Text;
                    }
                }
            }

            if (block.Children != null && block.Children.Count > 0)
            {
                clone.Children = CloneBlocksWithLocalization(block.Children, locale);
            }

            result.Add(clone);
        }

        return result;
    }

    private DialogDslDocument CloneDocument(DialogDslDocument document)
    {
        return new DialogDslDocument
        {
            DialogId = document.DialogId,
            Blocks = CloneBlocksWithLocalization(document.Blocks, null)
        };
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
