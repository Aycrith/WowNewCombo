using Core.Database;
using Core.GOAP;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Data;
using SharedLib.Extensions;

using System;
using System.Numerics;

namespace Core.Goals;

public sealed partial class WalkToCorpseGoal : GoapGoal, IGoapEventListener, IRouteProvider, IDisposable
{
    public override float Cost => 1f;

    private readonly ILogger<WalkToCorpseGoal> logger;
    private readonly Wait wait;
    private readonly ConfigurableInput input;

    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly Navigation navigation;
    private readonly StopMoving stopMoving;
    private readonly ExecGameCommand execGameCommand;

    private readonly AreaDB areaDB;

    private DateTime onEnterTime;
    private DateTime nextCorpseRetrieveAttemptUtc;
    private DateTime nextSpiritHealerInteractUtc;

    private const int CorpseWalkTimeoutSec = 90;
    private const int CorpseNoProgressEscalationSec = 25;
    private const int CorpseRetrieveRetryMs = 3000;
    private const int SpiritHealerInteractRetryMs = 5000;
    private const int SpiritHealerEscalationRetryMs = 12000;
    private const float SpiritHealerInteractDistanceYards = 7f;
    private const float SpiritHealerInteractFallbackDistanceYards = 35f;
    private const float SpiritHealerLookupMaxDistanceYards = 500f;
    private const float CorpseRetrieveConfirmDistanceYards = 35f;
    private const float CorpseProgressDeltaYards = 1.5f;
    private bool navigatedToSpiritHealer;
    private Vector3 spiritHealerWorldPos;
    private string spiritHealerName = string.Empty;
    private bool spiritHealerLookupWasSuspicious;
    private Vector3 lastProgressWorldPos;
    private DateTime lastProgressUtc;
    private DateTime nextNoProgressEscalationUtc;

    #region IRouteProvider

    public DateTime LastActive => navigation.LastActive;

    public Vector3[] MapRoute()
    {
        return Array.Empty<Vector3>();
    }


    public Vector3[] PathingRoute()
    {
        return navigation.TotalRoute;
    }

    public bool HasNext()
    {
        return navigation.HasNext();
    }

    public Vector3 NextMapPoint()
    {
        return navigation.NextMapPoint();
    }

    #endregion

    public WalkToCorpseGoal(ILogger<WalkToCorpseGoal> logger,
        ConfigurableInput input, Wait wait,
        PlayerReader playerReader, AddonBits bits,
        Navigation navigation, StopMoving stopMoving, AreaDB areaDB,
        ExecGameCommand execGameCommand)
        : base(nameof(WalkToCorpseGoal))
    {
        this.logger = logger;
        this.wait = wait;
        this.input = input;

        this.playerReader = playerReader;
        this.bits = bits;

        this.stopMoving = stopMoving;

        this.navigation = navigation;

        this.areaDB = areaDB;
        this.execGameCommand = execGameCommand;

        AddPrecondition(GoapKey.isdead, true);
    }

    public void Dispose()
    {
        navigation.Dispose();
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e.GetType() == typeof(ResumeEvent))
        {
            navigation.ResetStuckParameters();
        }
    }

    public override void OnEnter()
    {
        playerReader.WorldPosZ = 0;

        wait.While(AliveOrLoadingScreen);

        Vector3 playerWorldPos = playerReader.WorldPos;
        (Creature npc, Vector3 worldPos)
            = areaDB.FindClosestCreatureByNpcFlag(NpcFlags.SpiritHealer, playerWorldPos);

        float spiritHealerLookupDistance = worldPos.WorldDistanceXYTo(playerWorldPos);
        if (worldPos == default || spiritHealerLookupDistance > SpiritHealerLookupMaxDistanceYards)
        {
            logger.LogWarning(
                "Spirit healer lookup returned suspicious target {SpiritHealerPos} ({Distance:F1}y from player). Using player ghost position fallback {PlayerPos}",
                worldPos,
                spiritHealerLookupDistance,
                playerWorldPos);

            worldPos = new Vector3(playerWorldPos.X, playerWorldPos.Y, worldPos.Z);
            spiritHealerLookupWasSuspicious = true;
        }
        else
        {
            spiritHealerLookupWasSuspicious = false;
        }

        Log($"Player teleported to the graveyard! {worldPos}");

        playerReader.WorldPosZ = worldPos.Z;
        spiritHealerWorldPos = worldPos;
        spiritHealerName = spiritHealerLookupWasSuspicious ? string.Empty : (npc.Name ?? string.Empty);
        navigatedToSpiritHealer = false;

        Vector3 corpseLocation = playerReader.CorpseMapPos;
        Log($"Corpse location is {corpseLocation}");

        Vector3 corpseWorld = WorldMapAreaDB.ToWorld_FlipXY(corpseLocation, playerReader.WorldMapArea);
        float corpseDistanceYards = playerReader.WorldPos.WorldDistanceXYTo(corpseWorld);
        logger.LogInformation(
            "Corpse run: ghost at {GhostPos} -> corpse at map {CorpseMap} (world {CorpseWorld}), distance ~{Distance:F0}y, Z hint={ZHint:F1}",
            playerReader.WorldPos,
            corpseLocation,
            corpseWorld,
            corpseDistanceYards,
            worldPos.Z);

        bool corpseOutOfBounds =
            corpseLocation.X <= 0 || corpseLocation.X > 100 ||
            corpseLocation.Y <= 0 || corpseLocation.Y > 100;

        if (corpseOutOfBounds)
        {
            Log($"Corpse ({corpseLocation}) is outside zone bounds - routing to spirit healer at {worldPos}");
            navigatedToSpiritHealer = true;
            navigation.SetWayPoints([worldPos]);
        }
        else
        {
            navigation.SetWayPoints([corpseLocation]);
        }

        onEnterTime = DateTime.UtcNow;
        nextCorpseRetrieveAttemptUtc = DateTime.UtcNow;
        nextSpiritHealerInteractUtc = DateTime.UtcNow;
        nextNoProgressEscalationUtc = DateTime.UtcNow;
        lastProgressWorldPos = playerReader.WorldPos;
        lastProgressUtc = DateTime.UtcNow;
    }

    public override void OnExit()
    {
        navigation.StopMovement();
        navigation.Stop();
    }

    public override void Update()
    {
        TrackMovementProgress();

        if (!IsCorpseInRetrieveRange())
        {
            RandomJump();

            if (!navigatedToSpiritHealer &&
                IsDeadNoProgressEscalationDue() &&
                (DateTime.UtcNow - onEnterTime).TotalSeconds > 10)
            {
                Log($"No corpse progress for {CorpseNoProgressEscalationSec}s - escalating early to spirit healer route at {spiritHealerWorldPos}");
                navigatedToSpiritHealer = true;
                navigation.SetWayPoints([spiritHealerWorldPos]);
                nextNoProgressEscalationUtc = DateTime.UtcNow.AddMilliseconds(SpiritHealerEscalationRetryMs);
            }

            if (IsNearSpiritHealerForInteraction())
            {
                stopMoving.Stop();
                navigation.ResetStuckParameters();
                TryInteractSpiritHealer();
                wait.Update();
                return;
            }

            if (!navigatedToSpiritHealer &&
                (DateTime.UtcNow - onEnterTime).TotalSeconds > CorpseWalkTimeoutSec)
            {
                Log($"Corpse not reached after {CorpseWalkTimeoutSec}s - routing to spirit healer at {spiritHealerWorldPos}");
                navigatedToSpiritHealer = true;
                navigation.SetWayPoints([spiritHealerWorldPos]);
            }

            if (navigatedToSpiritHealer && IsDeadNoProgressEscalationDue())
            {
                Log($"No progress while routing to spirit healer - forcing interaction retry");
                ForceSpiritHealerInteractionFallback();
                nextNoProgressEscalationUtc = DateTime.UtcNow.AddMilliseconds(SpiritHealerEscalationRetryMs);
            }

            TryInteractSpiritHealer();
            navigation.Update();
        }
        else
        {
            stopMoving.Stop();
            navigation.ResetStuckParameters();
            TryRetrieveCorpse();
        }

        wait.Update();
    }

    private void TrackMovementProgress()
    {
        Vector3 pos = playerReader.WorldPos;
        if (pos.WorldDistanceXYTo(lastProgressWorldPos) >= CorpseProgressDeltaYards)
        {
            lastProgressWorldPos = pos;
            lastProgressUtc = DateTime.UtcNow;
        }
    }

    private bool IsDeadNoProgressEscalationDue()
    {
        return bits.Dead() &&
            DateTime.UtcNow >= nextNoProgressEscalationUtc &&
            (DateTime.UtcNow - lastProgressUtc).TotalSeconds >= CorpseNoProgressEscalationSec;
    }

    private void RandomJump()
    {
        if ((DateTime.UtcNow - onEnterTime).TotalSeconds > 5 &&
            input.Jump.SinceLastClickMs > Random.Shared.Next(10_000, 25_000))
        {
            Log("Random jump");
            input.PressJump();
        }
    }

    private void TryRetrieveCorpse()
    {
        if (!bits.Dead() || DateTime.UtcNow < nextCorpseRetrieveAttemptUtc)
        {
            return;
        }

        nextCorpseRetrieveAttemptUtc = DateTime.UtcNow.AddMilliseconds(CorpseRetrieveRetryMs);

        Log("Corpse in range - attempting corpse retrieval");
        execGameCommand.Run("/run RetrieveCorpse()", logMessage: null);
    }

    private bool IsCorpseInRetrieveRange()
    {
        if (!bits.CorpseInRange())
        {
            return false;
        }

        Vector3 corpseMapPos = playerReader.CorpseMapPos;
        if (corpseMapPos == Vector3.Zero)
        {
            return true;
        }

        Vector3 corpseWorldPos = WorldMapAreaDB.ToWorld_FlipXY(corpseMapPos, playerReader.WorldMapArea);
        if (corpseWorldPos == default)
        {
            return true;
        }

        float corpseDistance = playerReader.WorldPos.WorldDistanceXYTo(corpseWorldPos);
        if (!float.IsFinite(corpseDistance))
        {
            return true;
        }

        if (corpseDistance <= CorpseRetrieveConfirmDistanceYards)
        {
            return true;
        }

        logger.LogDebug(
            "CorpseInRange bit reported true but corpse is still {Distance:F1}y away; continuing corpse navigation",
            corpseDistance);
        return false;
    }

    private void TryInteractSpiritHealer()
    {
        if (!navigatedToSpiritHealer || DateTime.UtcNow < nextSpiritHealerInteractUtc || !bits.Dead())
        {
            return;
        }

        float interactDistanceThreshold = spiritHealerLookupWasSuspicious
            ? SpiritHealerInteractFallbackDistanceYards
            : SpiritHealerInteractDistanceYards;

        float distanceToSpiritHealer = playerReader.WorldPos.WorldDistanceXYTo(spiritHealerWorldPos);
        if (distanceToSpiritHealer > interactDistanceThreshold)
        {
            return;
        }

        nextSpiritHealerInteractUtc = DateTime.UtcNow.AddMilliseconds(SpiritHealerInteractRetryMs);

        Log($"Near spirit healer ({distanceToSpiritHealer:F1}y, threshold {interactDistanceThreshold:F1}) - attempting interaction");
        TryTargetAndInteractSpiritHealer();
        input.PressInteract();
        wait.Update();
        TryConfirmSpiritHealerResurrection();
        execGameCommand.Run("/run RetrieveCorpse()", logMessage: null);
    }

    private bool IsNearSpiritHealerForInteraction()
    {
        if (!navigatedToSpiritHealer || !bits.Dead())
        {
            return false;
        }

        float interactDistanceThreshold = spiritHealerLookupWasSuspicious
            ? SpiritHealerInteractFallbackDistanceYards
            : (SpiritHealerInteractDistanceYards + 1.5f);

        float distanceToSpiritHealer = playerReader.WorldPos.WorldDistanceXYTo(spiritHealerWorldPos);
        return distanceToSpiritHealer <= interactDistanceThreshold;
    }

    private void TryTargetAndInteractSpiritHealer()
    {
        if (!string.IsNullOrWhiteSpace(spiritHealerName))
        {
            execGameCommand.Run($"/targetexact {spiritHealerName}", logMessage: null);
            wait.Update();
        }

        if (!bits.Target())
        {
            Log("Spirit healer target not acquired by name - trying nearest target fallback");
            input.PressNearestTarget();
            wait.Update();
        }

        execGameCommand.Run("/run if UnitExists('target') then InteractUnit('target') end", logMessage: null);
        wait.Update();
    }

    private void ForceSpiritHealerInteractionFallback()
    {
        // When corpse-run pathing stalls for an extended period while dead, push a best-effort
        // spirit healer interaction sequence instead of remaining in turn-only loops.
        execGameCommand.Run("/targetexact Spirit Healer", logMessage: null);
        wait.Update();

        if (!bits.Target())
        {
            input.PressNearestTarget();
            wait.Update();
        }

        input.PressInteract();
        wait.Update();

        TryConfirmSpiritHealerResurrection();
        execGameCommand.Run("/run RetrieveCorpse()", logMessage: null);
    }

    private void TryConfirmSpiritHealerResurrection()
    {
        const string clickSpiritPopup =
            "/run if StaticPopup1 and StaticPopup1:IsShown() and StaticPopup1Button1 then StaticPopup1Button1:Click() end";

        execGameCommand.Run(clickSpiritPopup, logMessage: null);
        wait.Update();
    }

    private bool AliveOrLoadingScreen()
    {
        return bits.Dead() && playerReader.CorpseMapPos == Vector3.Zero;
    }

    private void Log(string text)
    {
        logger.LogInformation(text);
    }
}
