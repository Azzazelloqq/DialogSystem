using System;
using System.Collections.Generic;
using DialogSystem.Runtime;
using DialogSystem.Runtime.Flow;

namespace DialogSystem.Editor.FlowGraph
{
public static class DialogFlowOutcomeUtility
{
    public static List<string> CollectOutcomes(DialogAsset asset)
    {
        var outcomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dialog = asset != null ? asset.GetDefaultDialog() : null;
        if (dialog == null)
        {
            return new List<string>();
        }

        foreach (var instruction in dialog.Instructions)
        {
            if (instruction == null)
            {
                continue;
            }

            if (instruction.Type == DialogInstructionType.Outcome)
            {
                AddOutcome(outcomes, instruction.Outcome);
                continue;
            }

            if (instruction.Type == DialogInstructionType.ChoiceGroup && instruction.Choices != null)
            {
                foreach (var choice in instruction.Choices)
                {
                    if (choice == null || string.IsNullOrWhiteSpace(choice.Target))
                    {
                        continue;
                    }

                    if (TryGetOutcomeFromTarget(choice.Target, out var outcome))
                    {
                        AddOutcome(outcomes, outcome);
                    }
                }
            }
        }

        return new List<string>(outcomes);
    }

    public static void SyncOutcomes(DialogFlowNodeData node)
    {
        if (node == null || node.DialogAsset == null)
        {
            return;
        }

        var results = CollectOutcomes(node.DialogAsset);
        if (node.Outcomes == null)
        {
            node.Outcomes = new List<DialogFlowOutcomeData>();
        }

        var existing = new Dictionary<string, DialogFlowOutcomeData>(StringComparer.OrdinalIgnoreCase);
        foreach (var outcome in node.Outcomes)
        {
            if (outcome != null && !string.IsNullOrWhiteSpace(outcome.Outcome))
            {
                existing[outcome.Outcome] = outcome;
            }
        }

        node.Outcomes.Clear();
        foreach (var outcomeName in results)
        {
            if (existing.TryGetValue(outcomeName, out var stored))
            {
                node.Outcomes.Add(stored);
                continue;
            }

            node.Outcomes.Add(new DialogFlowOutcomeData
            {
                Outcome = outcomeName
            });
        }
    }

    private static void AddOutcome(HashSet<string> outcomes, string outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return;
        }

        outcomes.Add(outcome.Trim());
    }

    private static bool TryGetOutcomeFromTarget(string target, out string outcome)
    {
        outcome = null;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var trimmed = target.Trim();
        const string exitPrefix = "exit:";
        const string outcomePrefix = "outcome:";
        if (trimmed.StartsWith(exitPrefix, StringComparison.OrdinalIgnoreCase))
        {
            outcome = trimmed.Substring(exitPrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(outcome);
        }

        if (trimmed.StartsWith(outcomePrefix, StringComparison.OrdinalIgnoreCase))
        {
            outcome = trimmed.Substring(outcomePrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(outcome);
        }

        return false;
    }
}
}
