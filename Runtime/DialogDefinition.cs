using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime
{
[Serializable]
public sealed class DialogDefinition
{
    [SerializeField] private string _id;
    [SerializeField] private int _entryIndex;
    [SerializeField] private List<DialogInstruction> _instructions = new();
    [SerializeField] private List<DialogLabel> _labels = new();

    [NonSerialized] private Dictionary<string, int> _labelToIndex;

    public string Id => _id;
    public int EntryIndex => _entryIndex;
    public IReadOnlyList<DialogInstruction> Instructions => _instructions;
    public IReadOnlyList<DialogLabel> Labels => _labels;

    public void SetData(string id, int entryIndex, List<DialogInstruction> instructions, List<DialogLabel> labels)
    {
        _id = id;
        _entryIndex = entryIndex;
        _instructions = instructions ?? new List<DialogInstruction>();
        _labels = labels ?? new List<DialogLabel>();
        BuildCaches();
    }

    public void BuildCaches()
    {
        _labelToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            if (label == null || string.IsNullOrWhiteSpace(label.Name))
            {
                continue;
            }

            _labelToIndex[label.Name.Trim()] = label.InstructionIndex;
        }
    }

    public bool TryGetLabelIndex(string label, out int instructionIndex)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            instructionIndex = -1;
            return false;
        }

        if (_labelToIndex == null)
        {
            BuildCaches();
        }

        return _labelToIndex.TryGetValue(label.Trim(), out instructionIndex);
    }
}
}
