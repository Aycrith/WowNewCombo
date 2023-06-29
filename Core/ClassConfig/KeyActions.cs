using Microsoft.Extensions.Logging;
using System;

namespace Core;

public partial class KeyActions : IDisposable
{
    public KeyAction[] Sequence { get; set; } =
        Array.Empty<KeyAction>();

    public virtual void PreInitialise(string prefix,
        RequirementFactory requirementFactory, ILogger logger)
    {
        if (Sequence.Length > 0)
        {
            LogDynamicBinding(logger, prefix);
            requirementFactory.AddSequenceRange(this);
        }

        for (int i = 0; i < Sequence.Length; i++)
        {
            KeyAction keyAction = Sequence[i];
            keyAction.InitialiseSlot(logger);
            keyAction.InitDynamicBinding(requirementFactory);
        }
    }

    public virtual void Initialise(string prefix,
        ClassConfiguration config, AddonReader addonReader,
        PlayerReader playerReader, RecordInt globalTime,
        ActionBarCostReader costReader,
        RequirementFactory requirementFactory, ILogger logger,
        bool globalLog)
    {
        if (Sequence.Length > 0)
        {
            LogInitKeyActions(logger, prefix);
        }

        for (int i = 0; i < Sequence.Length; i++)
        {
            Sequence[i].Initialise(config, addonReader, playerReader,
                globalTime, costReader, requirementFactory,
                logger, globalLog);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < Sequence.Length; i++)
        {
            Sequence[i].Dispose();
        }
    }

    [LoggerMessage(
        EventId = 0010,
        Level = LogLevel.Information,
        Message = "[{prefix}] CreateDynamicBindings.")]
    protected static partial void LogDynamicBinding(ILogger logger, string prefix);

    [LoggerMessage(
        EventId = 0011,
        Level = LogLevel.Information,
        Message = "[{prefix}] Initialise KeyActions.")]
    protected static partial void LogInitKeyActions(ILogger logger, string prefix);

}