using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Core
{
    public partial class KeyAction : IDisposable
    {
        public string Name { get; set; } = string.Empty;
        public bool HasCastBar { get; set; }
        public bool StopBeforeCast { get; set; }
        public ConsoleKey ConsoleKey { get; set; }
        public string Key { get; set; } = string.Empty;
        public int Slot { get; set; }
        public int SlotIndex { get; private set; }
        public int PressDuration { get; set; } = 50;
        public string Form { get; set; } = string.Empty;
        public Form FormEnum { get; set; } = Core.Form.None;
        public float Cooldown { get; set; } = Goals.CastingHandler.SpellQueueTimeMs;

        private int _charge;
        public int Charge { get; set; } = 1;
        public SchoolMask School { get; set; } = SchoolMask.None;
        public int MinMana { get; set; }
        public int MinRage { get; set; }
        public int MinEnergy { get; set; }
        public int MinRunicPower { get; set; }
        public int MinRuneBlood { get; set; }
        public int MinRuneFrost { get; set; }
        public int MinRuneUnholy { get; set; }
        public int MinComboPoints { get; set; }

        public string Requirement { get; set; } = string.Empty;
        public List<string> Requirements { get; } = new();
        public Requirement[] RequirementsRuntime { get; set; } = Array.Empty<Requirement>();

        public bool WhenUsable { get; set; }

        public bool WaitForWithinMeleeRange { get; set; }
        public bool ResetOnNewTarget { get; set; }

        public bool Log { get; set; } = true;
        public int DelayAfterCast { get; set; } = 1450; // GCD 1500 - but spell queue window 400 ms

        public bool WaitForGCD { get; set; } = true;

        public bool SkipValidation { get; set; }

        public bool AfterCastWaitBuff { get; set; }

        public bool AfterCastWaitItem { get; set; }

        public bool AfterCastWaitNextSwing { get; set; }

        public bool AfterCastWaitCastbar { get; set; }

        public bool DelayUntilCombat { get; set; }
        public int DelayBeforeCast { get; set; }
        public float Cost { get; set; } = 18;
        public string InCombat { get; set; } = "false";

        public bool? UseWhenTargetIsCasting { get; set; }

        public string PathFilename { get; set; } = string.Empty;
        public Vector3[] Path { get; set; } = Array.Empty<Vector3>();

        public int StepBackAfterCast { get; set; }

        public Vector3 LastClickPostion { get; private set; }

        public int ConsoleKeyFormHash { private set; get; }

        protected static Dictionary<int, DateTime> LastClicked { get; } = new();

        public static int LastKeyClicked()
        {
            var (key, lastTime) = LastClicked.OrderByDescending(s => s.Value).First();
            if ((DateTime.UtcNow - lastTime).TotalSeconds > 2)
            {
                return (int)ConsoleKey.NoName;
            }
            return key;
        }

        private PlayerReader playerReader = null!;
        private ActionBarCostReader costReader = null!;

        private ILogger logger = null!;

        public void InitialiseSlot(ILogger logger)
        {
            if (!KeyReader.ReadKey(logger, this))
            {
                throw new Exception($"[{Name}] has no valid Key={ConsoleKey}");
            }
        }

        public void InitDynamicBinding(RequirementFactory requirementFactory)
        {
            requirementFactory.InitDynamicBindings(this);
        }

        public void Initialise(AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger, bool globalLog, KeyActions? keyActions = null)
        {
            this.playerReader = addonReader.PlayerReader;
            this.costReader = addonReader.ActionBarCostReader;
            this.logger = logger;

            if (!globalLog)
                Log = false;

            ResetCharges();

            InitialiseSlot(logger);

            if (!string.IsNullOrEmpty(this.Requirement))
            {
                Requirements.Add(this.Requirement);
            }

            if (HasFormRequirement())
            {
                if (Enum.TryParse(Form, out Form desiredForm))
                {
                    this.FormEnum = desiredForm;
                    this.logger.LogInformation($"[{Name}] Required Form: {FormEnum.ToStringF()}");
                }
                else
                {
                    throw new Exception($"[{Name}] Unknown form: {Form}");
                }
            }

            if (Slot > 0)
            {
                this.SlotIndex = Stance.ToSlot(this, playerReader) - 1;
                this.logger.LogInformation($"[{Name}] Actionbar Form key map: Key:{Key} -> Actionbar:{Slot} -> Index:{SlotIndex}");
            }

            ConsoleKeyFormHash = ((int)FormEnum * 1000) + (int)ConsoleKey;
            ResetCooldown();

            if (Slot > 0)
                InitMinPowerType(addonReader.ActionBarCostReader);

            requirementFactory.InitialiseRequirements(this, keyActions);
        }

        public void Dispose()
        {
            costReader.OnActionCostChanged -= ActionBarCostReader_OnActionCostChanged;
            costReader.OnActionCostReset -= ResetCosts;
        }

        public void InitialiseForm(AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger, bool globalLog)
        {
            Initialise(addonReader, requirementFactory, logger, globalLog);

            if (HasFormRequirement())
            {
                if (addonReader.PlayerReader.FormCost.ContainsKey(FormEnum))
                {
                    addonReader.PlayerReader.FormCost.Remove(FormEnum);
                }

                addonReader.PlayerReader.FormCost.Add(FormEnum, MinMana);
                logger.LogInformation($"[{Name}] Added {FormEnum} to FormCost with {MinMana}");
            }
        }

        public void ResetCosts()
        {
            MinMana = 0;
            MinRage = 0;
            MinEnergy = 0;
            MinRunicPower = 0;

            MinRuneBlood = 0;
            MinRuneFrost = 0;
            MinRuneUnholy = 0;
        }

        public float GetCooldownRemaining()
        {
            var remain = MillisecondsSinceLastClick;
            if (remain == double.MaxValue) return 0;
            return MathF.Max(Cooldown - (float)remain, 0);
        }

        public bool CanDoFormChangeAndHaveMinimumMana()
        {
            return playerReader.FormCost.ContainsKey(FormEnum) &&
                playerReader.ManaCurrent() >= playerReader.FormCost[FormEnum] + MinMana;
        }

        internal void SetClicked()
        {
            LastClickPostion = playerReader.PlayerLocation;
            LastClicked[ConsoleKeyFormHash] = DateTime.UtcNow;
        }

        public double MillisecondsSinceLastClick =>
            LastClicked.TryGetValue(ConsoleKeyFormHash, out DateTime lastTime) ?
            (DateTime.UtcNow - lastTime).TotalMilliseconds :
            double.MaxValue;

        public void ResetCooldown()
        {
            LastClicked[ConsoleKeyFormHash] = DateTime.Now.AddDays(-1);
        }

        public int GetChargeRemaining()
        {
            return _charge;
        }

        public void ConsumeCharge()
        {
            if (Charge > 1)
            {
                _charge--;
                if (_charge > 0)
                {
                    ResetCooldown();
                }
                else
                {
                    ResetCharges();
                    SetClicked();
                }
            }
        }

        internal void ResetCharges()
        {
            _charge = Charge;
        }

        public bool CanRun()
        {
            for (int i = 0; i < RequirementsRuntime.Length; i++)
            {
                if (!RequirementsRuntime[i].HasRequirement())
                    return false;
            }

            return true;
        }

        private void InitMinPowerType(ActionBarCostReader actionBarCostReader)
        {
            for (int i = 0; i < ActionBar.NUM_OF_COST; i++)
            {
                ActionBarCost abc = actionBarCostReader.GetCostByActionBarSlot(playerReader, this, i);
                if (abc.Cost != 0)
                {
                    int oldValue = 0;
                    switch (abc.PowerType)
                    {
                        case PowerType.Mana:
                            oldValue = MinMana;
                            MinMana = abc.Cost;
                            break;
                        case PowerType.Rage:
                            oldValue = MinRage;
                            MinRage = abc.Cost;
                            break;
                        case PowerType.Energy:
                            oldValue = MinEnergy;
                            MinEnergy = abc.Cost;
                            break;
                        case PowerType.RunicPower:
                            oldValue = MinRunicPower;
                            MinRunicPower = abc.Cost;
                            break;
                        case PowerType.RuneBlood:
                            oldValue = MinRuneBlood;
                            MinRuneBlood = abc.Cost;
                            break;
                        case PowerType.RuneFrost:
                            oldValue = MinRuneFrost;
                            MinRuneFrost = abc.Cost;
                            break;
                        case PowerType.RuneUnholy:
                            oldValue = MinRuneUnholy;
                            MinRuneUnholy = abc.Cost;
                            break;
                    }

                    int formCost = 0;
                    if (HasFormRequirement() && FormEnum != Core.Form.None && playerReader.FormCost.ContainsKey(FormEnum))
                    {
                        formCost = playerReader.FormCost[FormEnum];
                    }

                    LogPowerCostChange(logger, Name, abc.PowerType.ToStringF(), abc.Cost, oldValue);
                    if (formCost > 0)
                    {
                        logger.LogInformation($"[{Name}] +{formCost} Mana to change {FormEnum.ToStringF()} Form");
                    }
                }
            }
            actionBarCostReader.OnActionCostChanged += ActionBarCostReader_OnActionCostChanged;
            actionBarCostReader.OnActionCostReset += ResetCosts;
        }

        private void ActionBarCostReader_OnActionCostChanged(object? sender, ActionBarCostEventArgs e)
        {
            if (Slot != e.Slot) return;

                return;

            int oldValue = 0;
            switch (e.ActionBarCost.PowerType)
            {
                case PowerType.Mana:
                    oldValue = MinMana;
                    MinMana = e.ActionBarCost.Cost;
                    break;
                case PowerType.Rage:
                    oldValue = MinRage;
                    MinRage = e.ActionBarCost.Cost;
                    break;
                case PowerType.Energy:
                    oldValue = MinEnergy;
                    MinEnergy = e.ActionBarCost.Cost;
                    break;
                case PowerType.RunicPower:
                    oldValue = MinRunicPower;
                    MinRunicPower = e.ActionBarCost.Cost;
                    break;
                case PowerType.RuneBlood:
                    oldValue = MinRuneBlood;
                    MinRuneBlood = e.ActionBarCost.Cost;
                    break;
                case PowerType.RuneFrost:
                    oldValue = MinRuneFrost;
                    MinRuneFrost = e.ActionBarCost.Cost;
                    break;
                case PowerType.RuneUnholy:
                    oldValue = MinRuneUnholy;
                    MinRuneUnholy = e.ActionBarCost.Cost;
                    break;
            }

            if (e.ActionBarCost.Cost != oldValue)
            {
                LogPowerCostChange(logger, Name, e.ActionBarCost.PowerType.ToStringF(), e.ActionBarCost.Cost, oldValue);
            }
        }

        #region Logging

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Information,
            Message = "[{name}] Update {type} cost to {newCost} from {oldCost}")]
        static partial void LogPowerCostChange(ILogger logger, string name, string type, int newCost, int oldCost);

        #endregion
    }
}