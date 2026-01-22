using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.Editor
{
public sealed class DialogGraphNodeView : Node
{
    private readonly DialogGraphEditorView _owner;
    private readonly Dictionary<string, Port> _ports = new();
    private readonly List<ChoicePortEntry> _choicePorts = new();
    private Port _input;

    public DialogGraphNodeData Data { get; }
    public Port InputPort => _input;
    public Vector2 DefaultSize => new(260f, 180f);

    public DialogGraphNodeView(DialogGraphNodeData data, DialogGraphEditorView owner)
    {
        Data = data;
        _owner = owner;
        Build();
    }

    public Port GetOutputPort(DialogPortKind kind, int choiceIndex = -1)
    {
        if (kind == DialogPortKind.Choice)
        {
            return choiceIndex >= 0 && choiceIndex < _choicePorts.Count ? _choicePorts[choiceIndex].Port : null;
        }

        var key = GetKey(kind, choiceIndex);
        return _ports.TryGetValue(key, out var port) ? port : null;
    }

    private void Build()
    {
        UpdateTitle();
        CreatePorts();
        BuildFields();
        RefreshExpandedState();
        RefreshPorts();
    }

    private void UpdateTitle()
    {
        title = string.IsNullOrWhiteSpace(Data.Label) ? Data.Type.ToString() : $"{Data.Type} ({Data.Label})";
    }

    private void CreatePorts()
    {
        if (Data.Type != DialogGraphNodeType.Start)
        {
            _input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            _input.portName = string.Empty;
            inputContainer.Add(_input);
        }

        switch (Data.Type)
        {
            case DialogGraphNodeType.Start:
                AddOutputPort("Entry", DialogPortKind.Entry);
                break;
            case DialogGraphNodeType.Line:
            case DialogGraphNodeType.Set:
            case DialogGraphNodeType.Command:
                AddOutputPort("Next", DialogPortKind.Next);
                break;
            case DialogGraphNodeType.Jump:
                AddOutputPort("Jump", DialogPortKind.Jump);
                break;
            case DialogGraphNodeType.Call:
                AddOutputPort("Target", DialogPortKind.CallTarget);
                AddOutputPort("Next", DialogPortKind.Next);
                break;
            case DialogGraphNodeType.Condition:
                AddOutputPort("True", DialogPortKind.True);
                AddOutputPort("False", DialogPortKind.False);
                break;
            case DialogGraphNodeType.Choice:
                BuildChoicePorts();
                AddOutputPort("Default", DialogPortKind.Default);
                break;
        }
    }

    private void BuildFields()
    {
        if (Data.Type != DialogGraphNodeType.Start)
        {
            AddTextField("Label", Data.Label, value =>
            {
                _owner.RecordUndo("Change Node Label");
                Data.Label = value;
                UpdateTitle();
            });
        }

        switch (Data.Type)
        {
            case DialogGraphNodeType.Line:
                AddTextField("Speaker", Data.Speaker, value =>
                {
                    _owner.RecordUndo("Edit Speaker");
                    Data.Speaker = value;
                });
                AddTextArea("Text", Data.Text, value =>
                {
                    _owner.RecordUndo("Edit Line Text");
                    Data.Text = value;
                });
                AddTextField("Line Id", Data.StableId, value =>
                {
                    _owner.RecordUndo("Edit Line Id");
                    Data.StableId = value;
                });
                AddTextField("Tags (comma)", TagsToString(Data.Tags), value =>
                {
                    _owner.RecordUndo("Edit Line Tags");
                    Data.Tags = ParseTags(value);
                });
                break;
            case DialogGraphNodeType.Choice:
                BuildChoiceFields();
                break;
            case DialogGraphNodeType.Condition:
                AddTextField("Expression", Data.Expression, value =>
                {
                    _owner.RecordUndo("Edit Condition");
                    Data.Expression = value;
                });
                break;
            case DialogGraphNodeType.Set:
                AddTextField("Variable", Data.Variable, value =>
                {
                    _owner.RecordUndo("Edit Variable");
                    Data.Variable = value;
                });
                AddTextField("Expression", Data.Expression, value =>
                {
                    _owner.RecordUndo("Edit Expression");
                    Data.Expression = value;
                });
                break;
            case DialogGraphNodeType.Command:
                AddTextField("Expression", Data.Expression, value =>
                {
                    _owner.RecordUndo("Edit Command");
                    Data.Expression = value;
                });
                break;
            case DialogGraphNodeType.Jump:
            case DialogGraphNodeType.Call:
                AddTextField("Target (manual)", Data.TargetOverride, value =>
                {
                    _owner.RecordUndo("Edit Target");
                    Data.TargetOverride = value;
                });
                break;
        }
    }

    private void BuildChoiceFields()
    {
        var addButton = new Button(AddChoice)
        {
            text = "Add Choice"
        };
        extensionContainer.Add(addButton);

        if (Data.Choices == null)
        {
            Data.Choices = new List<DialogGraphChoiceData>();
        }

        for (int i = 0; i < Data.Choices.Count; i++)
        {
            var choice = Data.Choices[i];
            var foldout = AddChoiceRow(choice);
            var entry = _choicePorts.FirstOrDefault(item => item.Choice == choice);
            if (entry != null)
            {
                entry.Foldout = foldout;
            }
        }
    }

    private void BuildChoicePorts()
    {
        if (Data.Choices == null)
        {
            Data.Choices = new List<DialogGraphChoiceData>();
        }

        for (int i = 0; i < Data.Choices.Count; i++)
        {
            var choice = Data.Choices[i];
            if (choice == null)
            {
                continue;
            }

            var port = AddOutputPort(GetChoicePortName(choice, i), DialogPortKind.Choice, i);
            _choicePorts.Add(new ChoicePortEntry(choice, port));
        }
    }

    private void AddChoice()
    {
        _owner.RecordUndo("Add Choice");
        var choice = new DialogGraphChoiceData
        {
            Id = System.Guid.NewGuid().ToString("N"),
            Text = "Choice"
        };
        Data.Choices.Add(choice);

        var port = AddOutputPort(GetChoicePortName(choice, Data.Choices.Count - 1), DialogPortKind.Choice,
            Data.Choices.Count - 1);
        var entry = new ChoicePortEntry(choice, port);
        _choicePorts.Add(entry);
        entry.Foldout = AddChoiceRow(choice);
        RefreshExpandedState();
        RefreshPorts();
    }

    private Foldout AddChoiceRow(DialogGraphChoiceData choice)
    {
        var index = _choicePorts.FindIndex(entry => entry.Choice == choice);
        var foldout = new Foldout
        {
            text = $"Choice {index + 1}",
            value = false
        };

        var textField = new TextField("Text");
        textField.SetValueWithoutNotify(choice.Text);
        textField.RegisterValueChangedCallback(evt =>
        {
            _owner.RecordUndo("Edit Choice Text");
            choice.Text = evt.newValue;
            UpdateChoicePortLabel(choice);
        });
        foldout.Add(textField);

        var conditionField = new TextField("Condition");
        conditionField.SetValueWithoutNotify(choice.Condition);
        conditionField.RegisterValueChangedCallback(evt =>
        {
            _owner.RecordUndo("Edit Choice Condition");
            choice.Condition = evt.newValue;
        });
        foldout.Add(conditionField);

        var targetField = new TextField("Target (manual)");
        targetField.SetValueWithoutNotify(choice.TargetOverride);
        targetField.RegisterValueChangedCallback(evt =>
        {
            _owner.RecordUndo("Edit Choice Target");
            choice.TargetOverride = evt.newValue;
        });
        foldout.Add(targetField);

        var idField = new TextField("Choice Id");
        idField.SetValueWithoutNotify(choice.Id);
        idField.RegisterValueChangedCallback(evt =>
        {
            _owner.RecordUndo("Edit Choice Id");
            choice.Id = evt.newValue;
        });
        foldout.Add(idField);

        var tagsField = new TextField("Tags (comma)");
        tagsField.SetValueWithoutNotify(TagsToString(choice.Tags));
        tagsField.RegisterValueChangedCallback(evt =>
        {
            _owner.RecordUndo("Edit Choice Tags");
            choice.Tags = ParseTags(evt.newValue);
        });
        foldout.Add(tagsField);

        var removeButton = new Button(() => RemoveChoice(choice))
        {
            text = "Remove Choice"
        };
        foldout.Add(removeButton);

        extensionContainer.Add(foldout);
        return foldout;
    }

    private void RemoveChoice(DialogGraphChoiceData choice)
    {
        var index = Data.Choices.IndexOf(choice);
        if (index < 0)
        {
            return;
        }

        _owner.RecordUndo("Remove Choice");
        var entry = _choicePorts.FirstOrDefault(item => item.Choice == choice);
        if (entry != null)
        {
            _owner.RemoveConnections(entry.Port);
            outputContainer.Remove(entry.Port);
            if (entry.Foldout != null)
            {
                extensionContainer.Remove(entry.Foldout);
            }
            _choicePorts.Remove(entry);
        }

        Data.Choices.RemoveAt(index);
        RefreshChoiceIndices();
    }

    private void RefreshChoiceIndices()
    {
        for (int i = 0; i < _choicePorts.Count; i++)
        {
            var entry = _choicePorts[i];
            entry.Port.userData = new DialogPortData(DialogPortKind.Choice, i);
            entry.Port.portName = GetChoicePortName(entry.Choice, i);
            if (entry.Foldout != null)
            {
                entry.Foldout.text = $"Choice {i + 1}";
            }
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    private void UpdateChoicePortLabel(DialogGraphChoiceData choice)
    {
        var index = Data.Choices.IndexOf(choice);
        if (index < 0 || index >= _choicePorts.Count)
        {
            return;
        }

        _choicePorts[index].Port.portName = GetChoicePortName(choice, index);
    }

    private Port AddOutputPort(string name, DialogPortKind kind, int choiceIndex = -1)
    {
        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        port.portName = name;
        port.userData = new DialogPortData(kind, choiceIndex);
        outputContainer.Add(port);
        if (kind != DialogPortKind.Choice)
        {
            _ports[GetKey(kind, choiceIndex)] = port;
        }
        return port;
    }

    private void AddTextField(string label, string value, System.Action<string> onChange)
    {
        var field = new TextField(label)
        {
            value = value
        };
        field.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
        extensionContainer.Add(field);
    }

    private void AddTextArea(string label, string value, System.Action<string> onChange)
    {
        var field = new TextField(label)
        {
            value = value,
            multiline = true
        };
        field.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue));
        extensionContainer.Add(field);
    }

    private static string GetChoicePortName(DialogGraphChoiceData choice, int index)
    {
        if (choice == null || string.IsNullOrWhiteSpace(choice.Text))
        {
            return $"Choice {index + 1}";
        }

        var text = choice.Text.Trim();
        if (text.Length > 18)
        {
            text = $"{text.Substring(0, 18)}...";
        }

        return $"{index + 1}. {text}";
    }

    private static string GetKey(DialogPortKind kind, int index) => $"{kind}:{index}";

    private static List<string> ParseTags(string value)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return list;
        }

        var parts = value.Split(',');
        foreach (var part in parts)
        {
            var tag = part.Trim();
            if (!string.IsNullOrWhiteSpace(tag))
            {
                list.Add(tag);
            }
        }

        return list;
    }

    private static string TagsToString(List<string> tags)
    {
        return tags == null || tags.Count == 0 ? string.Empty : string.Join(", ", tags);
    }

    private sealed class ChoicePortEntry
    {
        public DialogGraphChoiceData Choice { get; }
        public Port Port { get; }
        public Foldout Foldout { get; set; }

        public ChoicePortEntry(DialogGraphChoiceData choice, Port port)
        {
            Choice = choice;
            Port = port;
        }
    }
}
}
