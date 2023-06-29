using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Core.Database;
using Core.Goals;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.NpcFinder;

namespace Core;

public sealed partial class RequirementFactory
{
    private readonly ILogger logger;
    private readonly AddonReader addonReader;
    private readonly PlayerReader playerReader;
    private readonly BuffStatus buffs;
    private readonly BagReader bagReader;
    private readonly EquipmentReader equipmentReader;
    private readonly SpellBookReader spellBookReader;
    private readonly TalentReader talentReader;
    private readonly CreatureDB creatureDb;
    private readonly ItemDB itemDb;

    private readonly AuraTimeReader<IPlayerBuffTimeReader> playerBuffTimeReader;
    private readonly AuraTimeReader<ITargetDebuffTimeReader> targetDebuffTimeReader;

    private readonly ActionBarBits<ICurrentAction> currentAction;
    private readonly ActionBarBits<IUsableAction> usableAction;
    private readonly ActionBarCooldownReader cooldownReader;
    private readonly ActionBarCostReader costReader;

    private KeyAction[] keyActions = Array.Empty<KeyAction>();

    private readonly Dictionary<int, SchoolMask[]> immunityBlacklist;

    private readonly string[] negateKeywords = new string[2]
    {
        "not ",
        "!"
    };

    private readonly Dictionary<string, Func<int>> intVariables;

    private readonly Dictionary<string, Func<bool>> boolVariables;

    private readonly Dictionary<string, Func<string, Requirement>> requirementMap;

    private const char SEP1 = ':';
    private const char SEP2 = ',';

    private const string Swimming = "Swimming";
    private const string Falling = "Falling";

    public const string AddVisible = "AddVisible";
    public const string Drink = "Drink";
    public const string Food = "Food";

    public const string HealthP = "Health%";
    public const string ManaP = "Mana%";

    private const string greaterThenOrEqual = ">=";
    private const string lessThenOrEqual = "<=";
    private const string greaterThen = ">";
    private const string lessThen = "<";
    private const string equals = "==";
    private const string modulo = "%";

    public RequirementFactory(IServiceProvider sp, ClassConfiguration classConfig)
    {
        this.logger = sp.GetRequiredService<ILogger>();
        this.addonReader = sp.GetRequiredService<AddonReader>();
        this.playerReader = sp.GetRequiredService<PlayerReader>();
        this.buffs = sp.GetRequiredService<BuffStatus>();
        this.bagReader = sp.GetRequiredService<BagReader>();
        this.equipmentReader = sp.GetRequiredService<EquipmentReader>();
        this.spellBookReader = sp.GetRequiredService<SpellBookReader>();
        this.talentReader = sp.GetRequiredService<TalentReader>();
        this.creatureDb = sp.GetRequiredService<CreatureDB>();
        this.itemDb = sp.GetRequiredService<ItemDB>();

        this.playerBuffTimeReader = sp.GetRequiredService<AuraTimeReader<IPlayerBuffTimeReader>>();
        this.targetDebuffTimeReader = sp.GetRequiredService<AuraTimeReader<ITargetDebuffTimeReader>>();

        this.currentAction = sp.GetRequiredService<ActionBarBits<ICurrentAction>>();
        this.usableAction = sp.GetRequiredService<ActionBarBits<IUsableAction>>();
        this.cooldownReader = sp.GetRequiredService<ActionBarCooldownReader>();
        this.costReader = sp.GetRequiredService<ActionBarCostReader>();

        this.immunityBlacklist = classConfig.ImmunityBlacklist;

        NpcNameFinder npcNameFinder = sp.GetRequiredService<NpcNameFinder>();
        AddonBits bits = sp.GetRequiredService<AddonBits>();
        BuffStatus playerBuffs = sp.GetRequiredService<BuffStatus>();
        TargetDebuffStatus targetDebuffs = sp.GetRequiredService<TargetDebuffStatus>();
        SessionStat sessionStat = sp.GetRequiredService<SessionStat>();
        CombatLog combatLog = sp.GetRequiredService<CombatLog>();

        requirementMap = new()
        {
            { greaterThenOrEqual, CreateGreaterOrEquals },
            { lessThenOrEqual, CreateLesserOrEquals },
            { greaterThen, CreateGreaterThen },
            { lessThen, CreateLesserThen },
            { equals, CreateEquals },
            { modulo, CreateModulo },
            { "npcID:", CreateNpcId },
            { "BagItem:", CreateBagItem },
            { "SpellInRange:", CreateSpellInRange },
            { "TargetCastingSpell", CreateTargetCastingSpell },
            { "Form", CreateForm },
            { "Race", CreateRace },
            { "Spell", CreateSpell },
            { "Talent", CreateTalent },
            { "Trigger:", CreateTrigger },
            { "Usable:", CreateUsable }
        };

        boolVariables = new()
        {
            // Target Based
            { "TargetYieldXP", bits.TargetIsNotTrivial },
            { "TargetsMe", playerReader.TargetsMe },
            { "TargetsPet", playerReader.TargetsPet },
            { "TargetsNone", playerReader.TargetsNone },

            { AddVisible, npcNameFinder._PotentialAddsExist },
            { "InCombat", bits.PlayerInCombat },

            // Range
            { "InMeleeRange", playerReader.IsInMeleeRange },
            { "InCloseMeleeRange", playerReader.InCloseMeleeRange },
            { "InDeadZoneRange", playerReader.IsInDeadZone },
            { "OutOfCombatRange", playerReader.OutOfCombatRange },
            { "InCombatRange", playerReader.WithInCombatRange },
            
            // Pet
            { "Has Pet", bits.HasPet },
            { "Pet Happy", bits.PetHappy },
            { "Pet HasTarget", playerReader.PetHasTarget },
            { "Mounted", bits.IsMounted },
            
            // Auto Spell
            { "AutoAttacking", bits.SpellOn_AutoAttack },
            { "Shooting", bits.SpellOn_Shoot },
            { "AutoShot", bits.SpellOn_AutoShot },
            
            // Temporary Enchants
            { "HasMainHandEnchant", bits.HasMainHandTempEnchant },
            { "HasOffHandEnchant", bits.HasOffHandTempEnchant },
            
            // Equipment - Bag
            { "Items Broken", bits.ItemsAreBroken },
            { "BagFull", bagReader.BagsFull },
            { "BagGreyItem", bagReader.AnyGreyItem },
            { "HasRangedWeapon", equipmentReader.HasRanged },
            { "HasAmmo", bits.HasAmmo },

            { "Casting", playerReader.IsCasting },
            { "HasTarget", bits.HasTarget },
            { "TargetHostile", bits.TargetCanBeHostile },
            { "TargetAlive", bits.TargetAlive },

            // General Buff Condition
            { Food, playerBuffs.Food },
            { Drink, playerBuffs.Drink },
            { "Mana Regeneration", playerBuffs.Mana_Regeneration },
            { "Well Fed", playerBuffs.Well_Fed },
            { "Clearcasting", playerBuffs.Clearcasting },

            // Player Affected
            { Swimming, bits.IsSwimming },
            { Falling, bits.IsFalling },
            { "Dead", bits.IsDead },

            //Priest
            { "Fortitude", playerBuffs.Fortitude },
            { "InnerFire", playerBuffs.InnerFire },
            { "Divine Spirit", playerBuffs.DivineSpirit },
            { "Renew", playerBuffs.Renew },
            { "Shield", playerBuffs.Shield },

            // Druid
            { "Mark of the Wild", playerBuffs.MarkOfTheWild },
            { "Thorns", playerBuffs.Thorns },
            { "TigersFury", playerBuffs.TigersFury },
            { "Prowl", playerBuffs.Prowl },
            { "Rejuvenation", playerBuffs.Rejuvenation },
            { "Regrowth", playerBuffs.Regrowth },
            { "Omen of Clarity", playerBuffs.OmenOfClarity },

            // Paladin
            { "Seal of Righteousness", playerBuffs.SealofRighteousness },
            { "Seal of the Crusader", playerBuffs.SealoftheCrusader },
            { "Seal of Command", playerBuffs.SealofCommand },
            { "Seal of Wisdom", playerBuffs.SealofWisdom },
            { "Seal of Light", playerBuffs.SealofLight },
            { "Seal of Blood", playerBuffs.SealofBlood },
            { "Seal of Vengeance", playerBuffs.SealofVengeance },
            { "Blessing of Might", playerBuffs.BlessingofMight },
            { "Blessing of Protection", playerBuffs.BlessingofProtection },
            { "Blessing of Wisdom", playerBuffs.BlessingofWisdom },
            { "Blessing of Kings", playerBuffs.BlessingofKings },
            { "Blessing of Salvation", playerBuffs.BlessingofSalvation },
            { "Blessing of Sanctuary", playerBuffs.BlessingofSanctuary },
            { "Blessing of Light", playerBuffs.BlessingofLight },
            { "Righteous Fury", playerBuffs.RighteousFury },
            { "Divine Protection", playerBuffs.DivineProtection },
            { "Avenging Wrath", playerBuffs.AvengingWrath },
            { "Holy Shield", playerBuffs.HolyShield },
            { "Divine Shield", playerBuffs.DivineShield },

            // Mage
            { "Frost Armor", playerBuffs.FrostArmor },
            { "Ice Armor", playerBuffs.FrostArmor },
            { "Molten Armor", playerBuffs.FrostArmor },
            { "Mage Armor", playerBuffs.FrostArmor },
            { "Arcane Intellect", playerBuffs.ArcaneIntellect },
            { "Ice Barrier", playerBuffs.IceBarrier },
            { "Ward", playerBuffs.Ward },
            { "Fire Power", playerBuffs.FirePower },
            { "Mana Shield", playerBuffs.ManaShield },
            { "Presence of Mind", playerBuffs.PresenceOfMind },
            { "Arcane Power", playerBuffs.ArcanePower },
            
            // Rogue
            { "Slice and Dice", playerBuffs.SliceAndDice },
            { "Stealth", playerBuffs.Stealth },
            
            // Warrior
            { "Battle Shout", playerBuffs.BattleShout },
            { "Bloodrage", playerBuffs.Bloodrage },
            
            // Warlock
            { "Demon Skin", playerBuffs.Demon },
            { "Demon Armor", playerBuffs.Demon },
            { "Soul Link", playerBuffs.SoulLink },
            { "Soulstone Resurrection", playerBuffs.SoulstoneResurrection },
            { "Shadow Trance", playerBuffs.ShadowTrance },
            { "Fel Armor", playerBuffs.FelArmor },
            { "Fel Domination", playerBuffs.FelDomination },
            { "Demonic Sacrifice", playerBuffs.DemonicSacrifice },
            
            // Shaman
            { "Lightning Shield", playerBuffs.LightningShield },
            { "Water Shield", playerBuffs.WaterShield },
            { "Shamanistic Focus", playerBuffs.ShamanisticFocus },
            { "Focused", playerBuffs.ShamanisticFocus },
            { "Stoneskin", playerBuffs.Stoneskin },
            
            //Hunter
            { "Aspect of the Cheetah", playerBuffs.AspectoftheCheetah },
            { "Aspect of the Pack", playerBuffs.AspectofthePack },
            { "Aspect of the Hawk", playerBuffs.AspectoftheHawk },
            { "Aspect of the Monkey", playerBuffs.AspectoftheMonkey },
            { "Aspect of the Viper", playerBuffs.AspectoftheViper },
            { "Rapid Fire", playerBuffs.RapidFire },
            { "Quick Shots", playerBuffs.QuickShots },
            { "Trueshot Aura", playerBuffs.TrueshotAura },
            { "Aspect of the Dragonhawk", playerBuffs.AspectoftheDragonhawk },
            { "Lock and Load", playerBuffs.LockandLoad },

            //Death Knight
            { "Blood Tap", playerBuffs.BloodTap },
            { "Horn of Winter", playerBuffs.HornofWinter },
            { "Icebound Fortitude", playerBuffs.IceboundFortitude },
            { "Path of Frost", playerBuffs.PathofFrost },
            { "Anti-Magic Shell", playerBuffs.AntiMagicShell },
            { "Army of the Dead", playerBuffs.ArmyoftheDead },
            { "Vampiric Blood", playerBuffs.VampiricBlood },
            { "Dancing Rune Weapon", playerBuffs.DancingRuneWeapon },
            { "Unbreakable Armor", playerBuffs.UnbreakableArmor },
            { "Bone Shield", playerBuffs.BoneShield },
            { "Summon Gargoyle", playerBuffs.SummonGargoyle },
            { "Freezing Fog", playerBuffs.FreezingFog },

            // Debuff Section
            // Druid Debuff
            { "Demoralizing Roar", targetDebuffs.Roar },
            { "Faerie Fire", targetDebuffs.FaerieFire },
            { "Rip", targetDebuffs.Rip },
            { "Moonfire", targetDebuffs.Moonfire },
            { "Entangling Roots", targetDebuffs.EntanglingRoots },
            { "Rake", targetDebuffs.Rake },
            
            // Paladin Debuff
            { "Judgement of the Crusader", targetDebuffs.JudgementoftheCrusader },
            { "Hammer of Justice", targetDebuffs.HammerOfJustice },
            { "Judgement of Wisdom", targetDebuffs.JudgementofWisdom },
            { "Judgement of Light", targetDebuffs.JudgementofLight },
            { "Judgement of Justice", targetDebuffs.JudgementofJustice },
            { "Judgement of Any", targetDebuffs.JudgementAny },

            // Warrior Debuff
            { "Rend", targetDebuffs.Rend },
            { "Thunder Clap", targetDebuffs.ThunderClap },
            { "Hamstring", targetDebuffs.Hamstring },
            { "Charge Stun", targetDebuffs.ChargeStun },
            
            // Priest Debuff
            { "Shadow Word: Pain", targetDebuffs.ShadowWordPain },
            
            // Mage Debuff
            { "Frostbite", targetDebuffs.Frostbite },
            { "Slow", targetDebuffs.Slow },
            
            // Warlock Debuff
            { "Curse of Weakness", targetDebuffs.Curseof },
            { "Curse of Elements", targetDebuffs.Curseof },
            { "Curse of Recklessness", targetDebuffs.Curseof },
            { "Curse of Shadow", targetDebuffs.Curseof },
            { "Curse of Agony", targetDebuffs.Curseof },
            { "Curse of", targetDebuffs.Curseof },
            { "Corruption", targetDebuffs.Corruption },
            { "Immolate", targetDebuffs.Immolate },
            { "Siphon Life", targetDebuffs.SiphonLife },
            
            // Hunter Debuff
            { "Serpent Sting", targetDebuffs.SerpentSting },
            { "Hunter's Mark", targetDebuffs.HuntersMark },
            { "Viper Sting", targetDebuffs.ViperSting },
            { "Explosive Shot", targetDebuffs.ExplosiveShot },
            { "Black Arrow", targetDebuffs.BlackArrow },

            // Death Knight Debuff
            { "Blood Plague", targetDebuffs.BloodPlague },
            { "Frost Fever", targetDebuffs.FrostFever },
            { "Strangulate", targetDebuffs.Strangulate },
            { "Chains of Ice", targetDebuffs.ChainsofIce },
        };

        intVariables = new Dictionary<string, Func<int>>
        {
            { HealthP, playerReader.HealthPercent },
            { "TargetHealth%", playerReader.TargetHealthPercentage },
            { "PetHealth%", playerReader.PetHealthPercentage },
            { ManaP, playerReader.ManaPercentage },
            { "Mana", playerReader.ManaCurrent },
            { "Energy", playerReader.PTCurrent },
            { "Rage", playerReader.PTCurrent },
            { "RunicPower", playerReader.PTCurrent },
            { "BloodRune", playerReader.BloodRune },
            { "FrostRune", playerReader.FrostRune },
            { "UnholyRune", playerReader.UnholyRune },
            { "TotalRune", playerReader.MaxRune },
            { "Combo Point", playerReader.ComboPoints },
            { "Durability%", playerReader.AvgEquipDurability },
            { "BagCount", bagReader.BagItemCount },
            { "FoodCount", bagReader.FoodItemCount },
            { "DrinkCount", bagReader.DrinkItemCount },
            { "MobCount", combatLog.DamageTakenCount },
            { "MinRange", playerReader.MinRange },
            { "MaxRange", playerReader.MaxRange },
            { "LastAutoShotMs", playerReader.AutoShot.ElapsedMs },
            { "LastMainHandMs", playerReader.MainHandSwing.ElapsedMs },
            { "LastTargetDodgeMs", () => Math.Max(0, combatLog.TargetDodge.ElapsedMs()) },
            //"CD_{KeyAction.Name}
            //"Cost_{KeyAction.Name}"
            //"Buff_{textureId}"
            //"Debuff_{textureId}"
            //"TBuff_{textureId}"
            { "MainHandSpeed", playerReader.MainHandSpeedMs },
            { "MainHandSwing", () => Math.Clamp(playerReader.MainHandSwing.ElapsedMs() - playerReader.MainHandSpeedMs(), -playerReader.MainHandSpeedMs(), 0) },
            { "RangedSpeed", playerReader.RangedSpeedMs },
            { "RangedSwing", () => Math.Clamp(playerReader.AutoShot.ElapsedMs() - playerReader.RangedSpeedMs(), -playerReader.RangedSpeedMs(), 0) },
            { "CurGCD", playerReader.GCD._Value },
            { "GCD", CastingHandler._GCD },

            // Session Stat
            { "Deaths", sessionStat._Deaths },
            { "Kills", sessionStat._Kills },
        };
    }

    public void AddSequenceRange(KeyActions keyActions)
    {
        int sizeBefore = this.keyActions.Length;
        Array.Resize(ref this.keyActions, this.keyActions.Length + keyActions.Sequence.Length);
        Array.ConstrainedCopy(keyActions.Sequence, 0, this.keyActions, sizeBefore, keyActions.Sequence.Length);
    }

    public void InitialiseRequirements(KeyAction item)
    {
        if (item.Name is Drink or Food)
            AddConsumableRequirement(item);

        InitPerKeyActionRequirements(item);

        List<Requirement> requirements = new();

        foreach (string requirement in CollectionsMarshal.AsSpan(item.Requirements))
        {
            List<string> expressions = InfixToPostfix.Convert(requirement);
            Stack<Requirement> stack = new();
            foreach (string expr in CollectionsMarshal.AsSpan(expressions))
            {
                if (expr.Contains(Requirement.SymbolAnd))
                {
                    Requirement a = stack.Pop();
                    Requirement b = stack.Pop();
                    b.And(a);

                    stack.Push(b);
                }
                else if (expr.Contains(Requirement.SymbolOr))
                {
                    Requirement a = stack.Pop();
                    Requirement b = stack.Pop();
                    b.Or(a);

                    stack.Push(b);
                }
                else
                {
                    string trim = expr.Trim();
                    if (string.IsNullOrEmpty(trim))
                    {
                        continue;
                    }

                    stack.Push(CreateRequirement(item.Name, trim));
                }
            }

            requirements.Add(stack.Pop());
        }

        AddMinRequirement(requirements, item, buffs);
        AddTargetIsCastingRequirement(requirements, item, playerReader);

        if (item.WhenUsable && !string.IsNullOrEmpty(item.Key))
        {
            requirements.Add(CreateActionUsableRequirement(item, playerReader, usableAction));

            if (item.Slot > 0)
                requirements.Add(CreateActionNotInGameCooldown(item, playerReader, intVariables));
        }

        AddCooldownRequirement(requirements, item);
        AddChargeRequirement(requirements, item);

        AddSpellSchoolRequirement(requirements, item, playerReader, immunityBlacklist);

        item.RequirementsRuntime = requirements.ToArray();

        if (item.Interrupts.Count > 0)
            InitialiseInterrupts(item);
    }

    private void InitialiseInterrupts(KeyAction item)
    {
        List<Requirement> requirements = new();

        foreach (string requirement in CollectionsMarshal.AsSpan(item.Interrupts))
        {
            List<string> expressions = InfixToPostfix.Convert(requirement);
            Stack<Requirement> stack = new();
            foreach (string expr in CollectionsMarshal.AsSpan(expressions))
            {
                if (expr.Contains(Requirement.SymbolAnd))
                {
                    Requirement a = stack.Pop();
                    Requirement b = stack.Pop();
                    b.And(a);

                    stack.Push(b);
                }
                else if (expr.Contains(Requirement.SymbolOr))
                {
                    Requirement a = stack.Pop();
                    Requirement b = stack.Pop();
                    b.Or(a);

                    stack.Push(b);
                }
                else
                {
                    string trim = expr.Trim();
                    if (string.IsNullOrEmpty(trim))
                    {
                        continue;
                    }

                    stack.Push(CreateRequirement(item.Name, trim));
                }
            }

            requirements.Add(stack.Pop());
        }

        item.InterruptsRuntime = requirements.ToArray();
    }

    public void InitUserDefinedIntVariables(Dictionary<string, int> intKeyValues,
        AuraTimeReader<IPlayerBuffTimeReader> playerBuffTimeReader,
        AuraTimeReader<ITargetDebuffTimeReader> targetDebuffTimeReader,
        AuraTimeReader<ITargetBuffTimeReader> targetBuffTimeReader)
    {
        foreach ((string key, int value) in intKeyValues)
        {
            int f() => value;

            if (!intVariables.TryAdd(key, f))
            {
                throw new Exception($"Unable to add user defined variable to values. [{key} -> {value}]");
            }
            else
            {
                if (key.StartsWith("Buff_"))
                {
                    int l() => playerBuffTimeReader.GetRemainingTimeMs(value);
                    intVariables.TryAdd($"{value}", l);
                }
                else if (key.StartsWith("Debuff_"))
                {
                    int l() => targetDebuffTimeReader.GetRemainingTimeMs(value);
                    intVariables.TryAdd($"{value}", l);
                }
                else if (key.StartsWith("TBuff_"))
                {
                    int l() => targetBuffTimeReader.GetRemainingTimeMs(value);
                    intVariables.TryAdd($"{value}", l);
                }

                LogUserDefinedValue(logger, nameof(RequirementFactory), key, value);
            }
        }
    }

    public void InitDynamicBindings(KeyAction item)
    {
        if (string.IsNullOrEmpty(item.Name) || item.Slot == 0) return;

        BindCooldown(item, cooldownReader);
        BindMinCost(item, costReader);
    }

    private void BindCooldown(KeyAction item, ActionBarCooldownReader reader)
    {
        string key = $"CD_{item.Name}";
        if (!intVariables.ContainsKey(key))
        {
            intVariables.Add(key, () => reader.Get(item));
        }
    }

    private void BindMinCost(KeyAction item, ActionBarCostReader reader)
    {
        string key = $"Cost_{item.Name}";
        if (!intVariables.ContainsKey(key))
        {
            intVariables.Add(key,
                () => reader.Get(item).Cost);
        }
    }

    private void InitPerKeyActionRequirements(KeyAction item)
    {
        InitPerKeyActionRequirementByKey(item, "CD");
        InitPerKeyActionRequirementByKey(item, "Cost");
    }

    private void InitPerKeyActionRequirementByKey(KeyAction item, string prefixKey)
    {
        string key = $"{prefixKey}_{item.Name}";
        intVariables.Remove(prefixKey);

        if (intVariables.TryGetValue(key, out Func<int>? func))
            intVariables.Add(prefixKey, func);
    }

    private void AddTargetIsCastingRequirement(List<Requirement> list, KeyAction item, PlayerReader playerReader)
    {
        if (item.UseWhenTargetIsCasting == null)
            return;

        bool f() => playerReader.IsTargetCasting() == item.UseWhenTargetIsCasting.Value;
        string l() => "Target casting";
        list.Add(new Requirement
        {
            HasRequirement = f,
            LogMessage = l
        });
    }

    private void AddMinRequirement(List<Requirement> list, KeyAction item, BuffStatus buffs)
    {
        AddMinPowerTypeRequirement(list, PowerType.Mana, item, playerReader, buffs);
        AddMinPowerTypeRequirement(list, PowerType.Rage, item, playerReader, buffs);
        AddMinPowerTypeRequirement(list, PowerType.Energy, item, playerReader, buffs);
        if (playerReader.Class == UnitClass.DeathKnight)
        {
            AddMinPowerTypeRequirement(list, PowerType.RunicPower, item, playerReader, buffs);
            AddMinPowerTypeRequirement(list, PowerType.RuneBlood, item, playerReader, buffs);
            AddMinPowerTypeRequirement(list, PowerType.RuneFrost, item, playerReader, buffs);
            AddMinPowerTypeRequirement(list, PowerType.RuneUnholy, item, playerReader, buffs);
        }
        AddMinComboPointsRequirement(list, item, playerReader);
    }

    private void AddMinPowerTypeRequirement(List<Requirement> list, PowerType type,
        KeyAction keyAction, PlayerReader playerReader, BuffStatus buffs)
    {
        switch (type)
        {
            case PowerType.Mana:
                bool fmana() => playerReader.ManaCurrent() >= keyAction.MinMana || buffs.Clearcasting();
                string smana() => $"{type.ToStringF()} {playerReader.ManaCurrent()} >= {keyAction.MinMana}";
                list.Add(new Requirement
                {
                    HasRequirement = fmana,
                    LogMessage = smana,
                    VisibleIfHasRequirement = keyAction.MinMana > 0
                });
                break;
            case PowerType.Rage:
                bool frage() => playerReader.PTCurrent() >= keyAction.MinRage || buffs.Clearcasting();
                string srage() => $"{type.ToStringF()} {playerReader.PTCurrent()} >= {keyAction.MinRage}";
                list.Add(new Requirement
                {
                    HasRequirement = frage,
                    LogMessage = srage,
                    VisibleIfHasRequirement = keyAction.MinRage > 0
                });
                break;
            case PowerType.Energy:
                bool fenergy() => playerReader.PTCurrent() >= keyAction.MinEnergy || buffs.Clearcasting();
                string senergy() => $"{type.ToStringF()} {playerReader.PTCurrent()} >= {keyAction.MinEnergy}";
                list.Add(new Requirement
                {
                    HasRequirement = fenergy,
                    LogMessage = senergy,
                    VisibleIfHasRequirement = keyAction.MinEnergy > 0
                });
                break;
            case PowerType.RunicPower:
                bool frunicpower() => playerReader.PTCurrent() >= keyAction.MinRunicPower;
                string srunicpower() => $"{type.ToStringF()} {playerReader.PTCurrent()} >= {keyAction.MinRunicPower}";
                list.Add(new Requirement
                {
                    HasRequirement = frunicpower,
                    LogMessage = srunicpower,
                    VisibleIfHasRequirement = keyAction.MinRunicPower > 0
                });
                break;
            case PowerType.RuneBlood:
                bool fbloodrune() => playerReader.BloodRune() >= keyAction.MinRuneBlood;
                string sbloodrune() => $"{type.ToStringF()} {playerReader.BloodRune()} >= {keyAction.MinRuneBlood}";
                list.Add(new Requirement
                {
                    HasRequirement = fbloodrune,
                    LogMessage = sbloodrune,
                    VisibleIfHasRequirement = keyAction.MinRuneBlood > 0
                });
                break;
            case PowerType.RuneFrost:
                bool ffrostrune() => playerReader.FrostRune() >= keyAction.MinRuneFrost;
                string sfrostrune() => $"{type.ToStringF()} {playerReader.FrostRune()} >= {keyAction.MinRuneFrost}";
                list.Add(new Requirement
                {
                    HasRequirement = ffrostrune,
                    LogMessage = sfrostrune,
                    VisibleIfHasRequirement = keyAction.MinRuneFrost > 0
                });
                break;
            case PowerType.RuneUnholy:
                bool funholyrune() => playerReader.UnholyRune() >= keyAction.MinRuneUnholy;
                string sunholyrune() => $"{type.ToStringF()} {playerReader.UnholyRune()} >= {keyAction.MinRuneUnholy}";
                list.Add(new Requirement
                {
                    HasRequirement = funholyrune,
                    LogMessage = sunholyrune,
                    VisibleIfHasRequirement = keyAction.MinRuneUnholy > 0
                });
                break;
        }
    }

    private void AddMinComboPointsRequirement(List<Requirement> list, KeyAction item, PlayerReader playerReader)
    {
        if (item.MinComboPoints <= 0)
            return;

        bool f() => playerReader.ComboPoints() >= item.MinComboPoints;
        string s() => $"Combo point {playerReader.ComboPoints()} >= {item.MinComboPoints}";
        list.Add(new Requirement
        {
            HasRequirement = f,
            LogMessage = s
        });
    }

    private static void AddCooldownRequirement(List<Requirement> list, KeyAction item)
    {
        if (item.Cooldown <= 0)
            return;

        bool f() => item.GetRemainingCooldown() == 0;
        string s() => $"Cooldown {item.GetRemainingCooldown() / 1000:F1}";
        list.Add(new Requirement
        {
            HasRequirement = f,
            LogMessage = s,
            VisibleIfHasRequirement = false
        });
    }

    private static void AddChargeRequirement(List<Requirement> list, KeyAction item)
    {
        if (item.BaseAction || item.Charge < 1)
            return;

        bool f() => item.GetChargeRemaining() != 0;
        string s() => $"Charge {item.GetChargeRemaining()}";
        list.Add(new Requirement
        {
            HasRequirement = f,
            LogMessage = s,
            VisibleIfHasRequirement = false
        });
    }

    private static void AddConsumableRequirement(KeyAction item)
    {
        item.BeforeCastStop = true;
        item.WhenUsable = true;
        item.AfterCastWaitBuff = true;
        item.Item = true;

        item.Requirements.Add($"!{item.Name}");
        item.Requirements.Add($"!{Swimming}");
        item.Requirements.Add($"!{Falling}");
    }

    private void AddSpellSchoolRequirement(List<Requirement> list, KeyAction item,
        PlayerReader playerReader, Dictionary<int, SchoolMask[]> immunityBlacklist)
    {
        if (item.School == SchoolMask.None)
            return;

        bool f() =>
            !immunityBlacklist.TryGetValue(playerReader.TargetId, out SchoolMask[]? immuneAgaints) ||
            !immuneAgaints.Contains(item.School);

        string s() => item.School.ToStringF();
        list.Add(new Requirement
        {
            HasRequirement = f,
            LogMessage = s,
            VisibleIfHasRequirement = false
        });
    }


    public Requirement CreateRequirement(string name, string requirement)
    {
        LogProcessingRequirement(logger, name, requirement);

        string? negated = negateKeywords.FirstOrDefault(requirement.StartsWith);
        if (!string.IsNullOrEmpty(negated))
        {
            requirement = requirement[negated.Length..];
        }

        string? key = requirementMap.Keys.FirstOrDefault(requirement.Contains);
        if (!string.IsNullOrEmpty(key))
        {
            Requirement req = requirementMap[key](requirement);
            if (negated != null)
            {
                req.Negate(negated);
            }
            return req;
        }

        if (boolVariables.ContainsKey(requirement))
        {
            string s() => requirement;
            Requirement req = new()
            {
                HasRequirement = boolVariables[requirement],
                LogMessage = s
            };

            if (negated != null)
            {
                req.Negate(negated);
            }
            return req;
        }

        LogUnknownRequirement(logger, requirement, string.Join(", ", boolVariables.Keys));
        return new Requirement
        {
            LogMessage = () => $"UNKNOWN REQUIREMENT! {requirement}"
        };
    }

    private Requirement CreateActionUsableRequirement(KeyAction item, PlayerReader playerReader, ActionBarBits<IUsableAction> usableAction)
    {
        bool CanDoFormChangeMinMana()
        {
            return playerReader.ManaCurrent() >= item.FormCost() + item.MinMana;
        }

        bool f() =>
            !item.HasFormRequirement
            ? usableAction.Is(item)
            : (playerReader.Form == item.FormEnum && usableAction.Is(item)) ||
            (playerReader.Form != item.FormEnum && CanDoFormChangeMinMana());

        string s() =>
            !item.HasFormRequirement
            ? "Usable"
            : (playerReader.Form != item.FormEnum && CanDoFormChangeMinMana()) ? "Usable after Form change" :
            (playerReader.Form == item.FormEnum && usableAction.Is(item)) ? "Usable current Form" : "not Usable current Form";

        return new Requirement
        {
            HasRequirement = f,
            LogMessage = s
        };
    }

    private Requirement CreateActionNotInGameCooldown(KeyAction item, PlayerReader playerReader, Dictionary<string, Func<int>> intVariables)
    {
        string key = $"CD_{item.Name}";
        bool f() => UsableGCD(key, playerReader, intVariables);
        string s() => $"CD {intVariables[key]() / 1000f:F1}";

        return new Requirement
        {
            HasRequirement = f,
            VisibleIfHasRequirement = false,
            LogMessage = s
        };
    }

    private static bool UsableGCD(string key, PlayerReader playerReader, Dictionary<string, Func<int>> intVariables)
    {
        return intVariables[key]() <= CastingHandler.SPELL_QUEUE - playerReader.NetworkLatency.Value;
    }

    private Requirement CreateTargetCastingSpell(string requirement)
    {
        return create(requirement, playerReader);
        static Requirement create(string requirement, PlayerReader playerReader)
        {
            ReadOnlySpan<char> span = requirement;
            int sep1 = span.IndexOf(SEP1);
            // 'TargetCastingSpell'
            if (sep1 == -1)
            {
                return new Requirement
                {
                    HasRequirement = playerReader.IsTargetCasting,
                    LogMessage = () => "Target casting"
                };
            }

            // 'TargetCastingSpell:_1_?,_n_'
            string[] spellsPart = span[(sep1 + 1)..].ToString().Split(SEP2);
            int[] spellIds = spellsPart.Select(int.Parse).ToArray();

            bool f() => spellIds.Contains(playerReader.SpellBeingCastByTarget);
            string s() => $"Target casts {playerReader.SpellBeingCastByTarget} ∈ [{string.Join(SEP2, spellIds)}]";
            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateForm(string requirement)
    {
        return create(requirement, playerReader);
        static Requirement create(string requirement, PlayerReader playerReader)
        {
            // 'Form:_FORM_'
            ReadOnlySpan<char> span = requirement;
            int sep = span.IndexOf(SEP1);
            Form form = Enum.Parse<Form>(span[(sep + 1)..]);

            bool f() => playerReader.Form == form;
            string s() => playerReader.Form.ToStringF();

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateRace(string requirement)
    {
        return create(requirement, playerReader);
        static Requirement create(string requirement, PlayerReader playerReader)
        {
            // 'Race:_RACE_'
            ReadOnlySpan<char> span = requirement;
            int sep = span.IndexOf(SEP1);
            UnitRace race = Enum.Parse<UnitRace>(span[(sep + 1)..]);

            bool f() => playerReader.Race == race;
            string s() => playerReader.Race.ToStringF();

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateSpell(string requirement)
    {
        return create(requirement, spellBookReader);
        static Requirement create(string requirement, SpellBookReader spellBookReader)
        {
            // 'Spell:_NAME_OR_ID_'
            ReadOnlySpan<char> span = requirement;
            int sep = span.IndexOf(SEP1);
            string name = span[(sep + 1)..].Trim().ToString();

            int id;
            if (int.TryParse(name, out id) &&
                spellBookReader.TryGetValue(id, out Spell spell))
            {
                name = $"{spell.Name}({id})";
            }
            else
            {
                id = spellBookReader.GetId(name);
            }

            bool f() => spellBookReader.Has(id);
            string s() => $"Spell {name}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateTalent(string requirement)
    {
        return create(requirement, talentReader);
        static Requirement create(string requirement, TalentReader talentReader)
        {
            // 'Talent:_NAME_?:_RANK_'
            ReadOnlySpan<char> span = requirement;

            int firstSep = span.IndexOf(SEP1);
            int lastSep = span.LastIndexOf(SEP1);

            int rank = 1;
            if (firstSep != lastSep)
            {
                rank = int.Parse(span[(lastSep + 1)..]);
            }
            else
            {
                lastSep = span.Length;
            }

            string name = span[(firstSep + 1)..lastSep].ToString();

            bool f() => talentReader.HasTalent(name, rank);
            string s() => rank == 1 ? $"Talent {name}" : $"Talent {name} (Rank {rank})";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateTrigger(string requirement)
    {
        return create(requirement, playerReader);
        static Requirement create(string requirement, PlayerReader playerReader)
        {
            // 'Trigger:_BIT_NUM_?:_TEXT_'
            ReadOnlySpan<char> span = requirement;
            int firstSep = span.IndexOf(SEP1);
            int lastSep = span.LastIndexOf(SEP1);

            string text = string.Empty;
            if (firstSep != lastSep)
            {
                text = span[(lastSep + 1)..].ToString();
            }
            else
            {
                lastSep = span.Length;
            }

            int bitNum = int.Parse(span[(firstSep + 1)..lastSep]);
            int bitMask = Mask.M[bitNum];

            bool f() => playerReader.CustomTrigger1[bitMask];
            string s() => $"Trigger({bitNum}) {text}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateNpcId(string requirement)
    {
        return create(requirement, playerReader, intVariables, creatureDb);
        static Requirement create(string requirement, PlayerReader playerReader,
            Dictionary<string, Func<int>> intVariables, CreatureDB creatureDb)
        {
            // 'npcID:_INTVARIABLE_OR_ID_'
            ReadOnlySpan<char> span = requirement;
            int sep = span.IndexOf(SEP1);
            ReadOnlySpan<char> name_or_id = span[(sep + 1)..];

            int npcId;
            if (intVariables.TryGetValue(name_or_id.ToString(), out Func<int>? value))
                npcId = value();
            else
                npcId = int.Parse(name_or_id);

            if (!creatureDb.Entries.TryGetValue(npcId, out string? npcName))
            {
                npcName = string.Empty;
            }

            bool f() => playerReader.TargetId == npcId;
            string s() => $"TargetID {npcName}({npcId})";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateBagItem(string requirement)
    {
        return create(requirement, bagReader, intVariables, itemDb);
        static Requirement create(string requirement, BagReader bagReader,
            Dictionary<string, Func<int>> intVariables, ItemDB itemDb)
        {
            // 'BagItem:_INTVARIABLE_OR_ID_?:_COUNT_'
            ReadOnlySpan<char> span = requirement;

            int firstSep = span.IndexOf(SEP1);
            int lastSep = span.LastIndexOf(SEP1);

            int count = 1;
            if (firstSep != lastSep)
            {
                count = int.Parse(span[(lastSep + 1)..]);
            }
            else
            {
                lastSep = span.Length;
            }

            ReadOnlySpan<char> name_or_id = span[(firstSep + 1)..lastSep];

            int itemId;
            if (intVariables.TryGetValue(name_or_id.ToString(), out Func<int>? value))
                itemId = value();
            else
                itemId = int.Parse(name_or_id);

            string itemName = string.Empty;
            if (itemDb.Items.TryGetValue(itemId, out Item item))
            {
                itemName = item.Name;
            }

            bool f() => bagReader.ItemCount(itemId) >= count;
            string s() => count == 1 ? $"in bag {itemName}({itemId})" : $"{itemName}({itemId}) count >= {count}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateSpellInRange(string requirement)
    {
        return create(requirement, playerReader.SpellInRange);
        static Requirement create(string requirement, SpellInRange range)
        {
            // 'SpellInRange:_BIT_NUM_'
            ReadOnlySpan<char> span = requirement;
            int sep = span.IndexOf(SEP1);
            int bitNum = int.Parse(span[(sep + 1)..]);
            int bitMask = Mask.M[bitNum];

            bool f() => range[bitMask];
            string s() => $"SpellInRange {bitNum}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }
    }

    private Requirement CreateUsable(string requirement)
    {
        // 'Usable:_KeyAction_Name_'
        ReadOnlySpan<char> span = requirement;
        int sep = span.IndexOf(SEP1);
        ReadOnlySpan<char> name = span[(sep + 1)..].Trim();

        KeyAction? keyAction = null;
        for (int i = 0; i < keyActions.Length; i++)
        {
            KeyAction test = keyActions[i];
            if (name.SequenceEqual(test.Name))
            {
                keyAction = test;
                break;
            }
        }

        if (keyAction == null)
            throw new InvalidOperationException($"'{requirement}' related named '{name}' {nameof(KeyAction)} not found!");

        return CreateActionUsableRequirement(keyAction, playerReader, usableAction);
    }


    private Requirement CreateGreaterThen(string requirement)
    {
        return CreateArithmeticRequirement(greaterThen, requirement, intVariables);
    }

    private Requirement CreateLesserThen(string requirement)
    {
        return CreateArithmeticRequirement(lessThen, requirement, intVariables);
    }

    private Requirement CreateGreaterOrEquals(string requirement)
    {
        return CreateArithmeticRequirement(greaterThenOrEqual, requirement, intVariables);
    }

    private Requirement CreateLesserOrEquals(string requirement)
    {
        return CreateArithmeticRequirement(lessThenOrEqual, requirement, intVariables);
    }

    private Requirement CreateEquals(string requirement)
    {
        return CreateArithmeticRequirement(equals, requirement, intVariables);
    }

    private Requirement CreateModulo(string requirement)
    {
        return CreateArithmeticRequirement(modulo, requirement, intVariables);
    }

    private Requirement CreateArithmeticRequirement(string symbol, string requirement, Dictionary<string, Func<int>> intVariables)
    {
        ReadOnlySpan<char> span = requirement;
        int sep = span.IndexOf(symbol);

        string key = span[..sep].Trim().ToString();
        ReadOnlySpan<char> varOrConst = span[(sep + symbol.Length)..];

        if (!intVariables.TryGetValue(key, out Func<int>? aliasOrKey))
        {
            LogUnknownRequirement(logger, requirement, string.Join(", ", intVariables.Keys));
            throw new ArgumentOutOfRangeException(requirement);
        }

        string display = key;

        string aliasKey = aliasOrKey().ToString();
        if (intVariables.ContainsKey(aliasKey))
        {
            key = aliasKey;
        }

        Func<int> lValue = intVariables[key];

        Func<int> rValue;
        if (int.TryParse(varOrConst, out int constValue))
        {
            int _constValue() => constValue;
            rValue = _constValue;
        }
        else
        {
            rValue = intVariables.TryGetValue(varOrConst.Trim().ToString(), out Func<int>? v)
                ? v
                : throw new ArgumentOutOfRangeException(requirement);
        }

        string msg() => $"{display} {lValue()} {symbol} {rValue()}";
        switch (symbol)
        {
            case modulo:
                bool m() => lValue() % rValue() == 0;
                return new Requirement { HasRequirement = m, LogMessage = msg };
            case equals:
                bool e() => lValue() == rValue();
                return new Requirement { HasRequirement = e, LogMessage = msg };
            case greaterThen:
                bool g() => lValue() > rValue();
                return new Requirement { HasRequirement = g, LogMessage = msg };
            case lessThen:
                bool l() => lValue() < rValue();
                return new Requirement { HasRequirement = l, LogMessage = msg };
            case greaterThenOrEqual:
                bool ge() => lValue() >= rValue();
                return new Requirement { HasRequirement = ge, LogMessage = msg };
            case lessThenOrEqual:
                bool le() => lValue() <= rValue();
                return new Requirement { HasRequirement = le, LogMessage = msg };
            default:
                throw new ArgumentOutOfRangeException(requirement);
        };
    }

    #region Logging

    [LoggerMessage(
        EventId = 0017,
        Level = LogLevel.Information,
        Message = "[{typeName}] Defined int variable [{key} -> {value}]")]
    static partial void LogUserDefinedValue(ILogger logger, string typeName, string key, int value);

    [LoggerMessage(
        EventId = 0018,
        Level = LogLevel.Information,
        Message = "[{name,-15}] Requirement: \"{requirement}\"")]
    static partial void LogProcessingRequirement(ILogger logger, string name, string requirement);

    [LoggerMessage(
        EventId = 0019,
        Level = LogLevel.Error,
        Message = "UNKNOWN REQUIREMENT! {requirement}: try one of: {available}")]
    static partial void LogUnknownRequirement(ILogger logger, string requirement, string available);

    #endregion
}