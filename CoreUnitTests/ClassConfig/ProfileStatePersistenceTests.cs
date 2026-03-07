using Core;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json.Linq;

using System.Collections.Generic;

using Xunit;

namespace CoreUnitTests.ClassConfig;

public sealed class ProfileStatePersistenceTests
{
    [Fact]
    public void ApplyEnabledStates_UpdatesExistingActions_AndAppendsGeneratedWaitActions()
    {
        ClassConfiguration config = new();
        config.Pull.Sequence =
        [
            new KeyAction
            {
                Name = "Curse of Agony",
                Enabled = false
            }
        ];
        config.Wait.Sequence =
        [
            new KeyAction
            {
                Name = "Food Buff",
                Enabled = false,
                Cost = 4.09f,
                Requirement = "Food && Health% < 100"
            }
        ];

        JObject profileJson = JObject.Parse("""
        {
          "Pull": {
            "Sequence": [
              { "Name": "Curse of Agony", "Key": "5" }
            ]
          },
          "Wait": {
            "Sequence": []
          }
        }
        """);

        ProfileStatePersistence.ApplyEnabledStates(config, profileJson);

        profileJson["Pull"]!["Sequence"]![0]![nameof(KeyAction.Enabled)]!.Value<bool>()
            .Should().BeFalse();

        JArray waitSequence = (JArray)profileJson["Wait"]!["Sequence"]!;
        waitSequence.Should().ContainSingle();
        waitSequence[0]![nameof(KeyAction.Name)]!.Value<string>().Should().Be("Food Buff");
        waitSequence[0]![nameof(KeyAction.Requirement)]!.Value<string>().Should().Be("Food && Health% < 100");
        waitSequence[0]![nameof(KeyAction.Enabled)]!.Value<bool>().Should().BeFalse();
    }

    [Fact]
    public void WaitKeyActions_AddWaitKeyActionsForFoodOrDrink_DoesNotDuplicateExistingWaitEntries()
    {
        WaitKeyActions wait = new()
        {
            Sequence =
            [
                new KeyAction
                {
                    Name = "Food Buff",
                    Requirement = "Food && Health% < 100"
                }
            ]
        };

        KeyActions parallel = new()
        {
            Sequence =
            [
                new KeyAction { Name = "Food" },
                new KeyAction { Name = "Drink" }
            ]
        };

        wait.AddWaitKeyActionsForFoodOrDrink(
            NullLoggerFactory.Instance.CreateLogger("test"),
            new List<(string, KeyActions)>
            {
                ("Parallel", parallel)
            });

        wait.Sequence.Should().HaveCount(2);
        wait.Sequence.Should().ContainSingle(static a => a.Name == "Food Buff");
        wait.Sequence.Should().ContainSingle(static a => a.Name == "Drink Buff");
    }
}
