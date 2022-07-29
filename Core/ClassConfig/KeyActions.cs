using Microsoft.Extensions.Logging;
using System;

namespace Core
{
    public partial class KeyActions : IDisposable
    {
        public KeyAction[] Sequence { get; init; } = Array.Empty<KeyAction>();

        public void PreInitialise(string prefix, RequirementFactory requirementFactory, ILogger logger)
        {
            if (Sequence.Length > 0)
            {
                LogDynamicBinding(logger, prefix);
            }

            for (int i = 0; i < Sequence.Length; i++)
            {
                var keyAction = Sequence[i];
                keyAction.InitialiseSlot(logger);
                keyAction.InitDynamicBinding(requirementFactory);
            }
        }

        public void Initialise(string prefix, ClassConfiguration config, AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger, bool globalLog)
        {
            if (Sequence.Length > 0)
            {
                LogInitKeyActions(logger, prefix);
            }

            for (int i = 0; i < Sequence.Length; i++)
            {
                Sequence[i].Initialise(config, addonReader, requirementFactory, logger, globalLog, this);
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
            EventId = 10,
            Level = LogLevel.Information,
            Message = "[{prefix}] CreateDynamicBindings.")]
        static partial void LogDynamicBinding(ILogger logger, string prefix);

        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Information,
            Message = "[{prefix}] Initialise KeyActions.")]
        static partial void LogInitKeyActions(ILogger logger, string prefix);

    }
}