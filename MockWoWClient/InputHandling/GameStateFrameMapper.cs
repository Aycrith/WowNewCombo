using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using MockWoWClient.Contracts;
using MockWoWClient.GameState;
using MockWoWClient.Rendering;

namespace MockWoWClient.InputHandling;

/// <summary>
/// Maps game state to the 324 pixel frames for the bot to read.
/// This is the bridge between game state and pixel rendering.
/// </summary>
public sealed class GameStateFrameMapper
{
    private readonly GameStateManager _gameState;
    private readonly PixelGridRenderer _renderer;

    public GameStateFrameMapper(GameStateManager gameState, PixelGridRenderer renderer)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <summary>
    /// Updates all pixel frames based on current game state.
    /// Called every simulation tick before screen capture.
    /// </summary>
    public void UpdateFrames()
    {
        if (_renderer.IsConfigMode)
        {
            // In config mode, frames show their indices
            return;
        }

        UpdatePlayerFrames();
        UpdateTargetFrames();
        UpdateBitsFrames();
        UpdateActionBarFrames();
        UpdateMiscFrames();
    }

    private void UpdatePlayerFrames()
    {
        var player = _gameState.Player;

        // Player X (frame 1) - encoded as float * 10
        _renderer.SetFrameFloat(FrameIndices.PlayerX, player.Position.X / 10f);

        // Player Y (frame 2) - encoded as float * 10
        _renderer.SetFrameFloat(FrameIndices.PlayerY, player.Position.Y / 10f);

        // Direction (frame 3)
        _renderer.SetFrameFloat(FrameIndices.Direction, player.Direction);

        // Map ID (frame 4) - would need actual map ID from data files
        _renderer.SetFrame(FrameIndices.UIMapId, 141); // Example: Eastern Kingdoms

        // Level (frame 5)
        _renderer.SetFrame(FrameIndices.PlayerLevel, player.Level);

        // Health (frames 10-11)
        _renderer.SetFrame(FrameIndices.HealthMax, player.HealthMax);
        _renderer.SetFrame(FrameIndices.HealthCurrent, player.Health);

        // Power (frames 12-13)
        _renderer.SetFrame(FrameIndices.PowerMax, player.PowerMax);
        _renderer.SetFrame(FrameIndices.PowerCurrent, player.Power);

        // Experience (frames 50-51)
        _renderer.SetFrame(FrameIndices.XPCurrent, player.Experience);
        _renderer.SetFrame(FrameIndices.XPMax, player.ExperienceMax);

        // Race/Class/Version (frame 46)
        // RACE*10000 + CLASS*100 + VERSION
        int raceValue = GetRaceValue(player.Race);
        int classValue = GetClassValue(player.ClassName);
        int versionValue = 115; // Anniversary edition
        int raceClassVersion = (raceValue * 10000) + (classValue * 100) + versionValue;
        _renderer.SetFrame(FrameIndices.RaceClassVersion, raceClassVersion);

        // Money (frames 44-45)
        _renderer.SetFrame(FrameIndices.MoneyCopper, player.Copper % 10000);
        _renderer.SetFrame(FrameIndices.MoneyGold, player.Gold);
    }

    private void UpdateTargetFrames()
    {
        var target = _gameState.CurrentTarget;

        if (target == null)
        {
            // Clear target frames
            _renderer.SetFrame(FrameIndices.TargetNamePart1, 0);
            _renderer.SetFrame(FrameIndices.TargetNamePart2, 0);
            _renderer.SetFrame(FrameIndices.TargetHealth, 0);
            return;
        }

        // Target name (frames 16-17) - up to 6 characters
        string name = target.Name.Length > 6 ? target.Name[..6] : target.Name;
        _renderer.SetFrameString(FrameIndices.TargetNamePart1, name[..Math.Min(3, name.Length)]);
        if (name.Length > 3)
        {
            _renderer.SetFrameString(FrameIndices.TargetNamePart2, name[3..]);
        }

        // Target health (frame 18)
        _renderer.SetFrame(FrameIndices.TargetHealth, target.Health);

        // Target level + classification (frame 43)
        int levelClass = (target.Level * 100) + (int)target.Classification;
        _renderer.SetFrame(43, levelClass);
    }

    private void UpdateBitsFrames()
    {
        var player = _gameState.Player;
        var target = _gameState.CurrentTarget;

        // Cell 8 (BitsCell1) - first 24 bits
        Span<bool> bits1 = stackalloc bool[24];
        bits1[AddonBitFlags.TargetCombat] = target?.IsInCombat ?? false;
        bits1[AddonBitFlags.TargetDead] = target?.IsDead ?? false;
        bits1[AddonBitFlags.PlayerDead] = player.IsDead;
        bits1[AddonBitFlags.MouseOver] = false; // Not implemented
        bits1[AddonBitFlags.TargetHostile] = target?.IsHostile ?? false;
        bits1[AddonBitFlags.HasPet] = false; // Not implemented
        bits1[AddonBitFlags.ItemsBroken] = false; // Not implemented
        bits1[AddonBitFlags.OnTaxi] = false; // Not implemented
        bits1[AddonBitFlags.Swimming] = player.IsSwimming;
        bits1[AddonBitFlags.InCombat] = player.InCombat;
        bits1[AddonBitFlags.HasTarget] = player.HasTarget;
        bits1[AddonBitFlags.Mounted] = player.IsMounted;
        bits1[AddonBitFlags.AutoAttack] = player.IsAutoAttacking;
        bits1[AddonBitFlags.TargetPlayer] = target?.IsPlayerControlled ?? false;
        bits1[AddonBitFlags.Falling] = player.IsFalling;
        _renderer.SetFrameBits(FrameIndices.BitsCell1, bits1);

        // Cell 9 (BitsCell2) - next 24 bits
        Span<bool> bits2 = stackalloc bool[24];
        bits2[AddonBitFlags.CorpseInRange - 24] = _gameState.GetNearestLootableCorpse(10f) != null;
        bits2[AddonBitFlags.Indoors - 24] = false; // Not implemented
        bits2[AddonBitFlags.Stealthed - 24] = player.IsStealthed;
        bits2[AddonBitFlags.AutoFollow - 24] = false; // Not implemented
        bits2[AddonBitFlags.Flying - 24] = player.IsFlying;
        bits2[AddonBitFlags.Moving - 24] = player.IsMoving;
        _renderer.SetFrameBits(FrameIndices.BitsCell2, bits2);

        // Cell 100 (BitsCell3) - additional bits
        Span<bool> bits3 = stackalloc bool[12];
        bits3[7] = player.IsCasting; // Channeling bit
        _renderer.SetFrameBits(FrameIndices.BitsCell3, bits3);
    }

    private void UpdateActionBarFrames()
    {
        var player = _gameState.Player;

        // Update action bar states (frames 25-34)
        for (int i = 0; i < FrameIndices.ActionBarCount; i++)
        {
            int slot = i;
            if (slot < player.ActionBars.Length)
            {
                var actionBar = player.ActionBars[slot];
                
                // Encode action bar state: slot * 100000 + usable + cost
                int usableFlag = actionBar.IsUsable ? 1 : 0;
                int inRangeFlag = actionBar.InRange ? 1 : 0;
                int value = (slot * 100000) + (usableFlag * 10000) + (inRangeFlag * 1000) + actionBar.Cost;
                
                _renderer.SetFrame(FrameIndices.ActionBarStart + i, value);
            }
        }
    }

    private void UpdateMiscFrames()
    {
        var player = _gameState.Player;

        // Casting spell ID (frame 53)
        _renderer.SetFrame(FrameIndices.CastingSpellId, player.CastingSpellId);

        // Remaining cast time (frame 76)
        _renderer.SetFrame(FrameIndices.RemainingCastTime, player.RemainingCastTimeMs);

        // GCD remaining (frame 95)
        // Calculate GCD based on action bar cooldowns
        int gcdRemaining = player.ActionBars.Min(a => a.CooldownRemaining);
        _renderer.SetFrame(FrameIndices.GcdRemaining, Math.Max(0, gcdRemaining));

        // Network latency (frame 96) - simulated
        _renderer.SetFrame(FrameIndices.NetworkLatency, 50); // 50ms

        // Loot window (frame 97)
        int lootCount = _gameState.Corpses.Count(c => !c.HasBeenLooted);
        _renderer.SetFrame(FrameIndices.LootWindow, lootCount);

        // Update global tick counter (frame 322)
        // This is handled by PixelGridRenderer.CaptureScreen()
    }

    private static int GetRaceValue(string race)
    {
        return race.ToLowerInvariant() switch
        {
            "human" => 1,
            "orc" => 2,
            "dwarf" => 3,
            "nightelf" => 4,
            "undead" => 5,
            "tauren" => 6,
            "gnome" => 7,
            "troll" => 8,
            "goblin" => 9,
            "bloodelf" => 10,
            "draenei" => 11,
            "worgen" => 22,
            "pandaren" => 24,
            _ => 1 // Default to human
        };
    }

    private static int GetClassValue(string className)
    {
        return className.ToLowerInvariant() switch
        {
            "warrior" => 1,
            "paladin" => 2,
            "hunter" => 3,
            "rogue" => 4,
            "priest" => 5,
            "deathknight" => 6,
            "shaman" => 7,
            "mage" => 8,
            "warlock" => 9,
            "monk" => 10,
            "druid" => 11,
            "demonhunter" => 12,
            "evoker" => 13,
            _ => 1 // Default to warrior
        };
    }
}
