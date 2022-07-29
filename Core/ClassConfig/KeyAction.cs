using Core.Goals;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Core
{
    public partial class KeyAction : IDisposable
    {
        public float Cost { get; set; } = 18;
        public string Name { get; set; } = string.Empty;
        public bool HasCastBar { get; set; }
        public ConsoleKey ConsoleKey { get; set; }
        public string Key { get; set; } = string.Empty;
        public int Slot { get; set; }
        public int SlotIndex { get; private set; }
        public int PressDuration { get; set; } = 50;
        public string Form { get; set; } = string.Empty;
        public Form FormEnum { get; set; } = Core.Form.None;
        public bool FormAction { get; private set; }
        public float Cooldown { get; set; } = CastingHandler.SpellQueueTimeMs;

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

        public int MinCost { get; set; }

        public Func<int> FormCost = null!;

        public bool HasFormRequirement { get; private set; }

        public string Requirement { get; set; } = string.Empty;
        public List<string> Requirements { get; } = new();
        public Requirement[] RequirementsRuntime { get; set; } = Array.Empty<Requirement>();

        public bool WhenUsable { get; set; }

        public bool ResetOnNewTarget { get; set; }

        public bool Log { get; set; } = true;

        public bool BaseAction { get; set; }

        public bool Item { get; set; }

        public int BeforeCastDelay { get; set; }
        public bool BeforeCastStop { get; set; }

        public int AfterCastDelay { get; set; }
        public bool AfterCastWaitMeleeRange { get; set; }
        public bool AfterCastWaitBuff { get; set; }
        public bool AfterCastWaitBag { get; set; }
        public bool AfterCastWaitSwing { get; set; }
        public bool AfterCastWaitCastbar { get; set; }
        public bool AfterCastWaitCombat { get; set; }
        public bool AfterCastWaitGCD { get; set; }
        public bool AfterCastAuraExpected { get; set; }
        public int AfterCastStepBack { get; set; }

        public string InCombat { get; set; } = "false";

        public bool? UseWhenTargetIsCasting { get; set; }

        public string PathFilename { get; set; } = string.Empty;
        public Vector3[] Path { get; set; } = Array.Empty<Vector3>();

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

        public void Initialise(ClassConfiguration config, AddonReader addonReader,
            RequirementFactory requirementFactory, ILogger logger, bool globalLog,
            KeyActions? keyActions = null)
        {
            this.playerReader = addonReader.PlayerReader;
            this.costReader = addonReader.ActionBarCostReader;
            this.logger = logger;

            FormCost = GetMinCost;

            if (!globalLog)
                Log = false;

            ResetCharges();

            InitialiseSlot(logger);

            if (!string.IsNullOrEmpty(Requirement))
            {
                Requirements.Add(Requirement);
            }

            HasFormRequirement = !string.IsNullOrEmpty(Form);

            if (HasFormRequirement)
            {
                if (Enum.TryParse(Form, out Form desiredForm))
                {
                    this.FormEnum = desiredForm;
                    this.logger.LogInformation($"[{Name}] Required Form: {FormEnum.ToStringF()}");

                    if (!FormAction)
                    {
                        for (int i = 0; i < config.Form.Length; i++)
                        {
                            if (config.Form[i].FormEnum == FormEnum)
                            {
                                FormCost = config.Form[i].FormCost;
                            }
                        }
                    }
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

        public void InitialiseForm(ClassConfiguration config, AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger, bool globalLog)
        {
            FormAction = true;
            Initialise(config, addonReader, requirementFactory, logger, globalLog);

            logger.LogInformation($"[{Name}] Added {FormEnum} to FormCost with {MinCost}");
        }

        public void ResetCosts()
        {
            MinCost = 0;

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

        public bool CanDoFormChangeMinResource()
        {
            return playerReader.ManaCurrent() >= FormCost() + MinMana;
        }

        public void SetClicked(double offset = 0)
        {
            LastClicked[ConsoleKeyFormHash] = DateTime.UtcNow.AddMilliseconds(offset);
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

        public void ResetCharges()
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
                ActionBarCost abc = actionBarCostReader.GetCostByActionBarSlot(this, i);
                if (abc.Cost == 0)
                    continue;

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

                MinCost = abc.Cost;

                LogPowerCostChange(logger, Name, abc.PowerType.ToStringF(), abc.Cost, oldValue);
                if (HasFormRequirement && FormEnum != Core.Form.None)
                {
                    int formCost = FormCost();
                    if (formCost > 0)
                    {
                        logger.LogInformation($"[{Name}] +{formCost} Mana to change into {FormEnum.ToStringF()}");
                    }
                }
            }
            actionBarCostReader.OnActionCostChanged += ActionBarCostReader_OnActionCostChanged;
            actionBarCostReader.OnActionCostReset += ResetCosts;
        }

        private void ActionBarCostReader_OnActionCostChanged(object? sender, ActionBarCostEventArgs e)
        {
            if (Slot != e.Slot) return;

            MinCost = e.ActionBarCost.Cost;

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

        private int GetMinCost()
        {
            return MinCost;
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