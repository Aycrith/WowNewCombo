using System;

namespace Core;

internal static class RecoveryState
{
    internal const int FullResourcePercent = 100;

    internal static bool IsRecoveryActive(
        bool hasFoodBuff,
        bool hasDrinkBuff,
        int healthPercent,
        int manaPercent)
    {
        return (hasFoodBuff && healthPercent < FullResourcePercent) ||
               (hasDrinkBuff && manaPercent < FullResourcePercent);
    }

    internal static bool IsRecoveryActionName(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return false;
        }

        return actionName.Equals("Food", StringComparison.OrdinalIgnoreCase) ||
               actionName.Equals("Drink", StringComparison.OrdinalIgnoreCase) ||
               actionName.Equals("Food Buff", StringComparison.OrdinalIgnoreCase) ||
               actionName.Equals("Drink Buff", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsGeneratedRecoveryWaitActionName(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return false;
        }

        return actionName.Equals("Food Buff", StringComparison.OrdinalIgnoreCase) ||
               actionName.Equals("Drink Buff", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ShouldSuppressNonRecoveryAction(
        string? actionName,
        bool hasFoodBuff,
        bool hasDrinkBuff,
        int healthPercent,
        int manaPercent)
    {
        if (!IsRecoveryActive(hasFoodBuff, hasDrinkBuff, healthPercent, manaPercent))
        {
            return false;
        }

        return !IsRecoveryActionName(actionName);
    }
}
