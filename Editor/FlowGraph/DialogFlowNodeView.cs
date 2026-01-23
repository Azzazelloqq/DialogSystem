using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Flow;
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
                AddTextField("Dialog Id", Data.DialogId, value =>
                {
                    _owner.RecordUndo("Edit Dialog Id");
                    Data.DialogId = value;
                });
                AddOutcomeFields();
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

    private void AddOutcomeFields()
    {
        var addButton = new Button(AddOutcome)
        {
            text = "Add Outcome"
        };
        extensionContainer.Add(addButton);

        if (Data.Outcomes == null)
        {
            Data.Outcomes = new List<DialogFlowOutcomeData>();
        }

        for (int i = 0; i < Data.Outcomes.Count; i++)
        {
            var outcome = Data.Outcomes[i];
            var foldout = AddOutcomeRow(outcome, i);
            var entry = _outcomePorts.FirstOrDefault(item => item.Outcome == outcome);
            if (entry != null)
            {
                entry.Foldout = foldout;
            }
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

    private void AddOutcome()
    {
        _owner.RecordUndo("Add Outcome");
        var outcome = new DialogFlowOutcomeData
        {
            Outcome = "Outcome"
        };
        Data.Outcomes.Add(outcome);
        var port = AddOutputPort(GetOutcomePortName(outcome, Data.Outcomes.Count - 1), DialogFlowPortKind.Outcome,
            Data.Outcomes.Count - 1);
        var entry = new OutcomePortEntry(outcome, port);
        _outcomePorts.Add(entry);
        entry.Foldout = AddOutcomeRow(outcome, Data.Outcomes.Count - 1);
        RefreshExpandedState();
        RefreshPorts();
    }

    private Foldout AddOutcomeRow(DialogFlowOutcomeData outcome, int index)
    {
        var foldout = new Foldout
        {
            text = $"Outcome {index + 1}",
            value = false
        };

        var nameField = new TextField("Name");
        nameField.SetValueWithoutNotify(outcome.Outcome);
        nameField.RegisterValueChangedCallback(evt =>
        {
            _owner.RecordUndo("Edit Outcome Name");
            outcome.Outcome = evt.newValue;
            UpdateOutcomePortLabel(outcome);
        });
        foldout.Add(nameField);

        var removeButton = new Button(() => RemoveOutcome(outcome))
        {
            text = "Remove Outcome"
        };
        foldout.Add(removeButton);

        extensionContainer.Add(foldout);
        return foldout;
    }

    private void RemoveOutcome(DialogFlowOutcomeData outcome)
    {
        var index = Data.Outcomes.IndexOf(outcome);
        if (index < 0)
        {
            return;
        }

        _owner.RecordUndo("Remove Outcome");
        var entry = _outcomePorts.FirstOrDefault(item => item.Outcome == outcome);
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

        Data.Outcomes.RemoveAt(index);
        RefreshOutcomeIndices();
    }

    private void RefreshOutcomeIndices()
    {
        for (int i = 0; i < _outcomePorts.Count; i++)
        {
            var entry = _outcomePorts[i];
            entry.Port.userData = new DialogFlowPortData(DialogFlowPortKind.Outcome, i);
            entry.Port.portName = GetOutcomePortName(entry.Outcome, i);
            if (entry.Foldout != null)
            {
                entry.Foldout.text = $"Outcome {i + 1}";
            }
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    private void UpdateOutcomePortLabel(DialogFlowOutcomeData outcome)
    {
        var index = Data.Outcomes.IndexOf(outcome);
        if (index < 0 || index >= _outcomePorts.Count)
        {
            return;
        }

        _outcomePorts[index].Port.portName = GetOutcomePortName(outcome, index);
    }

    private Port AddOutputPort(string name, DialogFlowPortKind kind, int outcomeIndex = -1)
    {
        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        port.portName = name;
        port.userData = new DialogFlowPortData(kind, outcomeIndex);
        outputContainer.Add(port);
        if (kind != DialogFlowPortKind.Outcome)
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
