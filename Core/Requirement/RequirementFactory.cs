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

        public const string AddVisible = "AddVisible";

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


            bool TargetYieldXP() => playerReader.TargetYieldXP;
            bool TargetsMe() => playerReader.TargetTarget == TargetTargetEnum.Me;
            bool TargetsPet() => playerReader.TargetTarget == TargetTargetEnum.Pet;
            bool TargetsNone() => playerReader.TargetTarget == TargetTargetEnum.None;
            bool PotentialAddsExist() => npcNameFinder.PotentialAddsExist;

            bool IsInMeleeRange() => playerReader.IsInMeleeRange;
            bool IsInDeadZone() => playerReader.IsInDeadZone;
            bool WithInCombatRange() => !playerReader.WithInCombatRange;
            bool InCombatRange() => playerReader.WithInCombatRange;

            bool HasPet() => playerReader.Bits.HasPet;
            bool PetHappy() => playerReader.Bits.PetHappy;

            bool AutoAttacking() => playerReader.Bits.IsAutoRepeatSpellOn_AutoAttack;
            bool Shooting() => playerReader.Bits.IsAutoRepeatSpellOn_Shoot;
            bool AutoShot() => playerReader.Bits.IsAutoRepeatSpellOn_AutoShot;

            bool HasMainHandEnchant() => playerReader.Bits.MainHandEnchant_Active;
            bool HasOffHandEnchant() => playerReader.Bits.OffHandEnchant_Active;

            bool ItemsBroken() => playerReader.Bits.ItemsAreBroken;
            bool BagFull() => bagReader.BagsFull;
            bool BagGreyItem() => bagReader.AnyGreyItem;
            bool HasRangedWeapon() => equipmentReader.HasRanged();
            bool HasAmmo() => playerReader.Bits.HasAmmo;

            bool IsCasting() => playerReader.IsCasting;

            bool Eating() => playerReader.Buffs.Eating;
            bool Drinking() => playerReader.Buffs.Drinking;
            bool ManaRegeneration() => playerReader.Buffs.ManaRegeneration;
            bool WellFed() => playerReader.Buffs.WellFed;
            bool Clearcasting() => playerReader.Buffs.Clearcasting;

            bool Swimming() => playerReader.Bits.IsSwimming;
            bool Falling() => playerReader.Bits.IsFalling;

            bool Fortitude() => playerReader.Buffs.Fortitude;
            bool InnerFire() => playerReader.Buffs.InnerFire;
            bool DivineSpirit() => playerReader.Buffs.DivineSpirit;
            bool Renew() => playerReader.Buffs.Renew;

            bool Shield() => playerReader.Buffs.Shield;
            bool MarkOfTheWild() => playerReader.Buffs.MarkOfTheWild;
            bool Thorns() => playerReader.Buffs.Thorns;
            bool TigersFury() => playerReader.Buffs.TigersFury;
            bool Prowl() => playerReader.Buffs.Prowl;
            bool Rejuvenation() => playerReader.Buffs.Rejuvenation;
            bool Regrowth() => playerReader.Buffs.Regrowth;
            bool OmenOfClarity() => playerReader.Buffs.OmenOfClarity;

            bool Paladin_Concentration_Aura() => playerReader.Form == Form.Paladin_Concentration_Aura;
            bool Paladin_Crusader_Aura() => playerReader.Form == Form.Paladin_Crusader_Aura;
            bool Paladin_Devotion_Aura() => playerReader.Form == Form.Paladin_Devotion_Aura;
            bool Paladin_Sanctity_Aura() => playerReader.Form == Form.Paladin_Sanctity_Aura;
            bool Paladin_Fire_Resistance_Aura() => playerReader.Form == Form.Paladin_Fire_Resistance_Aura;
            bool Paladin_Frost_Resistance_Aura() => playerReader.Form == Form.Paladin_Frost_Resistance_Aura;
            bool Paladin_Retribution_Aura() => playerReader.Form == Form.Paladin_Retribution_Aura;
            bool Paladin_Shadow_Resistance_Aura() => playerReader.Form == Form.Paladin_Shadow_Resistance_Aura;
            bool SealofRighteousness() => playerReader.Buffs.SealofRighteousness;
            bool SealoftheCrusader() => playerReader.Buffs.SealoftheCrusader;
            bool SealofCommand() => playerReader.Buffs.SealofCommand;
            bool SealofWisdom() => playerReader.Buffs.SealofWisdom;
            bool SealofLight() => playerReader.Buffs.SealofLight;
            bool SealofBlood() => playerReader.Buffs.SealofBlood;
            bool SealofVengeance() => playerReader.Buffs.SealofVengeance;
            bool BlessingofMight() => playerReader.Buffs.BlessingofMight;
            bool BlessingofProtection() => playerReader.Buffs.BlessingofProtection;
            bool BlessingofWisdom() => playerReader.Buffs.BlessingofWisdom;
            bool BlessingofKings() => playerReader.Buffs.BlessingofKings;
            bool BlessingofSalvation() => playerReader.Buffs.BlessingofSalvation;
            bool BlessingofSanctuary() => playerReader.Buffs.BlessingofSanctuary;
            bool BlessingofLight() => playerReader.Buffs.BlessingofLight;
            bool RighteousFury() => playerReader.Buffs.RighteousFury;
            bool DivineProtection() => playerReader.Buffs.DivineProtection;
            bool AvengingWrath() => playerReader.Buffs.AvengingWrath;
            bool HolyShield() => playerReader.Buffs.HolyShield;

            bool FrostArmor() => playerReader.Buffs.FrostArmor;
            bool ArcaneIntellect() => playerReader.Buffs.ArcaneIntellect;
            bool IceBarrier() => playerReader.Buffs.IceBarrier;
            bool Ward() => playerReader.Buffs.Ward;
            bool FirePower() => playerReader.Buffs.FirePower;
            bool ManaShield() => playerReader.Buffs.ManaShield;
            bool PresenceOfMind() => playerReader.Buffs.PresenceOfMind;
            bool ArcanePower() => playerReader.Buffs.ArcanePower;

            bool SliceAndDice() => playerReader.Buffs.SliceAndDice;
            bool Stealth() => playerReader.Buffs.Stealth;

            bool BattleShout() => playerReader.Buffs.BattleShout;
            bool Bloodrage() => playerReader.Buffs.Bloodrage;

            bool Demon() => playerReader.Buffs.Demon;
            bool SoulLink() => playerReader.Buffs.SoulLink;
            bool SoulstoneResurrection() => playerReader.Buffs.SoulstoneResurrection;
            bool ShadowTrance() => playerReader.Buffs.ShadowTrance;
            bool FelArmor() => playerReader.Buffs.FelArmor;
            bool FelDomination() => playerReader.Buffs.FelDomination;
            bool DemonicSacrifice() => playerReader.Buffs.DemonicSacrifice;

            bool LightningShield() => playerReader.Buffs.LightningShield;
            bool WaterShield() => playerReader.Buffs.WaterShield;
            bool ShamanisticFocus() => playerReader.Buffs.ShamanisticFocus;
            bool Stoneskin() => playerReader.Buffs.Stoneskin;

            bool AspectoftheCheetah() => playerReader.Buffs.AspectoftheCheetah;
            bool AspectofthePack() => playerReader.Buffs.AspectofthePack;
            bool AspectoftheHawk() => playerReader.Buffs.AspectoftheHawk;
            bool AspectoftheMonkey() => playerReader.Buffs.AspectoftheMonkey;
            bool AspectoftheViper() => playerReader.Buffs.AspectoftheViper;
            bool RapidFire() => playerReader.Buffs.RapidFire;
            bool QuickShots() => playerReader.Buffs.QuickShots;

            bool Roar() => playerReader.TargetDebuffs.Roar;
            bool FaerieFire() => playerReader.TargetDebuffs.FaerieFire;
            bool Rip() => playerReader.TargetDebuffs.Rip;
            bool Moonfire() => playerReader.TargetDebuffs.Moonfire;
            bool EntanglingRoots() => playerReader.TargetDebuffs.EntanglingRoots;
            bool Rake() => playerReader.TargetDebuffs.Rake;

            bool JudgementoftheCrusader() => playerReader.TargetDebuffs.JudgementoftheCrusader;
            bool HammerOfJustice() => playerReader.TargetDebuffs.HammerOfJustice;

            bool Rend() => playerReader.TargetDebuffs.Rend;
            bool ThunderClap() => playerReader.TargetDebuffs.ThunderClap;
            bool Hamstring() => playerReader.TargetDebuffs.Hamstring;
            bool ChargeStun() => playerReader.TargetDebuffs.ChargeStun;

            bool ShadowWordPain() => playerReader.TargetDebuffs.ShadowWordPain;

            bool Frostbite() => playerReader.TargetDebuffs.Frostbite;
            bool Slow() => playerReader.TargetDebuffs.Slow;

            bool Curseof() => playerReader.TargetDebuffs.Curseof;
            bool Corruption() => playerReader.TargetDebuffs.Corruption;
            bool Immolate() => playerReader.TargetDebuffs.Immolate;
            bool SiphonLife() => playerReader.TargetDebuffs.SiphonLife;

            bool SerpentSting() => playerReader.TargetDebuffs.SerpentSting;

            boolVariables = new Dictionary<string, Func<bool>>
            {
                // Target Based
                { "TargetYieldXP", TargetYieldXP },
                { "TargetsMe", TargetsMe },
                { "TargetsPet", TargetsPet },
                { "TargetsNone", TargetsNone },

                { AddVisible, PotentialAddsExist },

                // Range
                { "InMeleeRange", IsInMeleeRange },
                { "InDeadZoneRange", IsInDeadZone },
                { "OutOfCombatRange", WithInCombatRange },
                { "InCombatRange", InCombatRange },
                
                // Pet
                { "Has Pet", HasPet },
                { "Pet Happy", PetHappy },
                
                // Auto Spell
                { "AutoAttacking", AutoAttacking },
                { "Shooting", Shooting },
                { "AutoShot", AutoShot },
                
                // Temporary Enchants
                { "HasMainHandEnchant", HasMainHandEnchant },
                { "HasOffHandEnchant", HasOffHandEnchant },
                
                // Equipment - Bag
                { "Items Broken", ItemsBroken },
                { "BagFull", BagFull },
                { "BagGreyItem", BagGreyItem },
                { "HasRangedWeapon", HasRangedWeapon },
                { "HasAmmo", HasAmmo },

                { "Casting", IsCasting },

                // General Buff Condition
                { "Eating", Eating },
                { "Drinking", Drinking },
                { "Mana Regeneration", ManaRegeneration },
                { "Well Fed", WellFed },
                { "Clearcasting", Clearcasting },

                // Player Affected
                { "Swimming", Swimming },
                { "Falling", Falling },

                //Priest
                { "Fortitude", Fortitude },
                { "InnerFire", InnerFire },
                { "Divine Spirit", DivineSpirit },
                { "Renew", Renew },
                { "Shield", Shield },

                // Druid
                { "Mark of the Wild", MarkOfTheWild },
                { "Thorns", Thorns },
                { "TigersFury", TigersFury },
                { "Prowl", Prowl },
                { "Rejuvenation", Rejuvenation },
                { "Regrowth", Regrowth },
                { "Omen of Clarity", OmenOfClarity },

                // Paladin
                { "Concentration Aura", Paladin_Concentration_Aura },
                { "Crusader Aura", Paladin_Crusader_Aura },
                { "Devotion Aura", Paladin_Devotion_Aura },
                { "Sanctity Aura", Paladin_Sanctity_Aura },
                { "Fire Resistance Aura", Paladin_Fire_Resistance_Aura },
                { "Frost Resistance Aura", Paladin_Frost_Resistance_Aura },
                { "Retribution Aura", Paladin_Retribution_Aura },
                { "Shadow Resistance Aura", Paladin_Shadow_Resistance_Aura },
                { "Seal of Righteousness", SealofRighteousness },
                { "Seal of the Crusader", SealoftheCrusader },
                { "Seal of Command", SealofCommand },
                { "Seal of Wisdom", SealofWisdom },
                { "Seal of Light", SealofLight },
                { "Seal of Blood", SealofBlood },
                { "Seal of Vengeance", SealofVengeance },
                { "Blessing of Might", BlessingofMight },
                { "Blessing of Protection", BlessingofProtection },
                { "Blessing of Wisdom", BlessingofWisdom },
                { "Blessing of Kings", BlessingofKings },
                { "Blessing of Salvation", BlessingofSalvation },
                { "Blessing of Sanctuary", BlessingofSanctuary },
                { "Blessing of Light", BlessingofLight },
                { "Righteous Fury", RighteousFury },
                { "Divine Protection", DivineProtection },
                { "Avenging Wrath", AvengingWrath },
                { "Holy Shield", HolyShield },
                // Mage
                { "Frost Armor", FrostArmor },
                { "Ice Armor", FrostArmor },
                { "Molten Armor", FrostArmor },
                { "Mage Armor", FrostArmor },
                { "Arcane Intellect", ArcaneIntellect },
                { "Ice Barrier", IceBarrier },
                { "Ward", Ward },
                { "Fire Power", FirePower },
                { "Mana Shield", ManaShield },
                { "Presence of Mind", PresenceOfMind },
                { "Arcane Power", ArcanePower },
                
                // Rogue
                { "Slice and Dice", SliceAndDice },
                { "Stealth", Stealth },
                
                // Warrior
                { "Battle Shout", BattleShout },
                { "Bloodrage", Bloodrage },
                
                // Warlock
                { "Demon Skin", Demon },
                { "Demon Armor", Demon },
                { "Soul Link", SoulLink },
                { "Soulstone Resurrection", SoulstoneResurrection },
                { "Shadow Trance", ShadowTrance },
                { "Fel Armor", FelArmor },
                { "Fel Domination", FelDomination },
                { "Demonic Sacrifice", DemonicSacrifice },
                
                // Shaman
                { "Lightning Shield", LightningShield },
                { "Water Shield", WaterShield },
                { "Shamanistic Focus", ShamanisticFocus },
                { "Focused", ShamanisticFocus },
                { "Stoneskin", Stoneskin },
                
                //Hunter
                { "Aspect of the Cheetah", AspectoftheCheetah },
                { "Aspect of the Pack", AspectofthePack },
                { "Aspect of the Hawk", AspectoftheHawk },
                { "Aspect of the Monkey", AspectoftheMonkey },
                { "Aspect of the Viper", AspectoftheViper },
                { "Rapid Fire", RapidFire },
                { "Quick Shots", QuickShots },

                // Debuff Section
                // Druid Debuff
                { "Demoralizing Roar", Roar },
                { "Faerie Fire", FaerieFire },
                { "Rip", Rip },
                { "Moonfire", Moonfire },
                { "Entangling Roots", EntanglingRoots },
                { "Rake", Rake },
                
                // Paladin Debuff
                { "Judgement of the Crusader", JudgementoftheCrusader },
                { "Hammer of Justice", HammerOfJustice },

                // Warrior Debuff
                { "Rend", Rend },
                { "Thunder Clap", ThunderClap },
                { "Hamstring", Hamstring },
                { "Charge Stun", ChargeStun },
                
                // Priest Debuff
                { "Shadow Word: Pain", ShadowWordPain },
                
                // Mage Debuff
                { "Frostbite", Frostbite },
                { "Slow", Slow },
                
                // Warlock Debuff
                { "Curse of Weakness", Curseof },
                { "Curse of Elements", Curseof },
                { "Curse of Recklessness", Curseof },
                { "Curse of Shadow", Curseof },
                { "Curse of Agony", Curseof },
                { "Curse of", Curseof },
                { "Corruption", Corruption },
                { "Immolate", Immolate },
                { "Siphon Life", SiphonLife },
                
                // Hunter Debuff
                { "Serpent Sting", SerpentSting },
            };


            int HealthPercent() => playerReader.HealthPercent;
            int TargetHealthPercentage() => playerReader.TargetHealthPercentage;
            int PetHealthPercentage() => playerReader.PetHealthPercentage;
            int ManaPercentage() => playerReader.ManaPercentage;
            int ManaCurrent() => playerReader.ManaCurrent;
            int PTCurrent() => playerReader.PTCurrent;
            int ComboPoints() => playerReader.ComboPoints;
            int BagCount() => bagReader.BagItems.Count;
            int MobCount() => addonReader.CombatCreatureCount;
            int MinRange() => playerReader.MinRange;
            int MaxRange() => playerReader.MaxRange;
            int LastAutoShotMs() => playerReader.AutoShot.ElapsedMs;
            int LastMainHandMs() => playerReader.MainHandSwing.ElapsedMs;
            int MainHandSpeed() => playerReader.MainHandSpeedMs;
            int MainHandSwing() => Math.Clamp(playerReader.MainHandSwing.ElapsedMs - playerReader.MainHandSpeedMs, -playerReader.MainHandSpeedMs, 0);

            intVariables = new Dictionary<string, Func<int>>
            {
                { "Health%", HealthPercent },
                { "TargetHealth%", TargetHealthPercentage },
                { "PetHealth%", PetHealthPercentage },
                { "Mana%", ManaPercentage },
                { "Mana", ManaCurrent },
                { "Energy", PTCurrent },
                { "Rage", PTCurrent },
                { "Combo Point", ComboPoints },
                { "BagCount", BagCount },
                { "MobCount", MobCount },
                { "MinRange", MinRange },
                { "MaxRange", MaxRange },
                { "LastAutoShotMs", LastAutoShotMs },
                { "LastMainHandMs", LastMainHandMs }, 
                //"CD_{KeyAction.Name}
                //"Cost_{KeyAction.Name}"
                { "MainHandSpeed", MainHandSpeed },
                { "MainHandSwing", MainHandSwing }
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
                bool f() => playerReader.IsTargetCasting == item.UseWhenTargetIsCasting.Value;
                string l() => "Target casting";
                itemRequirementObjects.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = l
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
                    bool fmana() => playerReader.ManaCurrent >= keyAction.MinMana || playerReader.Buffs.Clearcasting;
                    string smana() => $"{type} {playerReader.ManaCurrent} >= {keyAction.MinMana}";
                    RequirementObjects.Add(new Requirement
                    {
                        HasRequirement = fmana,
                        LogMessage = smana,
                        VisibleIfHasRequirement = keyAction.MinMana > 0
                    });
                    break;
                case PowerType.Rage:
                    bool frage() => playerReader.PTCurrent >= keyAction.MinRage || playerReader.Buffs.Clearcasting;
                    string srage() => $"{type} {playerReader.PTCurrent} >= {keyAction.MinRage}";
                    RequirementObjects.Add(new Requirement
                    {
                        HasRequirement = frage,
                        LogMessage = srage,
                        VisibleIfHasRequirement = keyAction.MinRage > 0
                    });
                    break;
                case PowerType.Energy:
                    bool fenergy() => playerReader.PTCurrent >= keyAction.MinEnergy || playerReader.Buffs.Clearcasting;
                    string senergy() => $"{type} {playerReader.PTCurrent} >= {keyAction.MinEnergy}";
                    RequirementObjects.Add(new Requirement
                    {
                        HasRequirement = fenergy,
                        LogMessage = senergy,
                        VisibleIfHasRequirement = keyAction.MinEnergy > 0
                    });
                    break;
            }
        }

        private void AddMinComboPointsRequirement(List<Requirement> RequirementObjects, KeyAction item)
        {
            if (item.MinComboPoints > 0)
            {
                bool f() => playerReader.ComboPoints >= item.MinComboPoints;
                string s() => $"Combo point {playerReader.ComboPoints} >= {item.MinComboPoints}";
                RequirementObjects.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s
                });
            }
        }

        private static void AddCooldownRequirement(List<Requirement> RequirementObjects, KeyAction item)
        {
            if (item.Cooldown > 0)
            {
                bool f() => item.GetCooldownRemaining() == 0;
                string s() => $"Cooldown {item.GetCooldownRemaining() / 1000:F1}";
                RequirementObjects.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s,
                    VisibleIfHasRequirement = false
                });
            }
        }

        private static void AddChargeRequirement(List<Requirement> RequirementObjects, KeyAction item)
        {
            if (item.Charge > 1)
            {
                bool f() => item.GetChargeRemaining() != 0;
                string s() => $"Charge {item.GetChargeRemaining()}";
                RequirementObjects.Add(new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s
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
                string s() => requirement;
                var requirementObj = new Requirement
                {
                    HasRequirement = boolVariables[requirement],
                    LogMessage = s
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
            bool f() =>
                    !item.HasFormRequirement() ? addonReader.UsableAction.Is(item) :
                    (playerReader.Form == item.FormEnum && addonReader.UsableAction.Is(item)) ||
                    (playerReader.Form != item.FormEnum && item.CanDoFormChangeAndHaveMinimumMana());

            string s() =>
                    !item.HasFormRequirement() ? $"Usable" : // {playerReader.UsableAction.Num(item)}
                    (playerReader.Form != item.FormEnum && item.CanDoFormChangeAndHaveMinimumMana()) ? $"Usable after Form change" : // {playerReader.UsableAction.Num(item)}
                    (playerReader.Form == item.FormEnum && addonReader.UsableAction.Is(item)) ? $"Usable current Form" : $"not Usable current Form"; // {playerReader.UsableAction.Num(item)}

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateActionNotInGameCooldown(KeyAction item)
        {
            string key = $"CD_{item.Name}";
            bool f() => intVariables[key]() == 0;
            string s() => $"CD {intVariables[key]() / 1000}";

            return new Requirement
            {
                HasRequirement = f,
                VisibleIfHasRequirement = false,
                LogMessage = s
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
                bool f() => playerReader.IsTargetCasting;
                string s() => "Target casting";
                return new Requirement
                {
                    HasRequirement = f,
                    LogMessage = s
                };
            }
        }

        private Requirement CreateForm(string requirement)
        {
            var parts = requirement.Split(":");
            var form = Enum.Parse<Form>(parts[1]);

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
            var parts = requirement.Split(":");
            var race = Enum.Parse<RaceEnum>(parts[1]);

            bool f() => playerReader.Race == race;
            string s() => $"{playerReader.Race}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
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

            bool f() => spellBookReader.Spells.ContainsKey(id);
            string s() => $"Spell {name}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
            };
        }

        private Requirement CreateTalent(string requirement)
        {
            var parts = requirement.Split(":");
            var name = parts[1].Trim();
            var rank = parts.Length < 3 ? 1 : int.Parse(parts[2]);

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
            int bit = int.Parse(parts[1]);
            string text = parts.Length > 2 ? parts[2] : string.Empty;

            bool f() => playerReader.CustomTrigger1.IsBitSet(bit);
            string s() => $"Trigger({bit}) {text}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
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
            var parts = requirement.Split(":");
            var itemId = int.Parse(parts[1]);
            var count = parts.Length < 3 ? 1 : int.Parse(parts[2]);

            var itemName = string.Empty;
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
            var parts = requirement.Split(":");
            var bitId = int.Parse(parts[1]);

            bool f() => playerReader.SpellInRange.IsBitSet(bitId);
            string s() => $"SpellInRange {bitId}";

            return new Requirement
            {
                HasRequirement = f,
                LogMessage = s
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

            switch(symbol)
            {
                case "==":
                    bool e() => intVariables[key]() == value();
                    string es() => $"{key} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = e,
                        LogMessage = es
                    };
                case ">":
                    bool g() => intVariables[key]() > value();
                    string gs() => $"{key} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = g,
                        LogMessage = gs
                    };
                case "<":
                    bool l() => intVariables[key]() < value();
                    string ls() => $"{key} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = l,
                        LogMessage = ls
                    };
                case ">=":
                    bool ge() => intVariables[key]() >= value();
                    string ges() => $"{key} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = ge,
                        LogMessage = ges
                    };
                case "<=":
                    bool le() => intVariables[key]() <= value();
                    string les() => $"{key} {intVariables[key]()} {symbol} {value()}";
                    return new Requirement
                    {
                        HasRequirement = le,
                        LogMessage = les
                    };

                default:
                    return new Requirement
                    {
                        HasRequirement = () => false,
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