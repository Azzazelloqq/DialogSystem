using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime;
using DialogSystem.Runtime.Flow;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.Editor.FlowGraph
{
public sealed class DialogFlowNodeView : Node
{
    private readonly DialogFlowGraphView _owner;
    private readonly List<OutcomePortEntry> _outcomePorts = new();
    private readonly Dictionary<string, Port> _ports = new();
    private Port _input;

    public DialogFlowNodeData Data { get; }
    public Port InputPort => _input;
    public Vector2 DefaultSize => new(260f, 160f);

    public DialogFlowNodeView(DialogFlowNodeData data, DialogFlowGraphView owner)
    {
        Data = data;
        _owner = owner;
        Build();
    }

    public Port GetOutputPort(DialogFlowPortKind kind, int outcomeIndex = -1)
    {
        if (kind == DialogFlowPortKind.Outcome)
        {
            return outcomeIndex >= 0 && outcomeIndex < _outcomePorts.Count ? _outcomePorts[outcomeIndex].Port : null;
        }

        if (kind == DialogFlowPortKind.Choice)
        {
            return outcomeIndex >= 0 && outcomeIndex < _outcomePorts.Count ? _outcomePorts[outcomeIndex].Port : null;
        }

        var key = $"{kind}:{outcomeIndex}";
        return _ports.TryGetValue(key, out var port) ? port : null;
    }

    private void Build()
    {
        title = string.IsNullOrWhiteSpace(Data.Id) ? Data.Type.ToString() : Data.Type.ToString();
        CreatePorts();
        BuildFields();
        RefreshExpandedState();
        RefreshPorts();
    }

    private void CreatePorts()
    {
        if (Data.Type != DialogFlowNodeType.Start)
        {
            _input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            _input.portName = string.Empty;
            inputContainer.Add(_input);
        }

        switch (Data.Type)
        {
            case DialogFlowNodeType.Start:
                AddOutputPort("Entry", DialogFlowPortKind.Entry);
                break;
            case DialogFlowNodeType.Dialog:
                BuildOutcomePorts();
                break;
            case DialogFlowNodeType.Choice:
                BuildChoicePorts();
                break;
            case DialogFlowNodeType.Action:
                AddOutputPort("Next", DialogFlowPortKind.Next);
                break;
        }
    }

    private void BuildFields()
    {
        switch (Data.Type)
        {
            case DialogFlowNodeType.Dialog:
                AddDialogFields();
                break;
            case DialogFlowNodeType.Choice:
                AddChoiceFields();
                break;
            case DialogFlowNodeType.Action:
                AddTextField("Action Id", Data.ActionId, value =>
                {
                    _owner.RecordUndo("Edit Action Id");
                    Data.ActionId = value;
                });
                AddTextField("Payload", Data.Payload, value =>
                {
                    _owner.RecordUndo("Edit Payload");
                    Data.Payload = value;
                });
                break;
        }
    }

    private void AddDialogFields()
    {
        var assetField = new ObjectField("Dialog Asset")
        {
            objectType = typeof(DialogAsset),
            value = Data.DialogAsset
        };
        assetField.RegisterValueChangedCallback(evt =>
        {
            _owner.RecordUndo("Edit Dialog Asset");
            Data.DialogAsset = evt.newValue as DialogAsset;
            DialogFlowOutcomeUtility.SyncOutcomes(Data);
            RefreshDialogContent();
        });
        extensionContainer.Add(assetField);

        var defaultDialogId = Data.DialogAsset != null ? Data.DialogAsset.GetDefaultDialog()?.Id : null;
        if (!string.IsNullOrWhiteSpace(defaultDialogId))
        {
            extensionContainer.Add(new Label($"Dialog: {defaultDialogId}"));
        }

        var syncButton = new Button(() =>
        {
            _owner.RecordUndo("Sync Outcomes");
            DialogFlowOutcomeUtility.SyncOutcomes(Data);
            RefreshDialogContent();
        })
        {
            text = "Sync Outcomes"
        };
        extensionContainer.Add(syncButton);

        DrawOutcomeSummary();
    }

    private void RefreshDialogContent()
    {
        _outcomePorts.Clear();
        _ports.Clear();
        inputContainer.Clear();
        outputContainer.Clear();
        extensionContainer.Clear();
        CreatePorts();
        BuildFields();
        RefreshPorts();
        RefreshExpandedState();
    }

    private void DrawOutcomeSummary()
    {
        if (Data.Outcomes == null || Data.Outcomes.Count == 0)
        {
            extensionContainer.Add(new Label("Outcomes: (none)"));
            return;
        }

        extensionContainer.Add(new Label("Outcomes:"));
        for (int i = 0; i < Data.Outcomes.Count; i++)
        {
            var outcome = Data.Outcomes[i];
            var name = outcome != null && !string.IsNullOrWhiteSpace(outcome.Outcome) ? outcome.Outcome : "(unnamed)";
            extensionContainer.Add(new Label($"- {name}"));
        }
    }

    private void BuildOutcomePorts()
    {
        if (Data.Outcomes == null)
        {
            Data.Outcomes = new List<DialogFlowOutcomeData>();
        }

        for (int i = 0; i < Data.Outcomes.Count; i++)
        {
            var outcome = Data.Outcomes[i];
            var port = AddOutputPort(GetOutcomePortName(outcome, i), DialogFlowPortKind.Outcome, i);
            _outcomePorts.Add(new OutcomePortEntry(outcome, port));
        }
    }

    private void AddChoiceFields()
    {
        var addButton = new Button(AddChoice)
        {
            text = "Add Choice"
        };
        extensionContainer.Add(addButton);

        if (Data.Choices == null)
        {
            Data.Choices = new List<DialogFlowChoiceData>();
        }

        for (int i = 0; i < Data.Choices.Count; i++)
        {
            var choice = Data.Choices[i];
            var foldout = AddChoiceRow(choice, i);
            var entry = _outcomePorts.FirstOrDefault(item => item.Port.userData is DialogFlowPortData data &&
                                                             data.Kind == DialogFlowPortKind.Choice &&
                                                             data.Index == i);
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
            Data.Choices = new List<DialogFlowChoiceData>();
        }

        for (int i = 0; i < Data.Choices.Count; i++)
        {
            var choice = Data.Choices[i];
            var port = AddOutputPort(GetChoicePortName(choice, i), DialogFlowPortKind.Choice, i);
            _outcomePorts.Add(new OutcomePortEntry(null, port));
        }
    }

    private void AddChoice()
    {
        _owner.RecordUndo("Add Choice");
        var choice = new DialogFlowChoiceData
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = "Choice"
        };
        Data.Choices.Add(choice);
        var port = AddOutputPort(GetChoicePortName(choice, Data.Choices.Count - 1), DialogFlowPortKind.Choice,
            Data.Choices.Count - 1);
        var entry = new OutcomePortEntry(null, port);
        _outcomePorts.Add(entry);
        entry.Foldout = AddChoiceRow(choice, Data.Choices.Count - 1);
        RefreshExpandedState();
        RefreshPorts();
    }

    private Foldout AddChoiceRow(DialogFlowChoiceData choice, int index)
    {
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

        var removeButton = new Button(() => RemoveChoice(choice))
        {
            text = "Remove Choice"
        };
        foldout.Add(removeButton);

        extensionContainer.Add(foldout);
        return foldout;
    }

    private void RemoveChoice(DialogFlowChoiceData choice)
    {
        var index = Data.Choices.IndexOf(choice);
        if (index < 0)
        {
            return;
        }

        _owner.RecordUndo("Remove Choice");
        var entry = _outcomePorts.FirstOrDefault(item => item.Port.userData is DialogFlowPortData data &&
                                                         data.Kind == DialogFlowPortKind.Choice &&
                                                         data.Index == index);
        if (entry != null)
        {
            _owner.RemoveConnections(entry.Port);
            outputContainer.Remove(entry.Port);
            if (entry.Foldout != null)
            {
                extensionContainer.Remove(entry.Foldout);
            }

            _outcomePorts.Remove(entry);
        }

        Data.Choices.RemoveAt(index);
        RefreshChoiceIndices();
    }

    private void RefreshChoiceIndices()
    {
        var choiceIndex = 0;
        for (int i = 0; i < _outcomePorts.Count; i++)
        {
            var entry = _outcomePorts[i];
            if (entry.Port.userData is not DialogFlowPortData data || data.Kind != DialogFlowPortKind.Choice)
            {
                continue;
            }

            entry.Port.userData = new DialogFlowPortData(DialogFlowPortKind.Choice, choiceIndex);
            entry.Port.portName = GetChoicePortName(Data.Choices[choiceIndex], choiceIndex);
            if (entry.Foldout != null)
            {
                entry.Foldout.text = $"Choice {choiceIndex + 1}";
            }
            choiceIndex++;
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    private void UpdateChoicePortLabel(DialogFlowChoiceData choice)
    {
        var index = Data.Choices.IndexOf(choice);
        if (index < 0)
        {
            return;
        }

        var entry = _outcomePorts.FirstOrDefault(item => item.Port.userData is DialogFlowPortData data &&
                                                         data.Kind == DialogFlowPortKind.Choice &&
                                                         data.Index == index);
        if (entry != null)
        {
            entry.Port.portName = GetChoicePortName(choice, index);
        }
    }


    private Port AddOutputPort(string name, DialogFlowPortKind kind, int outcomeIndex = -1)
    {
        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        port.portName = name;
        port.userData = new DialogFlowPortData(kind, outcomeIndex);
        outputContainer.Add(port);
        if (kind != DialogFlowPortKind.Outcome && kind != DialogFlowPortKind.Choice)
        {
            _ports[$"{kind}:{outcomeIndex}"] = port;
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

    private static string GetOutcomePortName(DialogFlowOutcomeData outcome, int index)
    {
        if (outcome == null || string.IsNullOrWhiteSpace(outcome.Outcome))
        {
            return $"Outcome {index + 1}";
        }

        var text = outcome.Outcome.Trim();
        if (text.Length > 18)
        {
            text = $"{text.Substring(0, 18)}...";
        }

        return $"{index + 1}. {text}";
    }

    private static string GetChoicePortName(DialogFlowChoiceData choice, int index)
    {
        if (choice == null || string.IsNullOrWhiteSpace(choice.Text))
        {
            return $"Choice {index + 1}";
        }

        return choice.Text.Trim();
    }

    private sealed class OutcomePortEntry
    {
        public DialogFlowOutcomeData Outcome { get; }
        public Port Port { get; }
        public Foldout Foldout { get; set; }

        public OutcomePortEntry(DialogFlowOutcomeData outcome, Port port)
        {
            Outcome = outcome;
            Port = port;
        }
    }
}
}
