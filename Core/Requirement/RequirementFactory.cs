using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Database;
using SharedLib;
using SharedLib.NpcFinder;

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

        private readonly KeyActions keyActions;

        private readonly List<string> negateKeywords = new()
        {
            "not ",
            "!"
        };

        private readonly Dictionary<string, Func<int>> intVariables;

        private readonly Dictionary<string, Func<bool>> boolVariables;

        private readonly Dictionary<string, Func<string, Requirement>> requirementMap;

        public RequirementFactory(ILogger logger, AddonReader addonReader, NpcNameFinder npcNameFinder)
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

            this.keyActions = new KeyActions();

            requirementMap = new Dictionary<string, Func<string, Requirement>>()
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

            boolVariables = new Dictionary<string, Func<bool>>
            {
                // Target Based
                { "TargetYieldXP", () => playerReader.TargetYieldXP },
                { "TargetsMe", () => playerReader.TargetTarget == TargetTargetEnum.Me },
                { "TargetsPet", () => playerReader.TargetTarget == TargetTargetEnum.Pet },
                { "TargetsNone", () => playerReader.TargetTarget == TargetTargetEnum.None },

                { "AddVisible", () => npcNameFinder.PotentialAddsExist },

                // Range
                { "InMeleeRange", ()=> playerReader.IsInMeleeRange },
                { "InDeadZoneRange", ()=> playerReader.IsInDeadZone },
                { "OutOfCombatRange", ()=> !playerReader.WithInCombatRange },
                { "InCombatRange", ()=> playerReader.WithInCombatRange },
                
                // Pet
                { "Has Pet", ()=> playerReader.Bits.HasPet },
                { "Pet Happy", ()=> playerReader.Bits.PetHappy },
                
                // Auto Spell
                { "AutoAttacking", ()=> playerReader.Bits.IsAutoRepeatSpellOn_AutoAttack },
                { "Shooting", ()=> playerReader.Bits.IsAutoRepeatSpellOn_Shoot },
                { "AutoShot", ()=> playerReader.Bits.IsAutoRepeatSpellOn_AutoShot },
                
                // Temporary Enchants
                { "HasMainHandEnchant", ()=> playerReader.Bits.MainHandEnchant_Active },
                { "HasOffHandEnchant", ()=> playerReader.Bits.OffHandEnchant_Active },
                
                // Equipment - Bag
                { "Items Broken", ()=> playerReader.Bits.ItemsAreBroken },
                { "BagFull", ()=> bagReader.BagsFull },
                { "BagGreyItem", ()=> bagReader.AnyGreyItem },
                { "HasRangedWeapon", ()=> equipmentReader.HasRanged() },
                { "HasAmmo", ()=> playerReader.Bits.HasAmmo },
                
                // General Buff Condition
                { "Eating", ()=> playerReader.Buffs.Eating },
                { "Drinking", ()=> playerReader.Buffs.Drinking },
                { "Mana Regeneration", ()=> playerReader.Buffs.ManaRegeneration },
                { "Well Fed", ()=> playerReader.Buffs.WellFed },
                { "Clearcasting", ()=> playerReader.Buffs.Clearcasting },

                // Player Affected
                { "Swimming", ()=> playerReader.Bits.IsSwimming },
                { "Falling", ()=> playerReader.Bits.IsFalling },

                //Priest
                { "Fortitude", ()=> playerReader.Buffs.Fortitude },
                { "InnerFire", ()=> playerReader.Buffs.InnerFire },
                { "Divine Spirit", ()=> playerReader.Buffs.DivineSpirit },
                { "Renew", ()=> playerReader.Buffs.Renew },
                { "Shield", ()=> playerReader.Buffs.Shield },

                // Druid
                { "Mark of the Wild", ()=> playerReader.Buffs.MarkOfTheWild },
                { "Thorns", ()=> playerReader.Buffs.Thorns },
                { "TigersFury", ()=> playerReader.Buffs.TigersFury },
                { "Prowl", ()=> playerReader.Buffs.Prowl },
                { "Rejuvenation", ()=> playerReader.Buffs.Rejuvenation },
                { "Regrowth", ()=> playerReader.Buffs.Regrowth },
                { "Omen of Clarity", ()=> playerReader.Buffs.OmenOfClarity },

                // Paladin
                { "Concentration Aura", ()=> playerReader.Form == Form.Paladin_Concentration_Aura },
                { "Crusader Aura", ()=> playerReader.Form == Form.Paladin_Crusader_Aura },
                { "Devotion Aura", ()=> playerReader.Form == Form.Paladin_Devotion_Aura },
                { "Sanctity Aura", ()=> playerReader.Form == Form.Paladin_Sanctity_Aura },
                { "Fire Resistance Aura", ()=> playerReader.Form == Form.Paladin_Fire_Resistance_Aura },
                { "Frost Resistance Aura", ()=> playerReader.Form == Form.Paladin_Frost_Resistance_Aura },
                { "Retribution Aura", ()=> playerReader.Form == Form.Paladin_Retribution_Aura },
                { "Shadow Resistance Aura", ()=> playerReader.Form == Form.Paladin_Shadow_Resistance_Aura },
                { "Seal of Righteousness", ()=> playerReader.Buffs.SealofRighteousness },
                { "Seal of the Crusader", ()=> playerReader.Buffs.SealoftheCrusader },
                { "Seal of Command", ()=> playerReader.Buffs.SealofCommand },
                { "Seal of Wisdom", ()=> playerReader.Buffs.SealofWisdom },
                { "Seal of Light", ()=> playerReader.Buffs.SealofLight },
                { "Seal of Blood", ()=> playerReader.Buffs.SealofBlood },
                { "Seal of Vengeance", ()=> playerReader.Buffs.SealofVengeance },
                { "Blessing of Might", ()=> playerReader.Buffs.BlessingofMight },
                { "Blessing of Protection", ()=> playerReader.Buffs.BlessingofProtection },
                { "Blessing of Wisdom", ()=> playerReader.Buffs.BlessingofWisdom },
                { "Blessing of Kings", ()=> playerReader.Buffs.BlessingofKings },
                { "Blessing of Salvation", ()=> playerReader.Buffs.BlessingofSalvation },
                { "Blessing of Sanctuary", ()=> playerReader.Buffs.BlessingofSanctuary },
                { "Blessing of Light", ()=> playerReader.Buffs.BlessingofLight },
                { "Righteous Fury", ()=> playerReader.Buffs.RighteousFury },
                { "Divine Protection", ()=> playerReader.Buffs.DivineProtection },
                { "Avenging Wrath", ()=> playerReader.Buffs.AvengingWrath },
                { "Holy Shield", ()=> playerReader.Buffs.HolyShield },
                // Mage
                { "Frost Armor", ()=> playerReader.Buffs.FrostArmor },
                { "Ice Armor", ()=> playerReader.Buffs.FrostArmor },
                { "Arcane Intellect", ()=> playerReader.Buffs.ArcaneIntellect },
                { "Ice Barrier", ()=>playerReader.Buffs.IceBarrier },
                { "Ward", ()=>playerReader.Buffs.Ward },
                { "Fire Power", ()=>playerReader.Buffs.FirePower },
                { "Mana Shield", ()=>playerReader.Buffs.ManaShield },
                { "Presence of Mind", ()=>playerReader.Buffs.PresenceOfMind },
                { "Arcane Power", ()=>playerReader.Buffs.ArcanePower },
                
                // Rogue
                { "Slice and Dice", ()=> playerReader.Buffs.SliceAndDice },
                { "Stealth", ()=> playerReader.Buffs.Stealth },
                
                // Warrior
                { "Battle Shout", ()=> playerReader.Buffs.BattleShout },
                { "Bloodrage", ()=> playerReader.Buffs.Bloodrage },
                
                // Warlock
                { "Demon Skin", ()=> playerReader.Buffs.Demon },
                { "Demon Armor", ()=> playerReader.Buffs.Demon },
                { "Soul Link", ()=> playerReader.Buffs.SoulLink },
                { "Soulstone Resurrection", ()=> playerReader.Buffs.SoulstoneResurrection },
                { "Shadow Trance", ()=> playerReader.Buffs.ShadowTrance },
                
                // Shaman
                { "Lightning Shield", ()=> playerReader.Buffs.LightningShield },
                { "Water Shield", ()=> playerReader.Buffs.WaterShield },
                { "Shamanistic Focus", ()=> playerReader.Buffs.ShamanisticFocus },
                { "Focused", ()=> playerReader.Buffs.ShamanisticFocus },
                { "Stoneskin", ()=> playerReader.Buffs.Stoneskin },
                
                //Hunter
                { "Aspect of the Cheetah", ()=> playerReader.Buffs.AspectoftheCheetah },
                { "Aspect of the Pack", ()=> playerReader.Buffs.AspectofthePack },
                { "Aspect of the Hawk", ()=> playerReader.Buffs.AspectoftheHawk },
                { "Aspect of the Monkey", ()=> playerReader.Buffs.AspectoftheMonkey },
                { "Aspect of the Viper", ()=> playerReader.Buffs.AspectoftheViper },
                { "Rapid Fire", ()=> playerReader.Buffs.RapidFire },
                { "Quick Shots", ()=> playerReader.Buffs.QuickShots },

                // Debuff Section
                // Druid Debuff
                { "Demoralizing Roar", ()=> playerReader.TargetDebuffs.Roar },
                { "Faerie Fire", ()=> playerReader.TargetDebuffs.FaerieFire },
                { "Rip", ()=> playerReader.TargetDebuffs.Rip },
                { "Moonfire", ()=> playerReader.TargetDebuffs.Moonfire },
                { "Entangling Roots", ()=> playerReader.TargetDebuffs.EntanglingRoots },
                { "Rake", ()=> playerReader.TargetDebuffs.Rake },
                
                // Paladin Debuff
                { "Judgement of the Crusader", ()=> playerReader.TargetDebuffs.JudgementoftheCrusader },
                { "Hammer of Justice", ()=> playerReader.TargetDebuffs.HammerOfJustice },

                // Warrior Debuff
                { "Rend", ()=> playerReader.TargetDebuffs.Rend },
                { "Thunder Clap", ()=> playerReader.TargetDebuffs.ThunderClap },
                { "Hamstring", ()=> playerReader.TargetDebuffs.Hamstring },
                { "Charge Stun", ()=> playerReader.TargetDebuffs.ChargeStun },
                
                // Priest Debuff
                { "Shadow Word: Pain", ()=> playerReader.TargetDebuffs.ShadowWordPain },
                
                // Mage Debuff
                { "Frostbite", ()=> playerReader.TargetDebuffs.Frostbite },
                { "Slow", ()=> playerReader.TargetDebuffs.Slow },
                
                // Warlock Debuff
                { "Curse of Weakness", ()=> playerReader.TargetDebuffs.Curseof },
                { "Curse of Elements", ()=> playerReader.TargetDebuffs.Curseof },
                { "Curse of Recklessness", ()=> playerReader.TargetDebuffs.Curseof },
                { "Curse of Shadow", ()=> playerReader.TargetDebuffs.Curseof },
                { "Curse of Agony", ()=> playerReader.TargetDebuffs.Curseof },
                { "Curse of", ()=> playerReader.TargetDebuffs.Curseof },
                { "Corruption", ()=> playerReader.TargetDebuffs.Corruption },
                { "Immolate", ()=> playerReader.TargetDebuffs.Immolate },
                { "Siphon Life", ()=> playerReader.TargetDebuffs.SiphonLife },
                
                // Hunter Debuff
                { "Serpent Sting", ()=> playerReader.TargetDebuffs.SerpentSting },
            };

            intVariables = new Dictionary<string, Func<int>>
            {
                { "Health%", () => playerReader.HealthPercent },
                { "TargetHealth%", () => playerReader.TargetHealthPercentage },
                { "PetHealth%", () => playerReader.PetHealthPercentage },
                { "Mana%", () => playerReader.ManaPercentage },
                { "Mana", () => playerReader.ManaCurrent },
                { "Energy", () => playerReader.PTCurrent },
                { "Rage", () => playerReader.PTCurrent },
                { "Combo Point", () => playerReader.ComboPoints },
                { "BagCount", () => bagReader.BagItems.Count },
                { "MobCount", () => addonReader.CombatCreatureCount },
                { "MinRange", () => playerReader.MinRange },
                { "MaxRange", () => playerReader.MaxRange },
                { "LastAutoShotMs", () => playerReader.AutoShot.ElapsedMs },
                { "LastMainHandMs", () => playerReader.MainHandSwing.ElapsedMs }, 
                //"CD_{KeyAction.Name}
                //"Cost_{KeyAction.Name}"
                { "MainHandSpeed", () => playerReader.MainHandSpeedMs },
                { "MainHandSwing", () => Math.Clamp(playerReader.MainHandSwing.ElapsedMs - playerReader.MainHandSpeedMs, -playerReader.MainHandSpeedMs, 0) }
            };
        }

        public void InitialiseRequirements(KeyAction item, KeyActions? keyActions)
        {
            if (keyActions != null)
                this.keyActions.Sequence.AddRange(keyActions.Sequence);

            AddConsumableRequirement("Water", item);
            AddConsumableRequirement("Food", item);

            InitPerKeyActionRequirements(item);

            item.RequirementObjects.Clear();
            foreach (string requirement in item.Requirements)
            {
                List<string> expressions = InfixToPostfix.Convert(requirement);
                Stack<Requirement> stack = new();
                foreach (string expr in expressions)
                {
                    if (expr.Contains("&&"))
                    {
                        var a = stack.Pop();
                        var b = stack.Pop();

                        stack.Push(b.And(a));
                    }
                    else if (expr.Contains("||"))
                    {
                        var a = stack.Pop();
                        var b = stack.Pop();

                        stack.Push(b.Or(a));
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

                item.RequirementObjects.Add(stack.Pop());
            }

            AddMinRequirement(item.RequirementObjects, item);
            AddTargetIsCastingRequirement(item.RequirementObjects, item);

            if (item.WhenUsable && !string.IsNullOrEmpty(item.Key))
            {
                item.RequirementObjects.Add(CreateActionUsableRequirement(item));
                item.RequirementObjects.Add(CreateActionNotInGameCooldown(item));
            }

            AddCooldownRequirement(item.RequirementObjects, item);
            AddChargeRequirement(item.RequirementObjects, item);
        }

        public void InitUserDefinedIntVariables(Dictionary<string, int> intKeyValues)
        {
            foreach (var kvp in intKeyValues)
            {
                if (!intVariables.TryAdd(kvp.Key, () => kvp.Value))
                {
                    throw new Exception($"Unable to add user defined variable to values. [{kvp.Key} -> {kvp.Value}]");
                }
                else
                {
                    LogUserDefinedValue(logger, nameof(RequirementFactory), kvp.Key, kvp.Value);
                }
            }
        }

        public void InitDynamicBindings(KeyAction item)
        {
            BindCooldown(item);
            BindMinCost(item);
        }

        private void BindCooldown(KeyAction item)
        {
            string key = $"CD_{item.Name}";
            if (!intVariables.ContainsKey(key))
            {
                intVariables.Add(key,
                    () => addonReader.ActionBarCooldownReader.GetRemainingCooldown(playerReader, item));
            }
        }

        private void BindMinCost(KeyAction item)
        {
            string key = $"Cost_{item.Name}";
            if (!intVariables.ContainsKey(key))
            {
                intVariables.Add(key,
                    () => addonReader.ActionBarCostReader.GetCostByActionBarSlot(playerReader, item).cost);
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

        private void AddTargetIsCastingRequirement(List<Requirement> itemRequirementObjects, KeyAction item)
        {
            if (item.UseWhenTargetIsCasting != null)
            {
                itemRequirementObjects.Add(new Requirement
                {
                    HasRequirement = () => playerReader.IsTargetCasting == item.UseWhenTargetIsCasting.Value,
                    LogMessage = () => "Target casting"
                });
            }
        }

        private void AddMinRequirement(List<Requirement> RequirementObjects, KeyAction item)
        {
            AddMinPowerTypeRequirement(RequirementObjects, PowerType.Mana, item);
            AddMinPowerTypeRequirement(RequirementObjects, PowerType.Rage, item);
            AddMinPowerTypeRequirement(RequirementObjects, PowerType.Energy, item);
            AddMinComboPointsRequirement(RequirementObjects, item);
        }

        private void AddMinPowerTypeRequirement(List<Requirement> RequirementObjects, PowerType type, KeyAction keyAction)
        {
            switch (type)
            {
                case PowerType.Mana:
                    RequirementObjects.Add(new Requirement
                    {
                        HasRequirement = () => playerReader.ManaCurrent >= keyAction.MinMana || playerReader.Buffs.Clearcasting,
                        LogMessage = () => $"{type} {playerReader.ManaCurrent} >= {keyAction.MinMana}",
                        VisibleIfHasRequirement = keyAction.MinMana > 0
                    });
                    break;
                case PowerType.Rage:
                    RequirementObjects.Add(new Requirement
                    {
                        HasRequirement = () => playerReader.PTCurrent >= keyAction.MinRage || playerReader.Buffs.Clearcasting,
                        LogMessage = () => $"{type} {playerReader.PTCurrent} >= {keyAction.MinRage}",
                        VisibleIfHasRequirement = keyAction.MinRage > 0
                    });
                    break;
                case PowerType.Energy:
                    RequirementObjects.Add(new Requirement
                    {
                        HasRequirement = () => playerReader.PTCurrent >= keyAction.MinEnergy || playerReader.Buffs.Clearcasting,
                        LogMessage = () => $"{type} {playerReader.PTCurrent} >= {keyAction.MinEnergy}",
                        VisibleIfHasRequirement = keyAction.MinEnergy > 0
                    });
                    break;
            }
        }

        private void AddMinComboPointsRequirement(List<Requirement> RequirementObjects, KeyAction item)
        {
            if (item.MinComboPoints > 0)
            {
                RequirementObjects.Add(new Requirement
                {
                    HasRequirement = () => playerReader.ComboPoints >= item.MinComboPoints,
                    LogMessage = () => $"Combo point {playerReader.ComboPoints} >= {item.MinComboPoints}"
                });
            }
        }

        private static void AddCooldownRequirement(List<Requirement> RequirementObjects, KeyAction item)
        {
            if (item.Cooldown > 0)
            {
                RequirementObjects.Add(new Requirement
                {
                    HasRequirement = () => item.GetCooldownRemaining() == 0,
                    LogMessage = () => $"Cooldown {item.GetCooldownRemaining() / 1000:F1}",
                    VisibleIfHasRequirement = false
                });
            }
        }

        private static void AddChargeRequirement(List<Requirement> RequirementObjects, KeyAction item)
        {
            if (item.Charge > 1)
            {
                RequirementObjects.Add(new Requirement
                {
                    HasRequirement = () => item.GetChargeRemaining() != 0,
                    LogMessage = () => $"Charge {item.GetChargeRemaining()}"
                });
            }
        }

        private static void AddConsumableRequirement(string name, KeyAction item)
        {
            if (item.Name == name)
            {
                item.StopBeforeCast = true;
                item.WhenUsable = true;
                item.AfterCastWaitBuff = true;

                item.Requirements.Add("!Swimming");
                item.Requirements.Add("!Falling");
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
                var requirementObj = requirementMap[key](requirement);
                return negated != null ? requirementObj.Negate(negated) : requirementObj;
            }

            if (boolVariables.ContainsKey(requirement))
            {
                var requirementObj = new Requirement
                {
                    HasRequirement = boolVariables[requirement],
                    LogMessage = () => requirement
                };
                return negated != null ? requirementObj.Negate(negated) : requirementObj;
            }

            LogUnknownRequirement(logger, requirement, string.Join(", ", boolVariables.Keys));
            return new Requirement
            {
                HasRequirement = () => false,
                LogMessage = () => $"UNKNOWN REQUIREMENT! {requirement}"
            };
        }

        private Requirement CreateActionUsableRequirement(KeyAction item)
        {
            return new Requirement
            {
                HasRequirement = () =>
                    !item.HasFormRequirement() ? addonReader.UsableAction.Is(item) :
                    (playerReader.Form == item.FormEnum && addonReader.UsableAction.Is(item)) ||
                    (playerReader.Form != item.FormEnum && item.CanDoFormChangeAndHaveMinimumMana()),

                LogMessage = () =>
                    !item.HasFormRequirement() ? $"Usable" : // {playerReader.UsableAction.Num(item)}
                    (playerReader.Form != item.FormEnum && item.CanDoFormChangeAndHaveMinimumMana()) ? $"Usable after Form change" : // {playerReader.UsableAction.Num(item)}
                    (playerReader.Form == item.FormEnum && addonReader.UsableAction.Is(item)) ? $"Usable current Form" : $"not Usable current Form" // {playerReader.UsableAction.Num(item)}
            };
        }

        private Requirement CreateActionNotInGameCooldown(KeyAction item)
        {
            string key = $"CD_{item.Name}";
            return new Requirement
            {
                HasRequirement = () => intVariables[key]() == 0,
                VisibleIfHasRequirement = false,
                LogMessage = () =>
                    $"CD {intVariables[key]() / 1000:F1}"
            };
        }

        private Requirement CreateTargetCastingSpell(string requirement)
        {
            if (requirement.Contains(':'))
            {
                var parts = requirement.Split(":");
                var spellsPart = parts[1].Split("|");
                var spellIds = spellsPart.Select(x => int.Parse(x.Trim())).ToArray();

                var spellIdsStringFormatted = string.Join(", ", spellIds);

                return new Requirement
                {
                    HasRequirement = () => spellIds.Contains(playerReader.SpellBeingCastByTarget),
                    LogMessage = () =>
                        $"Target casting {playerReader.SpellBeingCastByTarget} ∈ [{spellIdsStringFormatted}]"
                };
            }
            else
            {
                return new Requirement
                {
                    HasRequirement = () => playerReader.IsTargetCasting,
                    LogMessage = () => "Target casting"
                };
            }
        }

        private Requirement CreateForm(string requirement)
        {
            var parts = requirement.Split(":");
            var form = Enum.Parse<Form>(parts[1]);

            return new Requirement
            {
                HasRequirement = () => playerReader.Form == form,
                LogMessage = () => $"{playerReader.Form}"
            };
        }

        private Requirement CreateRace(string requirement)
        {
            var parts = requirement.Split(":");
            var race = Enum.Parse<RaceEnum>(parts[1]);

            return new Requirement
            {
                HasRequirement = () => playerReader.Race == race,
                LogMessage = () => $"{playerReader.Race}"
            };
        }

        private Requirement CreateSpell(string requirement)
        {
            var parts = requirement.Split(":");
            var name = parts[1].Trim();

            if (int.TryParse(parts[1], out int id) && spellBookReader.SpellDB.Spells.TryGetValue(id, out Spell spell))
            {
                name = $"{spell.Name}({id})";
            }
            else
            {
                id = spellBookReader.GetSpellIdByName(name);
            }

            return new Requirement
            {
                HasRequirement = () => spellBookReader.Spells.ContainsKey(id),
                LogMessage = () => $"Spell {name}"
            };
        }

        private Requirement CreateTalent(string requirement)
        {
            var parts = requirement.Split(":");
            var name = parts[1].Trim();
            var rank = parts.Length < 3 ? 1 : int.Parse(parts[2]);

            return new Requirement
            {
                HasRequirement = () => talentReader.HasTalent(name, rank),
                LogMessage = () => rank == 1 ? $"Talent {name}" : $"Talent {name} (Rank {rank})"
            };
        }

        private Requirement CreateTrigger(string requirement)
        {
            var parts = requirement.Split(":");
            int bit = int.Parse(parts[1]);
            string text = parts.Length > 2 ? parts[2] : string.Empty;

            return new Requirement
            {
                HasRequirement = () => playerReader.CustomTrigger1.IsBitSet(bit),
                LogMessage = () => $"Trigger({bit}) {text}"
            };
        }

        private Requirement CreateNpcId(string requirement)
        {
            var parts = requirement.Split(":");
            var npcId = int.Parse(parts[1]);

            string npcName = string.Empty;
            if (creatureDb.Entries.TryGetValue(npcId, out Creature creature))
            {
                npcName = creature.Name;
            }

            return new Requirement
            {
                HasRequirement = () => playerReader.TargetId == npcId,
                LogMessage = () => $"TargetID {npcName}({npcId})"
            };
        }

        private Requirement CreateBagItem(string requirement)
        {
            var parts = requirement.Split(":");
            var itemId = int.Parse(parts[1]);
            var count = parts.Length < 3 ? 1 : int.Parse(parts[2]);

            var itemName = string.Empty;
            if (itemDb.Items.TryGetValue(itemId, out Item item))
            {
                itemName = item.Name;
            }

            return new Requirement
            {
                HasRequirement = () => bagReader.ItemCount(itemId) >= count,
                LogMessage = () => count == 1 ? $"in bag {itemName}({itemId})" : $"{itemName}({itemId}) count >= {count}"
            };
        }

        private Requirement CreateSpellInRange(string requirement)
        {
            var parts = requirement.Split(":");
            var bitId = int.Parse(parts[1]);

            return new Requirement
            {
                HasRequirement = () => playerReader.SpellInRange.IsBitSet(bitId),
                LogMessage = () => $"SpellInRange {bitId}"
            };
        }

        private Requirement CreateUsable(string requirement)
        {
            var parts = requirement.Split(":");
            string name = parts[1].Trim();

            var keyAction = keyActions.Sequence.First(x => x.Name == name);
            return CreateActionUsableRequirement(keyAction);
        }


        private Requirement CreateGreaterThen(string requirement)
        {
            return CreateArithmeticRequirement(">", requirement);
        }

        private Requirement CreateLesserThen(string requirement)
        {
            return CreateArithmeticRequirement("<", requirement);
        }

        private Requirement CreateGreaterOrEquals(string requirement)
        {
            return CreateArithmeticRequirement(">=", requirement);
        }

        private Requirement CreateLesserOrEquals(string requirement)
        {
            return CreateArithmeticRequirement("<=", requirement);
        }

        private Requirement CreateEquals(string requirement)
        {
            return CreateArithmeticRequirement("==", requirement);
        }

        private Requirement CreateArithmeticRequirement(string symbol, string requirement)
        {
            var parts = requirement.Split(symbol);
            var key = parts[0].Trim();

            if (!intVariables.ContainsKey(key))
            {
                LogUnknownRequirement(logger, requirement, string.Join(", ", intVariables.Keys));
                return new Requirement
                {
                    HasRequirement = () => false,
                    LogMessage = () => $"UNKNOWN REQUIREMENT! {requirement}"
                };
            }

            Func<int> value = () => 0;
            if (int.TryParse(parts[1], out int v))
            {
                value = () => v;
            }
            else
            {
                string variable = parts[1].Trim();
                if (intVariables.ContainsKey(variable))
                {
                    value = intVariables[variable];
                }
            }

            return symbol switch
            {
                "==" => new Requirement
                {
                    HasRequirement = () => intVariables[key]() == value(),
                    LogMessage = () => $"{key} {intVariables[key]()} {symbol} {value()}"
                },
                ">" => new Requirement
                {
                    HasRequirement = () => intVariables[key]() > value(),
                    LogMessage = () => $"{key} {intVariables[key]()} {symbol} {value()}"
                },
                "<" => new Requirement
                {
                    HasRequirement = () => intVariables[key]() < value(),
                    LogMessage = () => $"{key} {intVariables[key]()} {symbol} {value()}"
                },
                ">=" => new Requirement
                {
                    HasRequirement = () => intVariables[key]() >= value(),
                    LogMessage = () => $"{key} {intVariables[key]()} {symbol} {value()}"
                },
                "<=" => new Requirement
                {
                    HasRequirement = () => intVariables[key]() <= value(),
                    LogMessage = () => $"{key} {intVariables[key]()} {symbol} {value()}"
                },
                _ => new Requirement
                {
                    HasRequirement = () => false,
                    LogMessage = () => $"UNKNOWN ARITHMETIC REQUIREMENT! {key} {intVariables[key]()} ? {value()}"
                },
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