using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;

namespace Core;

internal static class ProfileStatePersistence
{
    private static readonly (string SectionName, Func<ClassConfiguration, KeyAction[]> Sequence)[] PersistedSections =
    [
        (nameof(ClassConfiguration.Pull), static c => c.Pull.Sequence),
        (nameof(ClassConfiguration.Flee), static c => c.Flee.Sequence),
        (nameof(ClassConfiguration.Combat), static c => c.Combat.Sequence),
        (nameof(ClassConfiguration.Adhoc), static c => c.Adhoc.Sequence),
        (nameof(ClassConfiguration.Parallel), static c => c.Parallel.Sequence),
        (nameof(ClassConfiguration.NPC), static c => c.NPC.Sequence),
        (nameof(ClassConfiguration.AssistFocus), static c => c.AssistFocus.Sequence),
        (nameof(ClassConfiguration.Wait), static c => c.Wait.Sequence)
    ];

    internal static void ApplyEnabledStates(ClassConfiguration classConfig, JObject profileJson)
    {
        for (int i = 0; i < PersistedSections.Length; i++)
        {
            (string sectionName, Func<ClassConfiguration, KeyAction[]> resolver) = PersistedSections[i];
            SyncSection(profileJson, sectionName, resolver(classConfig));
        }
    }

    private static void SyncSection(JObject root, string sectionName, KeyAction[] runtimeSequence)
    {
        if (root[sectionName] is not JObject sectionObject ||
            sectionObject[nameof(KeyActions.Sequence)] is not JArray sequenceArray)
        {
            return;
        }

        Dictionary<string, int> ordinalByName = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < runtimeSequence.Length; i++)
        {
            KeyAction runtimeAction = runtimeSequence[i];
            if (string.IsNullOrWhiteSpace(runtimeAction.Name))
            {
                continue;
            }

            int ordinal = ordinalByName.TryGetValue(runtimeAction.Name, out int existingOrdinal)
                ? existingOrdinal
                : 0;
            ordinalByName[runtimeAction.Name] = ordinal + 1;

            JObject? actionNode = FindNthAction(sequenceArray, runtimeAction.Name, ordinal);
            if (actionNode == null)
            {
                if (sectionName == nameof(ClassConfiguration.Wait) &&
                    RecoveryState.IsGeneratedRecoveryWaitActionName(runtimeAction.Name))
                {
                    actionNode = CreateGeneratedWaitActionNode(runtimeAction);
                    sequenceArray.Add(actionNode);
                }
                else
                {
                    continue;
                }
            }

            actionNode[nameof(KeyAction.Enabled)] = runtimeAction.Enabled;
        }
    }

    private static JObject? FindNthAction(JArray sequenceArray, string actionName, int ordinal)
    {
        int seen = 0;

        for (int i = 0; i < sequenceArray.Count; i++)
        {
            if (sequenceArray[i] is not JObject candidate)
            {
                continue;
            }

            string? candidateName = candidate.Value<string>(nameof(KeyAction.Name));
            if (!string.Equals(candidateName, actionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen == ordinal)
            {
                return candidate;
            }

            seen++;
        }

        return null;
    }

    private static JObject CreateGeneratedWaitActionNode(KeyAction runtimeAction)
    {
        JObject node = new()
        {
            [nameof(KeyAction.Name)] = runtimeAction.Name,
            [nameof(KeyAction.Cost)] = runtimeAction.Cost,
            [nameof(KeyAction.Enabled)] = runtimeAction.Enabled
        };

        if (!string.IsNullOrWhiteSpace(runtimeAction.Requirement))
        {
            node[nameof(KeyAction.Requirement)] = runtimeAction.Requirement;
        }

        return node;
    }
}
