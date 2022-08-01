using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Database;
using SharedLib;
using SharedLib.NpcFinder;
using Core.Goals;

namespace Core
{
    public partial class RequirementFactory
    {
        private readonly ILogger logger;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly BagReader bagReader;
        private readonly EquipmentReader equipmentReader;
        private readonly SpellBookReader spellBookReader;
        private readonly TalentReader talentReader;
        private readonly CreatureDB creatureDb;
        private readonly ItemDB itemDb;
        private readonly AuraTimeReader playerBuffTimeReader;
        private readonly AuraTimeReader targetDebuffTimeReader;

        private KeyAction[] keyActions = Array.Empty<KeyAction>();

        private readonly Dictionary<int, SchoolMask[]> immunityBlacklist;

        private readonly List<string> negateKeywords = new()
        {
            "not ",
            "!"
        };

        private readonly Dictionary<string, Func<int>> intVariables;

        private readonly Dictionary<string, Func<bool>> boolVariables;

        private readonly Dictionary<string, Func<string, Requirement>> requirementMap;

        public const string AddVisible = "AddVisible";
        public const string Drink = "Drink";
        public const string Food = "Food";

        public RequirementFactory(ILogger logger, AddonReader addonReader,
            NpcNameFinder npcNameFinder, Dictionary<int, SchoolMask[]> immunityBlacklist)
        {
            this.logger = logger;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.bagReader = addonReader.BagReader;
            this.equipmentReader = addonReader.EquipmentReader;
            this.spellBookReader = addonReader.SpellBookReader;
            this.talentReader = addonReader.TalentReader;
            this.creatureDb = addonReader.CreatureDb;
            this.itemDb = addonReader.ItemDb;
            this.immunityBlacklist = immunityBlacklist;
            this.playerBuffTimeReader = addonReader.PlayerBuffTimeReader;
            this.targetDebuffTimeReader = addonReader.TargetDebuffTimeReader;

            requirementMap = new()
            {
                { ">=", CreateGreaterOrEquals },
                { "<=", CreateLesserOrEquals },
                { ">", CreateGreaterThen },
                { "<", CreateLesserThen },
                { "==", CreateEquals },
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
                { "TargetYieldXP", playerReader.TargetYieldXP },
                { "TargetsMe", playerReader.TargetsMe },
                { "TargetsPet", playerReader.TargetsPet },
                { "TargetsNone", playerReader.TargetsNone },

                { AddVisible, npcNameFinder._PotentialAddsExist },

                // Range
                { "InMeleeRange", playerReader.IsInMeleeRange },
                { "InDeadZoneRange", playerReader.IsInDeadZone },
                { "OutOfCombatRange", playerReader.OutOfCombatRange },
                { "InCombatRange", playerReader.WithInCombatRange },
                
                // Pet
                { "Has Pet", playerReader.Bits.HasPet },
                { "Pet Happy", playerReader.Bits.PetHappy },
                { "Mounted", playerReader.Bits.IsMounted },
                
                // Auto Spell
                { "AutoAttacking", playerReader.Bits.SpellOn_AutoAttack },
                { "Shooting", playerReader.Bits.SpellOn_Shoot },
                { "AutoShot", playerReader.Bits.SpellOn_AutoShot },
                
                // Temporary Enchants
                { "HasMainHandEnchant", playerReader.Bits.MainHandEnchant_Active },
                { "HasOffHandEnchant", playerReader.Bits.OffHandEnchant_Active },
                
                // Equipment - Bag
                { "Items Broken", playerReader.Bits.ItemsAreBroken },
                { "BagFull", bagReader.BagsFull },
                { "BagGreyItem", bagReader.AnyGreyItem },
                { "HasRangedWeapon", equipmentReader.HasRanged },
                { "HasAmmo", playerReader.Bits.HasAmmo },

                { "Casting", playerReader.IsCasting },

                // General Buff Condition
                { Food, playerReader.Buffs.Food },
                { Drink, playerReader.Buffs.Drink },
                { "Mana Regeneration", playerReader.Buffs.Mana_Regeneration },
                { "Well Fed", playerReader.Buffs.Well_Fed },
                { "Clearcasting", playerReader.Buffs.Clearcasting },

                // Player Affected
                { "Swimming", playerReader.Bits.IsSwimming },
                { "Falling", playerReader.Bits.IsFalling },

                //Priest
                { "Fortitude", playerReader.Buffs.Fortitude },
                { "InnerFire", playerReader.Buffs.InnerFire },
                { "Divine Spirit", playerReader.Buffs.DivineSpirit },
                { "Renew", playerReader.Buffs.Renew },
                { "Shield", playerReader.Buffs.Shield },

                // Druid
                { "Mark of the Wild", playerReader.Buffs.MarkOfTheWild },
                { "Thorns", playerReader.Buffs.Thorns },
                { "TigersFury", playerReader.Buffs.TigersFury },
                { "Prowl", playerReader.Buffs.Prowl },
                { "Rejuvenation", playerReader.Buffs.Rejuvenation },
                { "Regrowth", playerReader.Buffs.Regrowth },
                { "Omen of Clarity", playerReader.Buffs.OmenOfClarity },

                // Paladin
                { "Seal of Righteousness", playerReader.Buffs.SealofRighteousness },
                { "Seal of the Crusader", playerReader.Buffs.SealoftheCrusader },
                { "Seal of Command", playerReader.Buffs.SealofCommand },
                { "Seal of Wisdom", playerReader.Buffs.SealofWisdom },
                { "Seal of Light", playerReader.Buffs.SealofLight },
                { "Seal of Blood", playerReader.Buffs.SealofBlood },
                { "Seal of Vengeance", playerReader.Buffs.SealofVengeance },
                { "Blessing of Might", playerReader.Buffs.BlessingofMight },
                { "Blessing of Protection", playerReader.Buffs.BlessingofProtection },
                { "Blessing of Wisdom", playerReader.Buffs.BlessingofWisdom },
                { "Blessing of Kings", playerReader.Buffs.BlessingofKings },
                { "Blessing of Salvation", playerReader.Buffs.BlessingofSalvation },
                { "Blessing of Sanctuary", playerReader.Buffs.BlessingofSanctuary },
                { "Blessing of Light", playerReader.Buffs.BlessingofLight },
                { "Righteous Fury", playerReader.Buffs.RighteousFury },
                { "Divine Protection", playerReader.Buffs.DivineProtection },
                { "Avenging Wrath", playerReader.Buffs.AvengingWrath },
                { "Holy Shield", playerReader.Buffs.HolyShield },

                // Mage
                { "Frost Armor", playerReader.Buffs.FrostArmor },
                { "Ice Armor", playerReader.Buffs.FrostArmor },
                { "Molten Armor", playerReader.Buffs.FrostArmor },
                { "Mage Armor", playerReader.Buffs.FrostArmor },
                { "Arcane Intellect", playerReader.Buffs.ArcaneIntellect },
                { "Ice Barrier", playerReader.Buffs.IceBarrier },
                { "Ward", playerReader.Buffs.Ward },
                { "Fire Power", playerReader.Buffs.FirePower },
                { "Mana Shield", playerReader.Buffs.ManaShield },
                { "Presence of Mind", playerReader.Buffs.PresenceOfMind },
                { "Arcane Power", playerReader.Buffs.ArcanePower },
                
                // Rogue
                { "Slice and Dice", playerReader.Buffs.SliceAndDice },
                { "Stealth", playerReader.Buffs.Stealth },
                
                // Warrior
                { "Battle Shout", playerReader.Buffs.BattleShout },
                { "Bloodrage", playerReader.Buffs.Bloodrage },
                
                // Warlock
                { "Demon Skin", playerReader.Buffs.Demon },
                { "Demon Armor", playerReader.Buffs.Demon },
                { "Soul Link", playerReader.Buffs.SoulLink },
                { "Soulstone Resurrection", playerReader.Buffs.SoulstoneResurrection },
                { "Shadow Trance", playerReader.Buffs.ShadowTrance },
                { "Fel Armor", playerReader.Buffs.FelArmor },
                { "Fel Domination", playerReader.Buffs.FelDomination },
                { "Demonic Sacrifice", playerReader.Buffs.DemonicSacrifice },
                
                // Shaman
                { "Lightning Shield", playerReader.Buffs.LightningShield },
                { "Water Shield", playerReader.Buffs.WaterShield },
                { "Shamanistic Focus", playerReader.Buffs.ShamanisticFocus },
                { "Focused", playerReader.Buffs.ShamanisticFocus },
                { "Stoneskin", playerReader.Buffs.Stoneskin },
                
                //Hunter
                { "Aspect of the Cheetah", playerReader.Buffs.AspectoftheCheetah },
                { "Aspect of the Pack", playerReader.Buffs.AspectofthePack },
                { "Aspect of the Hawk", playerReader.Buffs.AspectoftheHawk },
                { "Aspect of the Monkey", playerReader.Buffs.AspectoftheMonkey },
                { "Aspect of the Viper", playerReader.Buffs.AspectoftheViper },
                { "Rapid Fire", playerReader.Buffs.RapidFire },
                { "Quick Shots", playerReader.Buffs.QuickShots },

                //Death Knight
                { "Blood Tap", playerReader.Buffs.BloodTap },
                { "Horn of Winter", playerReader.Buffs.HornofWinter },
                { "Icebound Fortitude", playerReader.Buffs.IceboundFortitude },
                { "Path of Frost", playerReader.Buffs.PathofFrost },
                { "Anti-Magic Shell", playerReader.Buffs.AntiMagicShell },
                { "Army of the Dead", playerReader.Buffs.ArmyoftheDead },
                { "Vampiric Blood", playerReader.Buffs.VampiricBlood },
                { "Dancing Rune Weapon", playerReader.Buffs.DancingRuneWeapon },
                { "Unbreakable Armor", playerReader.Buffs.UnbreakableArmor },
                { "Bone Shield", playerReader.Buffs.BoneShield },
                { "Summon Gargoyle", playerReader.Buffs.SummonGargoyle },
                { "Freezing Fog", playerReader.Buffs.FreezingFog },

                // Debuff Section
                // Druid Debuff
                { "Demoralizing Roar", playerReader.TargetDebuffs.Roar },
                { "Faerie Fire", playerReader.TargetDebuffs.FaerieFire },
                { "Rip", playerReader.TargetDebuffs.Rip },
                { "Moonfire", playerReader.TargetDebuffs.Moonfire },
                { "Entangling Roots", playerReader.TargetDebuffs.EntanglingRoots },
                { "Rake", playerReader.TargetDebuffs.Rake },
                
                // Paladin Debuff
                { "Judgement of the Crusader", playerReader.TargetDebuffs.JudgementoftheCrusader },
                { "Hammer of Justice", playerReader.TargetDebuffs.HammerOfJustice },
                { "Judgement of Wisdom", playerReader.TargetDebuffs.JudgementofWisdom },

                // Warrior Debuff
                { "Rend", playerReader.TargetDebuffs.Rend },
                { "Thunder Clap", playerReader.TargetDebuffs.ThunderClap },
                { "Hamstring", playerReader.TargetDebuffs.Hamstring },
                { "Charge Stun", playerReader.TargetDebuffs.ChargeStun },
                
                // Priest Debuff
                { "Shadow Word: Pain", playerReader.TargetDebuffs.ShadowWordPain },
                
                // Mage Debuff
                { "Frostbite", playerReader.TargetDebuffs.Frostbite },
                { "Slow", playerReader.TargetDebuffs.Slow },
                
                // Warlock Debuff
                { "Curse of Weakness", playerReader.TargetDebuffs.Curseof },
                { "Curse of Elements", playerReader.TargetDebuffs.Curseof },
                { "Curse of Recklessness", playerReader.TargetDebuffs.Curseof },
                { "Curse of Shadow", playerReader.TargetDebuffs.Curseof },
                { "Curse of Agony", playerReader.TargetDebuffs.Curseof },
                { "Curse of", playerReader.TargetDebuffs.Curseof },
                { "Corruption", playerReader.TargetDebuffs.Corruption },
                { "Immolate", playerReader.TargetDebuffs.Immolate },
                { "Siphon Life", playerReader.TargetDebuffs.SiphonLife },
                
                // Hunter Debuff
                { "Serpent Sting", playerReader.TargetDebuffs.SerpentSting },

                // Death Knight Debuff
                { "Blood Plague", playerReader.TargetDebuffs.BloodPlague },
                { "Frost Fever", playerReader.TargetDebuffs.FrostFever },
                { "Strangulate", playerReader.TargetDebuffs.Strangulate },
                { "Chains of Ice", playerReader.TargetDebuffs.ChainsofIce },
            };

            intVariables = new Dictionary<string, Func<int>>
            {
                { "Health%", playerReader.HealthPercent },
                { "TargetHealth%", playerReader.TargetHealthPercentage },
                { "PetHealth%", playerReader.PetHealthPercentage },
                { "Mana%", playerReader.ManaPercentage },
                { "Mana", playerReader.ManaCurrent },
                { "Energy", playerReader.PTCurrent },
                { "Rage", playerReader.PTCurrent },
                { "RunicPower", playerReader.PTCurrent },
                { "BloodRune", playerReader.BloodRune },
                { "FrostRune", playerReader.FrostRune },
                { "UnholyRune", playerReader.UnholyRune },
                { "TotalRune", playerReader.MaxRune },
                { "Combo Point", playerReader.ComboPoints },
                { "BagCount", bagReader.BagItemCount },
                { "MobCount", addonReader.DamageTakenCount },
                { "MinRange", playerReader.MinRange },
                { "MaxRange", playerReader.MaxRange },
                { "LastAutoShotMs", playerReader.AutoShot.ElapsedMs },
                { "LastMainHandMs", playerReader.MainHandSwing.ElapsedMs },
                { "LastTargetDodgeMs", () => Math.Max(0, addonReader.CombatLog.TargetDodge.ElapsedMs()) },
                //"CD_{KeyAction.Name}
                //"Cost_{KeyAction.Name}"
                //"Buff_{textureId}"
                //"Debuff_{textureId}"
                { "MainHandSpeed", playerReader.MainHandSpeedMs },
                { "MainHandSwing", () => Math.Clamp(playerReader.MainHandSwing.ElapsedMs() - playerReader.MainHandSpeedMs(), -playerReader.MainHandSpeedMs(), 0) },
                { "CurGCD", playerReader.GCD._Value },
                { "GCD", CastingHandler._GCD },
                { "SpellQueueTime", CastingHandler._SpellQueueTime },
                { "NextSpell", CastingHandler._NextSpell },
            };
        }

        public void InitialiseRequirements(KeyAction item, KeyActions? keyActions)
        {
            if (keyActions != null)
            {
                int sizeBefore = this.keyActions.Length;
                Array.Resize(ref this.keyActions, this.keyActions.Length + keyActions.Sequence.Length);

                for (int i = 0; i < keyActions.Sequence.Length; i++)
                {
                    this.keyActions[sizeBefore + i] = keyActions.Sequence[i];
                }
            }

            if (item.Name is Drink or Food)
                AddConsumableRequirement(item);

            InitPerKeyActionRequirements(item);

            List<Requirement> requirements = new();

            foreach (string requirement in item.Requirements)
            {
                List<string> expressions = InfixToPostfix.Convert(requirement);
                Stack<Requirement> stack = new();
                foreach (string expr in expressions)
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

            AddMinRequirement(requirements, item);
            AddTargetIsCastingRequirement(requirements, item, playerReader);

            if (item.WhenUsable && !string.IsNullOrEmpty(item.Key))
            {
                requirements.Add(CreateActionUsableRequirement(item, playerReader, addonReader.UsableAction));

                if (item.Slot > 0)
                    requirements.Add(CreateActionNotInGameCooldown(item));
            }

            AddCooldownRequirement(requirements, item);
            AddChargeRequirement(requirements, item);

            AddSpellSchoolRequirement(requirements, item, playerReader, immunityBlacklist);

            item.RequirementsRuntime = requirements.ToArray();
        }

        public void InitUserDefinedIntVariables(Dictionary<string, int> intKeyValues, AuraTimeReader playerBuffTimeReader, AuraTimeReader targetDebuffTimeReader)
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

                    LogUserDefinedValue(logger, nameof(RequirementFactory), key, value);
                }
            }
        }

        public void InitDynamicBindings(KeyAction item)
        {
            if (string.IsNullOrEmpty(item.Name) || item.Slot == 0) return;

            BindCooldown(item, addonReader.ActionBarCooldownReader);
            BindMinCost(item, addonReader.ActionBarCostReader);
        }

        private void BindCooldown(KeyAction item, ActionBarCooldownReader reader)
        {
            string key = $"CD_{item.Name}";
            if (!intVariables.ContainsKey(key))
            {
                intVariables.Add(key, () => reader.GetRemainingCooldown(item));
            }
        }

        private void BindMinCost(KeyAction item, ActionBarCostReader reader)
        {
            string key = $"Cost_{item.Name}";
            if (!intVariables.ContainsKey(key))
            {
                intVariables.Add(key,
                    () => reader.GetCostByActionBarSlot(item).Cost);
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
            if (intVariables.ContainsKey(prefixKey))
                intVariables.Remove(prefixKey);

            if (intVariables.ContainsKey(key))
                intVariables.Add(prefixKey, intVariables[key]);
        }

        private void AddTargetIsCastingRequirement(List<Requirement> list, KeyAction item, PlayerReader playerReader)
        {
            if (item.UseWhenTargetIsCasting != null)
            {
                bool f() => playerReader.IsTargetCasting() == item.UseWhenTargetIsCasting.Value;
                string l() => "Target casting";
                list.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = l
                });
            }
        }

        private void AddMinRequirement(List<Requirement> list, KeyAction item)
        {
            AddMinPowerTypeRequirement(list, PowerType.Mana, item, playerReader);
            AddMinPowerTypeRequirement(list, PowerType.Rage, item, playerReader);
            AddMinPowerTypeRequirement(list, PowerType.Energy, item, playerReader);
            if (playerReader.Class == UnitClass.DeathKnight)
            {
                AddMinPowerTypeRequirement(list, PowerType.RunicPower, item, playerReader);
                AddMinPowerTypeRequirement(list, PowerType.RuneBlood, item, playerReader);
                AddMinPowerTypeRequirement(list, PowerType.RuneFrost, item, playerReader);
                AddMinPowerTypeRequirement(list, PowerType.RuneUnholy, item, playerReader);
            }
            AddMinComboPointsRequirement(list, item, playerReader);
        }

        private void AddMinPowerTypeRequirement(List<Requirement> list, PowerType type, KeyAction keyAction, PlayerReader playerReader)
        {
            switch (type)
            {
                case PowerType.Mana:
                    bool fmana() => playerReader.ManaCurrent() >= keyAction.MinMana || playerReader.Buffs.Clearcasting();
                    string smana() => $"{type.ToStringF()} {playerReader.ManaCurrent()} >= {keyAction.MinMana}";
                    list.Add(new Requirement
                    {
                        HasRequirement = fmana,
                        LogMessage = smana,
                        VisibleIfHasRequirement = keyAction.MinMana > 0
                    });
                    break;
                case PowerType.Rage:
                    bool frage() => playerReader.PTCurrent() >= keyAction.MinRage || playerReader.Buffs.Clearcasting();
                    string srage() => $"{type.ToStringF()} {playerReader.PTCurrent()} >= {keyAction.MinRage}";
                    list.Add(new Requirement
                    {
                        HasRequirement = frage,
                        LogMessage = srage,
                        VisibleIfHasRequirement = keyAction.MinRage > 0
                    });
                    break;
                case PowerType.Energy:
                    bool fenergy() => playerReader.PTCurrent() >= keyAction.MinEnergy || playerReader.Buffs.Clearcasting();
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
            if (item.MinComboPoints > 0)
            {
                bool f() => playerReader.ComboPoints() >= item.MinComboPoints;
                string s() => $"Combo point {playerReader.ComboPoints()} >= {item.MinComboPoints}";
                list.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s
                });
            }
        }

        private static void AddCooldownRequirement(List<Requirement> list, KeyAction item)
        {
            if (item.Cooldown > 0)
            {
                bool f() => item.GetCooldownRemaining() == 0;
                string s() => $"Cooldown {item.GetCooldownRemaining() / 1000:F1}";
                list.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s,
                    VisibleIfHasRequirement = false
                });
            }
        }

        private static void AddChargeRequirement(List<Requirement> list, KeyAction item)
        {
            if (item.Charge > 1)
            {
                bool f() => item.GetChargeRemaining() != 0;
                string s() => $"Charge {item.GetChargeRemaining()}";
                list.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s
                });
            }
        }

        private static void AddConsumableRequirement(KeyAction item)
        {
            item.BeforeCastStop = true;
            item.WhenUsable = true;
            item.AfterCastWaitBuff = true;
            item.Item = true;

            item.Requirements.Add($"!{item.Name}");
            item.Requirements.Add("!Swimming");
            item.Requirements.Add("!Falling");
        }

        private void AddSpellSchoolRequirement(List<Requirement> list, KeyAction item, PlayerReader playerReader, Dictionary<int, SchoolMask[]> immunityBlacklist)
        {
            if (item.School != SchoolMask.None)
            {
                bool f() => !immunityBlacklist.TryGetValue(playerReader.TargetId, out SchoolMask[]? immuneAgaints) || !immuneAgaints.Contains(item.School);
                string s() => item.School.ToStringF();
                list.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s,
                    VisibleIfHasRequirement = false
                });
            }
        }


        public Requirement CreateRequirement(string name, string requirement)
        {
            LogProcessingRequirement(logger, name, requirement);

            string? negated = negateKeywords.FirstOrDefault(x => requirement.StartsWith(x));
            if (!string.IsNullOrEmpty(negated))
            {
                requirement = requirement[negated.Length..];
            }

            string? key = requirementMap.Keys.FirstOrDefault(x => requirement.Contains(x));
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

        private Requirement CreateActionUsableRequirement(KeyAction item, PlayerReader playerReader, ActionBarBits usableAction)
        {
            bool f() =>
                    !item.HasFormRequirement ? usableAction.Is(item) :
                    (playerReader.Form == item.FormEnum && usableAction.Is(item)) ||
                    (playerReader.Form != item.FormEnum && item.CanDoFormChangeMinResource());

            string s() =>
                    !item.HasFormRequirement ? "Usable" :
                    (playerReader.Form != item.FormEnum && item.CanDoFormChangeMinResource()) ? "Usable after Form change" :
                    (playerReader.Form == item.FormEnum && usableAction.Is(item)) ? "Usable current Form" : "not Usable current Form";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateActionNotInGameCooldown(KeyAction item)
        {
            string key = $"CD_{item.Name}";
            bool f() => UsableGCD(key);
            string s() => $"CD {intVariables[key]() / 1000f:F1}";

            return new Requirement
            {
                HasRequirement = f,
                VisibleIfHasRequirement = false,
                LogMessage = s
            };
        }

        private bool UsableGCD(string key)
        {
            return intVariables[key]() <= CastingHandler.SpellQueueTimeMs - playerReader.NetworkLatency.Value;
        }

        private Requirement CreateTargetCastingSpell(string requirement)
        {
            if (requirement.Contains(':'))
            {
                string[] parts = requirement.Split(":");
                string[] spellsPart = parts[1].Split("|");
                int[] spellIds = spellsPart.Select(x => int.Parse(x.Trim())).ToArray();

                var spellIdsStringFormatted = string.Join(", ", spellIds);

                bool f() => spellIds.Contains(playerReader.SpellBeingCastByTarget);
                string s() => $"Target casting {playerReader.SpellBeingCastByTarget} ∈ [{spellIdsStringFormatted}]";
                return new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s
                };
            }
            else
            {
                string s() => "Target casting";
                return new Requirement
                {
                    HasRequirement = playerReader.IsTargetCasting,
                    LogMessage = s
                };
            }
        }

        private Requirement CreateForm(string requirement)
        {
            string[] parts = requirement.Split(":");
            Form form = Enum.Parse<Form>(parts[1]);

            bool f() => playerReader.Form == form;
            string s() => $"{playerReader.Form}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateRace(string requirement)
        {
            string[] parts = requirement.Split(":");
            UnitRace race = Enum.Parse<UnitRace>(parts[1]);

            bool f() => playerReader.Race == race;
            string s() => playerReader.Race.ToStringF();

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateSpell(string requirement)
        {
            string[] parts = requirement.Split(":");
            string name = parts[1].Trim();

            if (int.TryParse(name, out int id) && spellBookReader.TryGetValue(id, out Spell spell))
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

        private Requirement CreateTalent(string requirement)
        {
            string[] parts = requirement.Split(":");
            string name = parts[1].Trim();
            int rank = parts.Length < 3 ? 1 : int.Parse(parts[2]);

            bool f() => talentReader.HasTalent(name, rank);
            string s() => rank == 1 ? $"Talent {name}" : $"Talent {name} (Rank {rank})";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateTrigger(string requirement)
        {
            var parts = requirement.Split(":");
            int bitNum = int.Parse(parts[1]);
            string text = parts.Length > 2 ? parts[2] : string.Empty;

            int bitMask = Mask.M[bitNum];

            bool f() => playerReader.CustomTrigger1[bitMask];
            string s() => $"Trigger({bitNum}) {text}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateNpcId(string requirement)
        {
            string[] parts = requirement.Split(":");

            int npcId;
            if (intVariables.TryGetValue(parts[1], out Func<int>? value))
                npcId = value();
            else
                npcId = int.Parse(parts[1]);

            string npcName = string.Empty;
            if (creatureDb.Entries.TryGetValue(npcId, out Creature creature))
            {
                npcName = creature.Name;
            }

            bool f() => playerReader.TargetId == npcId;
            string s() => $"TargetID {npcName}({npcId})";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateBagItem(string requirement)
        {
            string[] parts = requirement.Split(":");

            int itemId;
            if (intVariables.TryGetValue(parts[1], out Func<int>? value))
                itemId = value();
            else
                itemId = int.Parse(parts[1]);

            int count = parts.Length < 3 ? 1 : int.Parse(parts[2]);

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

        private Requirement CreateSpellInRange(string requirement)
        {
            string[] parts = requirement.Split(":");
            int bitNum = int.Parse(parts[1]);
            int bitMask = Mask.M[bitNum];

            bool f() => playerReader.SpellInRange[bitMask];
            string s() => $"SpellInRange {bitNum}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateUsable(string requirement)
        {
            string[] parts = requirement.Split(":");
            string name = parts[1].Trim();

            KeyAction keyAction = keyActions.First(x => x.Name == name);
            return CreateActionUsableRequirement(keyAction, playerReader, addonReader.UsableAction);
        }


        private Requirement CreateGreaterThen(string requirement)
        {
            return CreateArithmeticRequirement(">", requirement, intVariables);
        }

        private Requirement CreateLesserThen(string requirement)
        {
            return CreateArithmeticRequirement("<", requirement, intVariables);
        }

        private Requirement CreateGreaterOrEquals(string requirement)
        {
            return CreateArithmeticRequirement(">=", requirement, intVariables);
        }

        private Requirement CreateLesserOrEquals(string requirement)
        {
            return CreateArithmeticRequirement("<=", requirement, intVariables);
        }

        private Requirement CreateEquals(string requirement)
        {
            return CreateArithmeticRequirement("==", requirement, intVariables);
        }

        private Requirement CreateArithmeticRequirement(string symbol, string requirement, Dictionary<string, Func<int>> intVariables)
        {
            var parts = requirement.Split(symbol);
            var key = parts[0].Trim();

            if (!intVariables.ContainsKey(key))
            {
                LogUnknownRequirement(logger, requirement, string.Join(", ", intVariables.Keys));
                return new Requirement
                {
                    LogMessage = () => $"UNKNOWN REQUIREMENT! {requirement}"
                };
            }

            string display = key;

            Func<int> alias = intVariables[key];
            string aliasKey = alias().ToString();
            if (intVariables.ContainsKey(aliasKey))
            {
                key = aliasKey;
            }

            int zero() => 0;
            Func<int> value = zero;
            if (int.TryParse(parts[1], out int v))
            {
                int c() => v;
                value = c;
            }
            else
            {
                string variable = parts[1].Trim();
                if (intVariables.ContainsKey(variable))
                {
                    value = intVariables[variable];
                }
            }

            switch (symbol)
            {
                case "==":
                    bool e() => intVariables[key]() == value();
                    string es() => $"{display} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = e,
                        LogMessage = es
                    };
                case ">":
                    bool g() => intVariables[key]() > value();
                    string gs() => $"{display} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = g,
                        LogMessage = gs
                    };
                case "<":
                    bool l() => intVariables[key]() < value();
                    string ls() => $"{display} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = l,
                        LogMessage = ls
                    };
                case ">=":
                    bool ge() => intVariables[key]() >= value();
                    string ges() => $"{display} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = ge,
                        LogMessage = ges
                    };
                case "<=":
                    bool le() => intVariables[key]() <= value();
                    string les() => $"{display} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = le,
                        LogMessage = les
                    };

                default:
                    return new Requirement
                    {
                        LogMessage = () => $"UNKNOWN ARITHMETIC REQUIREMENT! {key} {intVariables[key]()} ? {value()}"
                    };
            };
        }

        #region Logging

        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Information,
            Message = "[{typeName}] Defined int variable [{key} -> {value}]")]
        static partial void LogUserDefinedValue(ILogger logger, string typeName, string key, int value);

        [LoggerMessage(
            EventId = 12,
            Level = LogLevel.Information,
            Message = "[{name}] Requirement: \"{requirement}\"")]
        static partial void LogProcessingRequirement(ILogger logger, string name, string requirement);

        [LoggerMessage(
            EventId = 13,
            Level = LogLevel.Error,
            Message = "UNKNOWN REQUIREMENT! {requirement}: try one of: {available}")]
        static partial void LogUnknownRequirement(ILogger logger, string requirement, string available);

        #endregion
    }
}