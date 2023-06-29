using Microsoft.Extensions.Logging;

using System;

namespace Core;

public sealed partial class WaitKeyActions : KeyActions
{
    public float FoodDrinkCost { get; set; } = 4.09f;

    public bool AutoGenerateWaitForFoodAndDrink { get; set; } = true;

    public override void PreInitialise(string prefix, RequirementFactory requirementFactory, ILogger logger)
    {
        base.PreInitialise(prefix, requirementFactory, logger);
    }

    public override void Initialise(string prefix,
        ClassConfiguration config, AddonReader addonReader, PlayerReader playerReader,
        RecordInt globalTime, ActionBarCostReader actionBarCostReader,
        RequirementFactory requirementFactory, ILogger logger, bool globalLog)
    {
        if (AutoGenerateWaitForFoodAndDrink)
            AddWaitKeyActionsForFoodOrDrink(logger, config);

        base.Initialise(prefix, config, addonReader, playerReader, globalTime, actionBarCostReader, requirementFactory, logger, globalLog);
    }

    private void AddWaitKeyActionsForFoodOrDrink(ILogger logger, ClassConfiguration classConfig)
    {
        bool foodExists = false;
        bool drinkExists = false;

        for (int i = 0; i < classConfig.Adhoc.Sequence.Length; i++)
        {
            KeyAction action = classConfig.Adhoc.Sequence[i];
            if (action.Name.Contains(RequirementFactory.Food, StringComparison.InvariantCultureIgnoreCase))
            {
                foodExists = true;
            }
            else if (action.Name.Contains(RequirementFactory.Drink, StringComparison.InvariantCultureIgnoreCase))
            {
                drinkExists = true;
            }
        }

        for (int i = 0; i < classConfig.Parallel.Sequence.Length; i++)
        {
            KeyAction action = classConfig.Parallel.Sequence[i];
            if (action.Name.Contains(RequirementFactory.Food, StringComparison.InvariantCultureIgnoreCase))
            {
                foodExists = true;
            }
            else if (action.Name.Contains(RequirementFactory.Drink, StringComparison.InvariantCultureIgnoreCase))
            {
                drinkExists = true;
            }
        }

        if (foodExists)
        {
            KeyAction foodWaitAction = new()
            {
                Cost = FoodDrinkCost,
                Name = "Eating",
                Requirement = $"{RequirementFactory.Food} && {RequirementFactory.HealthP} < 99"
            };

            KeyAction[] keyActions = Sequence;

            int newSize = keyActions.Length + 1;
            Array.Resize(ref keyActions, newSize);

            keyActions[^1] = foodWaitAction;

            Sequence = keyActions;

            LogAddedWait(logger, nameof(WaitKeyActions), RequirementFactory.Food);
        }

        if (drinkExists)
        {
            KeyAction drinkWaitAction = new()
            {
                Cost = FoodDrinkCost,
                Name = "Drinking",
                Requirement = $"{RequirementFactory.Drink} && {RequirementFactory.ManaP} < 99"
            };

            KeyAction[] keyActions = Sequence;

            int newSize = keyActions.Length + 1;
            Array.Resize(ref keyActions, newSize);

            keyActions[^1] = drinkWaitAction;

            Sequence = keyActions;

            LogAddedWait(logger, nameof(WaitKeyActions), RequirementFactory.Drink);
        }
    }

    #region logging

    [LoggerMessage(
        EventId = 0016,
        Level = LogLevel.Information,
        Message = "[{typeName}] Added awaiting action for {keyActionName}")]
    static partial void LogAddedWait(ILogger logger, string typeName, string keyActionName);

    #endregion

}