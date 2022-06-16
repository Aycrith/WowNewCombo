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
        public int PressDuration { get; set; } = 50;
        public string Form { get; set; } = string.Empty;
        public Form FormEnum { get; set; } = Core.Form.None;
        public float Cooldown { get; set; }

        private int _charge;
        public int Charge { get; set; } = 1;
        public SchoolMask School { get; set; } = SchoolMask.None;
        public int MinMana { get; set; }
        public int MinRage { get; set; }
        public int MinEnergy { get; set; }
        public int MinComboPoints { get; set; }

        public string Requirement { get; set; } = string.Empty;
        public List<string> Requirements { get; } = new();

        public bool WhenUsable { get; set; }

        public bool WaitForWithinMeleeRange { get; set; }
        public bool ResetOnNewTarget { get; set; }

        public bool Log { get; set; } = true;
        public int DelayAfterCast { get; set; } = 1450; // GCD 1500 - but spell queue window 400 ms

        public bool WaitForGCD { get; set; } = true;

        public bool SkipValidation { get; set; }

        public bool AfterCastWaitBuff { get; set; }

        public bool AfterCastWaitNextSwing { get; set; }

        public bool DelayUntilCombat { get; set; }
        public int DelayBeforeCast { get; set; }
        public float Cost { get; set; } = 18;
        public string InCombat { get; set; } = "false";

        public bool? UseWhenTargetIsCasting { get; set; }

        public string PathFilename { get; set; } = string.Empty;
        public List<Vector3> Path { get; } = new();

        public int StepBackAfterCast { get; set; }

        public Vector3 LastClickPostion { get; private set; }

        public List<Requirement> RequirementObjects { get; } = new();

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

                    if (KeyReader.ActionBarSlotMap.TryGetValue(Key, out int slot))
                    {
                        int offset = Stance.RuntimeSlotToActionBar(this, playerReader, slot);
                        this.logger.LogInformation($"[{Name}] Actionbar Form key map: Key:{Key} -> Actionbar:{slot} -> Form Map:{slot + offset}");
                    }
                }
                else
                {
                    throw new Exception($"[{Name}] Unknown form: {Form}");
                }
            }

            ConsoleKeyFormHash = ((int)FormEnum * 1000) + (int)ConsoleKey;
            ResetCooldown();

            if (Slot > 0)
                InitMinPowerType(playerReader, addonReader.ActionBarCostReader);

            requirementFactory.InitialiseRequirements(this, keyActions);
        }

        public void Dispose()
        {
            costReader.OnActionCostChanged -= ActionBarCostReader_OnActionCostChanged;
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
            return !this.RequirementObjects.Any(r => !r.HasRequirement());
        }

        public bool HasFormRequirement()
        {
            return !string.IsNullOrEmpty(Form);
        }

        private void InitMinPowerType(PlayerReader playerReader, ActionBarCostReader actionBarCostReader)
        {
            ActionBarCost abc = actionBarCostReader.GetCostByActionBarSlot(playerReader, this);
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

            actionBarCostReader.OnActionCostChanged += ActionBarCostReader_OnActionCostChanged;
        }

        private void ActionBarCostReader_OnActionCostChanged(object? sender, ActionBarCostEventArgs e)
        {
            int slot = Slot + Stance.RuntimeSlotToActionBar(this, playerReader, Slot);
            if (slot != e.Index)
                return;

            int oldValue = 0;
            switch (e.PowerType)
            {
                case PowerType.Mana:
                    oldValue = MinMana;
                    MinMana = e.Cost;
                    break;
                case PowerType.Rage:
                    oldValue = MinRage;
                    MinRage = e.Cost;
                    break;
                case PowerType.Energy:
                    oldValue = MinEnergy;
                    MinEnergy = e.Cost;
                    break;
            }

            if (e.Cost != oldValue)
            {
                LogPowerCostChange(logger, Name, e.PowerType.ToStringF(), e.Cost, oldValue);
            }
        }

        #region Logging

        public void LogInformation(string message)
        {
            logger.LogInformation($"[{Name}]: {message}");
        }

        public void LogWarning(string message)
        {
            logger.LogWarning($"[{Name}]: {message}");
        }

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Information,
            Message = "[{name}] Update {type} cost to {newCost} from {oldCost}")]
        static partial void LogPowerCostChange(ILogger logger, string name, string type, int newCost, int oldCost);

        #endregion
    }
}