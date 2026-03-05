namespace Core.BehaviorTree;

using System;
using System.Collections.Generic;
using System.Linq;

using Core.BehaviorTree.Nodes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Converts class configuration JSON to behavior tree structures.
/// </summary>
public sealed class JsonToBehaviorTreeConverter
{
    private readonly ILogger logger;

    /// <summary>
    /// Creates a new converter.
    /// </summary>
    public JsonToBehaviorTreeConverter(ILogger logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Converts combat sequence to behavior tree.
    /// </summary>
    /// <param name="config">Class configuration.</param>
    /// <returns>Root behavior node.</returns>
    public IBehaviorNode ConvertCombatSequence(ClassConfiguration config)
    {
        SelectorNode root = new("CombatRoot");

        // Emergency actions (health < 20%)
        SequenceNode? emergency = CreateEmergencySequence(config);
        if (emergency != null)
        {
            root.Children.Add(emergency);
        }

        // Normal rotation
        SelectorNode rotation = new("Rotation");
        KeyAction[]? sequence = config.Combat?.Sequence?.ToArray();
        if (sequence != null)
        {
            foreach (KeyAction keyAction in sequence)
            {
                SequenceNode action = CreateActionWithConditions(keyAction);
                rotation.Children.Add(action);
            }

            int actionCount = sequence.Length;
            logger.LogInformation("[BehaviorTreeConverter] Converted {ActionCount} actions to behavior tree", actionCount);
        }

        // Fallback auto-attack
        rotation.Children.Add(new ActionNode("AutoAttack", _ =>
        {
            // Trigger auto-attack
            return NodeStatus.Success;
        }));

        root.Children.Add(rotation);

        return root;
    }

    /// <summary>
    /// Creates emergency sequence if health-based actions exist.
    /// </summary>
    private SequenceNode? CreateEmergencySequence(ClassConfiguration config)
    {
        if (config.Combat?.Sequence == null)
        {
            return null;
        }

        // Look for healthstone/healing potion in combat sequence
        KeyAction? emergencyAction = config.Combat.Sequence.FirstOrDefault(a =>
            a.Name.Contains("Healthstone", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("Healing Potion", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("Health", StringComparison.OrdinalIgnoreCase));

        if (emergencyAction == null)
        {
            return null;
        }

        SequenceNode sequence = new("Emergency");
        sequence.Children.Add(new ConditionNode("LowHealth", ctx => ctx.Player.HealthPercent() < 20));
        sequence.Children.Add(CreateActionFromKeyAction(emergencyAction));

        return sequence;
    }

    /// <summary>
    /// Creates action node with conditions from KeyAction.
    /// </summary>
    private SequenceNode CreateActionWithConditions(KeyAction keyAction)
    {
        SequenceNode sequence = new(keyAction.Name);

        // Add cooldown condition
        sequence.Children.Add(new ConditionNode($"{keyAction.Name}_Cooldown", ctx =>
        {
            // Check if action is on cooldown
            return ctx.LastAction?.Name != keyAction.Name || ctx.ElapsedMs > 1500;
        }));

        // Add custom requirements as conditions
        if (keyAction.Requirements != null)
        {
            foreach (string requirement in keyAction.Requirements)
            {
                string req = requirement;
                sequence.Children.Add(new ConditionNode($"{keyAction.Name}_{req}", ctx =>
                {
                    return EvaluateRequirement(ctx, req);
                }));
            }
        }

        // Add the action itself
        sequence.Children.Add(CreateActionFromKeyAction(keyAction));

        return sequence;
    }

    /// <summary>
    /// Creates action node from KeyAction.
    /// </summary>
    private ActionNode CreateActionFromKeyAction(KeyAction keyAction)
    {
        return new ActionNode($"Cast_{keyAction.Name}", ctx =>
        {
            try
            {
                logger.LogDebug("[BehaviorTreeCombat] Executing action: {ActionName}", keyAction.Name);

                // Store last action
                ctx.LastAction = keyAction;

                // Execute the action
                _ = ctx.Casting.CastIfReady(keyAction, () => false);

                return NodeStatus.Success;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[BehaviorTreeCombat] Failed to execute action: {ActionName}", keyAction.Name);
                return NodeStatus.Failure;
            }
        });
    }

    /// <summary>
    /// Evaluates a requirement string.
    /// </summary>
    private bool EvaluateRequirement(BehaviorContext ctx, string requirement)
    {
        try
        {
            // Parse common requirement patterns
            // Examples: "Health% > 50", "Mana% > 20", "TargetHealth% < 20", "HasBuff: Power Word: Fortitude"

            if (requirement.Contains("Health%", StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateHealthRequirement(ctx, requirement);
            }

            if (requirement.Contains("Mana%", StringComparison.OrdinalIgnoreCase) ||
                requirement.Contains("Rage%", StringComparison.OrdinalIgnoreCase) ||
                requirement.Contains("Energy%", StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateResourceRequirement(ctx, requirement);
            }

            if (requirement.Contains("TargetHealth%", StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateTargetHealthRequirement(ctx, requirement);
            }

            if (requirement.Contains("HasBuff", StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateBuffRequirement(ctx, requirement);
            }

            if (requirement.Contains("Combo", StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateComboPointsRequirement(ctx, requirement);
            }

            // Default: assume requirement is satisfied
            logger.LogDebug("[BehaviorTreeConverter] Unknown requirement: {Requirement}", requirement);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BehaviorTreeConverter] Error evaluating requirement: {Requirement}", requirement);
            return false;
        }
    }

    private static bool EvaluateHealthRequirement(BehaviorContext ctx, string requirement)
    {
        // Parse "Health% > X" or "Health% < X"
        int threshold = ParseThreshold(requirement);
        int healthPercent = ctx.Player.HealthPercent();

        if (requirement.Contains('>'))
        {
            return healthPercent > threshold;
        }
        else if (requirement.Contains('<'))
        {
            return healthPercent < threshold;
        }

        return true;
    }

    private static bool EvaluateResourceRequirement(BehaviorContext ctx, string requirement)
    {
        int threshold = ParseThreshold(requirement);
        int resourcePercent = ctx.Player.ManaPercent();

        if (requirement.Contains('>'))
        {
            return resourcePercent > threshold;
        }
        else if (requirement.Contains('<'))
        {
            return resourcePercent < threshold;
        }

        return true;
    }

    private static bool EvaluateTargetHealthRequirement(BehaviorContext ctx, string requirement)
    {
        if (ctx.CurrentTarget == null)
        {
            return false;
        }

        int threshold = ParseThreshold(requirement);

        if (requirement.Contains('>'))
        {
            return ctx.CurrentTarget.HealthPercent > threshold;
        }
        else if (requirement.Contains('<'))
        {
            return ctx.CurrentTarget.HealthPercent < threshold;
        }

        return true;
    }

    private static bool EvaluateBuffRequirement(BehaviorContext ctx, string requirement)
    {
        // Parse "HasBuff: BuffName"
        int colonIndex = requirement.IndexOf(':');
        if (colonIndex < 0)
        {
            return false;
        }

        string buffName = requirement[(colonIndex + 1)..].Trim();
        // Check if player has buff - would need implementation
        return false;
    }

    private static bool EvaluateComboPointsRequirement(BehaviorContext ctx, string requirement)
    {
        // Parse "ComboPoints >= X"
        int threshold = ParseThreshold(requirement);

        // Would need combo points from player reader
        return false;
    }

    private static int ParseThreshold(string requirement)
    {
        // Extract number from requirement string
        foreach (string part in requirement.Split([' ', '>', '<', '='], StringSplitOptions.RemoveEmptyEntries))
        {
            string cleanPart = part.Replace("%", "");
            if (int.TryParse(cleanPart, out int value))
            {
                return value;
            }
        }

        return 0;
    }
}
