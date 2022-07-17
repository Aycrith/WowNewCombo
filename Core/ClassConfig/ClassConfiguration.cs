using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Core
{
    public class BadZone
    {
        public int ZoneId { get; init; } = -1;
        public Vector3 ExitZoneLocation { get; init; }
    }

    public enum Mode
    {
        Grind = 0,
        CorpseRun = 1,
        AttendedGather = 2,
        AttendedGrind = 3,
        AssistFocus = 4
    }


    public class ClassConfiguration : IDisposable
    {
        public bool Log { get; set; } = true;
        public bool Loot { get; set; } = true;
        public bool Skin { get; set; }
        public bool Herb { get; set; }
        public bool Mine { get; set; }
        public bool Salvage { get; set; }
        public bool GatherCorpse => Skin || Herb || Mine || Salvage;

        public bool UseMount { get; set; } = true;
        public bool KeyboardOnly { get; set; }

        public string PathFilename { get; set; } = string.Empty;

        public string? OverridePathFilename { get; set; } = string.Empty;

        public bool PathThereAndBack { get; set; } = true;
        public bool PathReduceSteps { get; set; }

        public Mode Mode { get; init; } = Mode.Grind;

        public BadZone WrongZone { get; } = new BadZone();

        public int NPCMaxLevels_Above { get; set; } = 1;
        public int NPCMaxLevels_Below { get; set; } = 7;

        public bool CheckTargetGivesExp { get; set; }
        public string[] Blacklist { get; init; } = Array.Empty<string>();

        public Dictionary<int, SchoolMask[]> ImmunityBlacklist { get; } = new();

        public Dictionary<string, int> IntVariables { get; } = new();

        public KeyActions Pull { get; } = new();
        public KeyActions Combat { get; } = new();
        public KeyActions Adhoc { get; } = new();
        public KeyActions Parallel { get; } = new();
        public KeyActions NPC { get; } = new();

        public KeyAction[] Form { get; init; } = Array.Empty<KeyAction>();
        public KeyAction[] GatherFindKeyConfig { get; set; } = Array.Empty<KeyAction>();
        public string[] GatherFindKeys { get; init; } = Array.Empty<string>();

        public KeyAction Jump { get; } = new();
        public string JumpKey { get; init; } = "Spacebar";

        public KeyAction Interact { get; } = new();
        public string InteractKey { get; init; } = "I";

        public KeyAction Approach { get; } = new();
        public KeyAction AutoAttack { get; } = new();

        public KeyAction TargetLastTarget { get; } = new();
        public string TargetLastTargetKey { get; init; } = "G";

        public KeyAction StandUp { get; } = new();
        public string StandUpKey { get; init; } = "X";

        public KeyAction ClearTarget { get; } = new();
        public string ClearTargetKey { get; init; } = "Insert";

        public KeyAction StopAttack { get; } = new();
        public string StopAttackKey { get; init; } = "Delete";

        public KeyAction TargetNearestTarget { get; } = new();
        public string TargetNearestTargetKey { get; init; } = "Tab";

        public KeyAction TargetTargetOfTarget { get; } = new();
        public string TargetTargetOfTargetKey { get; init; } = "F";
        public KeyAction TargetPet { get; } = new();
        public string TargetPetKey { get; init; } = "Multiply";

        public KeyAction PetAttack { get; } = new();
        public string PetAttackKey { get; init; } = "Subtract";

        public KeyAction TargetFocus { get; } = new();
        public string TargetFocusKey { get; init; } = "PageUp";

        public KeyAction FollowTarget { get; } = new();
        public string FollowTargetKey { get; init; } = "PageDown";

        public KeyAction Mount { get; } = new();
        public string MountKey { get; init; } = "O";

        public KeyAction Hearthstone { get; } = new();
        public string HearthstoneKey { get; init; } = "I";

        public ConsoleKey ForwardKey { get; init; } = ConsoleKey.UpArrow;  // 38
        public ConsoleKey BackwardKey { get; init; } = ConsoleKey.DownArrow; // 40
        public ConsoleKey TurnLeftKey { get; init; } = ConsoleKey.LeftArrow; // 37
        public ConsoleKey TurnRightKey { get; init; } = ConsoleKey.RightArrow; // 39

        public void Initialise(DataConfig dataConfig, AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger, string? overridePathProfileFile)
        {
            requirementFactory.InitUserDefinedIntVariables(IntVariables);

            Jump.Key = JumpKey;
            Jump.Name = nameof(Jump);
            Jump.Initialise(addonReader, requirementFactory, logger, Log);

            TargetLastTarget.Key = TargetLastTargetKey;
            TargetLastTarget.Name = nameof(TargetLastTarget);
            TargetLastTarget.Cooldown = 0;
            TargetLastTarget.Initialise(addonReader, requirementFactory, logger, Log);

            StandUp.Key = StandUpKey;
            StandUp.Name = nameof(StandUp);
            StandUp.Cooldown = 0;
            StandUp.Initialise(addonReader, requirementFactory, logger, Log);

            ClearTarget.Key = ClearTargetKey;
            ClearTarget.Name = nameof(ClearTarget);
            ClearTarget.Cooldown = 0;
            ClearTarget.Initialise(addonReader, requirementFactory, logger, Log);

            StopAttack.Key = StopAttackKey;
            StopAttack.Name = nameof(StopAttack);
            StopAttack.Cooldown = 400;
            StopAttack.WaitForGCD = false;
            StopAttack.PressDuration = 20;
            StopAttack.SkipValidation = true;
            StopAttack.Initialise(addonReader, requirementFactory, logger, Log);

            TargetNearestTarget.Key = TargetNearestTargetKey;
            TargetNearestTarget.Name = nameof(TargetNearestTarget);
            TargetNearestTarget.Cooldown = 400;
            TargetNearestTarget.Initialise(addonReader, requirementFactory, logger, Log);

            TargetPet.Key = TargetPetKey;
            TargetPet.Name = nameof(TargetPet);
            TargetPet.Cooldown = 0;
            TargetPet.Initialise(addonReader, requirementFactory, logger, Log);

            TargetTargetOfTarget.Key = TargetTargetOfTargetKey;
            TargetTargetOfTarget.Name = nameof(TargetTargetOfTarget);
            TargetTargetOfTarget.Cooldown = 0;
            TargetTargetOfTarget.Initialise(addonReader, requirementFactory, logger, Log);

            TargetFocus.Key = TargetFocusKey;
            TargetFocus.Name = nameof(TargetFocus);
            TargetFocus.Cooldown = 0;
            TargetFocus.Initialise(addonReader, requirementFactory, logger, Log);

            FollowTarget.Key = FollowTargetKey;
            FollowTarget.Name = nameof(FollowTarget);
            FollowTarget.Cooldown = 0;
            FollowTarget.Initialise(addonReader, requirementFactory, logger, Log);

            PetAttack.Key = PetAttackKey;
            PetAttack.Name = nameof(PetAttack);
            PetAttack.PressDuration = 10;
            PetAttack.Cooldown = 400;
            PetAttack.Initialise(addonReader, requirementFactory, logger, Log);

            Mount.Key = MountKey;
            Mount.Name = nameof(Mount);
            Mount.Initialise(addonReader, requirementFactory, logger, Log);

            Hearthstone.Key = HearthstoneKey;
            Hearthstone.Name = nameof(Hearthstone);
            Hearthstone.Initialise(addonReader, requirementFactory, logger, Log);

            Interact.Key = InteractKey;
            Interact.Name = nameof(Interact);
            Interact.Cooldown = 0;
            Interact.WaitForGCD = false;
            Interact.DelayAfterCast = 0;
            Interact.PressDuration = 30;
            Interact.SkipValidation = true;
            Interact.Cooldown = 0;
            Interact.Initialise(addonReader, requirementFactory, logger, Log);

            Approach.Key = InteractKey;
            Approach.Name = nameof(Approach);
            Approach.WaitForGCD = false;
            Approach.DelayAfterCast = 0;
            Approach.PressDuration = 10;
            Approach.Cooldown = 400;
            Approach.SkipValidation = true;
            Approach.Initialise(addonReader, requirementFactory, logger, Log);

            AutoAttack.Key = InteractKey;
            AutoAttack.Name = nameof(AutoAttack);
            AutoAttack.Cooldown = 0;
            AutoAttack.WaitForGCD = false;
            AutoAttack.DelayAfterCast = 0;
            AutoAttack.SkipValidation = true;
            AutoAttack.Initialise(addonReader, requirementFactory, logger, Log);

            InitializeKeyActions(Pull, Interact, Approach, AutoAttack, StopAttack);
            InitializeKeyActions(Combat, Interact, Approach, AutoAttack, StopAttack);

            logger.LogInformation($"[{nameof(Form)}] Initialise KeyActions.");
            for (int i = 0; i < Form.Length; i++)
            {
                Form[i].InitialiseForm(addonReader, requirementFactory, logger, Log);
            }

            Pull.PreInitialise(nameof(Pull), requirementFactory, logger);
            Combat.PreInitialise(nameof(Combat), requirementFactory, logger);
            Adhoc.PreInitialise(nameof(Adhoc), requirementFactory, logger);
            NPC.PreInitialise(nameof(NPC), requirementFactory, logger);
            Parallel.PreInitialise(nameof(Parallel), requirementFactory, logger);

            Pull.Initialise(nameof(Pull), addonReader, requirementFactory, logger, Log);
            Combat.Initialise(nameof(Combat), addonReader, requirementFactory, logger, Log);
            Adhoc.Initialise(nameof(Adhoc), addonReader, requirementFactory, logger, Log);
            NPC.Initialise(nameof(NPC), addonReader, requirementFactory, logger, Log);
            Parallel.Initialise(nameof(Parallel), addonReader, requirementFactory, logger, Log);

            int index = 0;
            GatherFindKeyConfig = new KeyAction[GatherFindKeys.Length];
            for (int i = 0; i < GatherFindKeys.Length; i++)
            {
                GatherFindKeyConfig[index] = new KeyAction { Key = GatherFindKeys[index], Name = $"Profession {index}" };
                GatherFindKeyConfig[index].Initialise(addonReader, requirementFactory, logger, Log);
                index++;
            }

            OverridePathFilename = overridePathProfileFile;
            if (!string.IsNullOrEmpty(OverridePathFilename))
            {
                PathFilename = OverridePathFilename;
            }

            if (!File.Exists(Path.Join(dataConfig.Path, PathFilename)))
            {
                if (!string.IsNullOrEmpty(OverridePathFilename))
                    throw new Exception($"[{nameof(ClassConfiguration)}] `{OverridePathFilename}` file does not exists!");
                else
                    throw new Exception($"[{nameof(ClassConfiguration)}] `{PathFilename}` file does not exists!");
            }

            CheckConfigConsistency(logger);
        }

        public void Dispose()
        {
            Pull.Dispose();
            Combat.Dispose();
            Parallel.Dispose();
            Adhoc.Dispose();
            NPC.Dispose();
        }

        private void CheckConfigConsistency(ILogger logger)
        {
            if (CheckTargetGivesExp)
            {
                logger.LogWarning($"{nameof(CheckTargetGivesExp)} is enabled. { nameof(NPCMaxLevels_Above)} and {nameof(NPCMaxLevels_Below)} will be ignored.");
            }
            if (KeyboardOnly)
            {
<<<<<<< HEAD
                logger.LogWarning($"{nameof(KeyboardOnly)} mode is enabled. The bot will not try to utilize your mouse. {nameof(GatherCorpse)} will be disabled and the npc target function will be limited.");
=======
                logger.LogWarning("KeyboardOnly mode is enabled. The bot will not try to utilize your mouse. Gathering will be disabled and the npc target function will be limited.");
>>>>>>> e2c5b1d9d3597052cb73eb5bf301e4cf19d80672
                Skin = false;
                Mine = false;
                Herb = false;
                Salvage = false;
            }
        }

        private static void InitializeKeyActions(KeyActions userActions, params KeyAction[] defaultActions)
        {
            KeyAction dummyDefault = new();
            for (int i = 0; i < userActions.Sequence.Length; i++)
            {
                KeyAction user = userActions.Sequence[i];
                for (int d = 0; d < defaultActions.Length; d++)
                {
                    KeyAction @default = defaultActions[d];

                    if (user.Name != @default.Name)
                        continue;

                    user.Key = @default.Key;
                    user.WaitForGCD = @default.WaitForGCD;

                    //if (!string.IsNullOrEmpty(@default.Requirement))
                    //    user.Requirement += " " + @default.Requirement;
                    //user.Requirements.AddRange(@default.Requirements);

                    if (user.DelayAfterCast == dummyDefault.DelayAfterCast)
                        user.DelayAfterCast = @default.DelayAfterCast;

                    if (user.DelayAfterCast == dummyDefault.DelayAfterCast)
                        user.PressDuration = @default.PressDuration;

                    if (user.Cooldown == dummyDefault.Cooldown)
                        user.Cooldown = @default.Cooldown;

                    if (user.SkipValidation == dummyDefault.SkipValidation)
                        user.SkipValidation = @default.SkipValidation;
                }
            }
        }
    }
}