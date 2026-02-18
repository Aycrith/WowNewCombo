namespace Core.AI.HybridDecision;

using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Core.GOAP;

/// <summary>
/// Serializes game state for LLM consumption.
/// </summary>
public sealed class GameStateSerializer
{
    private readonly JsonSerializerOptions jsonOptions;

    /// <summary>
    /// Creates a new game state serializer.
    /// </summary>
    public GameStateSerializer()
    {
        jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Serializes current game state to JSON.
    /// </summary>
    /// <returns>JSON representation of game state.</returns>
    public string SerializeState()
    {
        // TODO: Integrate with actual PlayerReader and game state
        // For now, return a placeholder structure
        var state = new GameStateSnapshot
        {
            Player = new PlayerState
            {
                HealthPercent = 100,
                ManaPercent = 100,
                Level = 60,
                Class = "Warrior",
                Position = new Vector3(0, 0, 0),
                InCombat = false,
                IsMoving = false,
                HasTarget = false
            },
            Target = null,
            Environment = new EnvironmentState
            {
                NearbyEnemies = 0,
                NearbyFriendlies = 0,
                Zone = "Unknown",
                SubZone = "Unknown"
            },
            Timestamp = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(state, jsonOptions);
    }

    /// <summary>
    /// Gets a hash of the current state for caching.
    /// </summary>
    /// <returns>Hash string representing current state.</returns>
    public string GetStateHash()
    {
        string stateJson = SerializeState();
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(stateJson));
        return Convert.ToHexString(hashBytes)[..16]; // Use first 16 chars for brevity
    }

    /// <summary>
    /// Snapshot of game state.
    /// </summary>
    public sealed class GameStateSnapshot
    {
        [JsonPropertyName("player")]
        public PlayerState Player { get; set; } = new();

        [JsonPropertyName("target")]
        public TargetState? Target { get; set; }

        [JsonPropertyName("environment")]
        public EnvironmentState Environment { get; set; } = new();

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Player state information.
    /// </summary>
    public sealed class PlayerState
    {
        [JsonPropertyName("healthPercent")]
        public int HealthPercent { get; set; }

        [JsonPropertyName("manaPercent")]
        public int ManaPercent { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("class")]
        public string Class { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public Vector3 Position { get; set; }

        [JsonPropertyName("inCombat")]
        public bool InCombat { get; set; }

        [JsonPropertyName("isMoving")]
        public bool IsMoving { get; set; }

        [JsonPropertyName("hasTarget")]
        public bool HasTarget { get; set; }
    }

    /// <summary>
    /// Target state information.
    /// </summary>
    public sealed class TargetState
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("healthPercent")]
        public int HealthPercent { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("isElite")]
        public bool IsElite { get; set; }

        [JsonPropertyName("isDead")]
        public bool IsDead { get; set; }

        [JsonPropertyName("distance")]
        public float Distance { get; set; }

        [JsonPropertyName("position")]
        public Vector3 Position { get; set; }
    }

    /// <summary>
    /// Environment state information.
    /// </summary>
    public sealed class EnvironmentState
    {
        [JsonPropertyName("nearbyEnemies")]
        public int NearbyEnemies { get; set; }

        [JsonPropertyName("nearbyFriendlies")]
        public int NearbyFriendlies { get; set; }

        [JsonPropertyName("zone")]
        public string Zone { get; set; } = string.Empty;

        [JsonPropertyName("subZone")]
        public string SubZone { get; set; } = string.Empty;
    }
}
