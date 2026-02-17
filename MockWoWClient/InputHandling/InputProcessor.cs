using MockWoWClient.GameState;
using System.Linq;
using System.Numerics;

namespace MockWoWClient.InputHandling;

/// <summary>
/// Interface for receiving input from the bot.
/// </summary>
public interface IInputReceiver
{
    void KeyDown(int keyCode);
    void KeyUp(int keyCode);
    void MouseMove(int x, int y);
    void MouseClick(int button, int x, int y);
}

/// <summary>
/// Processes input from the bot and updates game state.
/// </summary>
public sealed class InputProcessor : IInputReceiver
{
    private readonly GameStateManager _gameState;
    private readonly InputQueue _inputQueue;
    private readonly Dictionary<int, KeyBinding> _keyBindings;
    private readonly ChatCommandHandler _chatHandler;

    // Movement keys (Virtual Key Codes)
    public const int VK_W = 0x57;
    public const int VK_A = 0x41;
    public const int VK_S = 0x53;
    public const int VK_D = 0x44;
    public const int VK_UP = 0x26;
    public const int VK_DOWN = 0x28;
    public const int VK_LEFT = 0x25;
    public const int VK_RIGHT = 0x27;
    public const int VK_SPACE = 0x20;
    public const int VK_TAB = 0x09;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_RETURN = 0x0D;
    public const int VK_INSERT = 0x2D;
    public const int VK_DELETE = 0x2E;
    public const int VK_HOME = 0x24;
    public const int VK_END = 0x23;
    public const int VK_PRIOR = 0x21;  // Page Up
    public const int VK_NEXT = 0x22;   // Page Down
    public const int VK_NUMPAD1 = 0x61;
    public const int VK_NUMPAD2 = 0x62;
    public const int VK_NUMPAD3 = 0x63;
    public const int VK_NUMPAD4 = 0x64;
    public const int VK_NUMPAD5 = 0x65;
    public const int VK_NUMPAD6 = 0x66;
    public const int VK_NUMPAD7 = 0x67;
    public const int VK_NUMPAD8 = 0x68;
    public const int VK_NUMPAD9 = 0x69;
    public const int VK_NUMPAD0 = 0x60;
    public const int VK_MULTIPLY = 0x6A;
    public const int VK_SUBTRACT = 0x6D;

    // Modifiers
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;  // Alt

    // Number keys
    public const int VK_0 = 0x30;
    public const int VK_1 = 0x31;
    public const int VK_2 = 0x32;
    public const int VK_3 = 0x33;
    public const int VK_4 = 0x34;
    public const int VK_5 = 0x35;
    public const int VK_6 = 0x36;
    public const int VK_7 = 0x37;
    public const int VK_8 = 0x38;
    public const int VK_9 = 0x39;

    // Letter key range
    public const int VK_Z = 0x5A;

    // Function keys
    public const int VK_F1 = 0x70;
    public const int VK_F12 = 0x7B;

    // Movement speed (units per second)
    public float MovementSpeed { get; set; } = 7.0f;
    public float TurnSpeed { get; set; } = 180.0f; // degrees per second

    public InputProcessor(GameStateManager gameState)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _inputQueue = new InputQueue();
        _keyBindings = new Dictionary<int, KeyBinding>();
        _chatHandler = new ChatCommandHandler(gameState);

        InitializeDefaultKeyBindings();
    }

    /// <summary>
    /// Gets the input queue for processing.
    /// </summary>
    public InputQueue Queue => _inputQueue;

    public void KeyDown(int keyCode)
    {
        _inputQueue.Enqueue(new InputEvent(keyCode, InputAction.KeyDown));
    }

    public void KeyUp(int keyCode)
    {
        _inputQueue.Enqueue(new InputEvent(keyCode, InputAction.KeyUp));
    }

    public void MouseMove(int x, int y)
    {
        _inputQueue.Enqueue(new InputEvent(0, InputAction.MouseMove, x, y));
    }

    public void MouseClick(int button, int x, int y)
    {
        var action = button == 0 ? InputAction.MouseLeftClick : InputAction.MouseRightClick;
        _inputQueue.Enqueue(new InputEvent(0, action, x, y));
    }

    /// <summary>
    /// Processes all queued inputs.
    /// Called every simulation tick.
    /// </summary>
    public void ProcessFrame(TimeSpan deltaTime)
    {
        int eventCount = _inputQueue.Count;
        if (eventCount == 0)
        {
            return;
        }

        // Distribute deltaTime evenly across all events to prevent
        // movement speed amplification when multiple events are queued
        TimeSpan eventDeltaTime = TimeSpan.FromTicks(deltaTime.Ticks / eventCount);

        while (_inputQueue.TryDequeue(out InputEvent evt))
        {
            ProcessInput(evt, eventDeltaTime);
        }
    }

    private void ProcessInput(InputEvent evt, TimeSpan deltaTime)
    {
        switch (evt.Action)
        {
            case InputAction.KeyDown:
                HandleKeyDown(evt.KeyCode, deltaTime);
                break;
            case InputAction.KeyUp:
                HandleKeyUp(evt.KeyCode);
                break;
            case InputAction.MouseMove:
                HandleMouseMove(evt.MouseX, evt.MouseY);
                break;
            case InputAction.MouseLeftClick:
                HandleMouseClick(leftButton: true);
                break;
            case InputAction.MouseRightClick:
                HandleMouseClick(leftButton: false);
                break;
        }
    }

    private void HandleKeyDown(int keyCode, TimeSpan deltaTime)
    {
        var player = _gameState.Player;

        // Check for chat commands (keys 0-9 and letters)
        if (keyCode >= VK_0 && keyCode <= VK_Z)
        {
            char c = (char)keyCode;
            if (_chatHandler.TryHandleChatInput(c))
            {
                return;
            }
        }

        // Movement
        switch (keyCode)
        {
            case VK_W:
            case VK_UP:
                MoveForward(deltaTime);
                break;
            case VK_S:
            case VK_DOWN:
                MoveBackward(deltaTime);
                break;
            case VK_A:
            case VK_LEFT:
                TurnLeft(deltaTime);
                break;
            case VK_D:
            case VK_RIGHT:
                TurnRight(deltaTime);
                break;
            case VK_SPACE:
                Jump();
                break;
            case VK_TAB:
                TargetNearestEnemy();
                break;
            case VK_ESCAPE:
                ClearTarget();
                break;
        }

        // Action bar keys
        if ((keyCode >= VK_1 && keyCode <= VK_9) || keyCode == VK_0)
        {
            int slot = keyCode == VK_0 ? 9 : keyCode - VK_1;
            UseActionBarSlot(slot);
        }

        // Function keys (bottom left action bar)
        if (keyCode >= VK_F1 && keyCode <= VK_F12)
        {
            int slot = 60 + (keyCode - VK_F1); // Slots 61-72
            UseActionBarSlot(slot);
        }

        // Check custom key bindings
        if (_keyBindings.TryGetValue(keyCode, out var binding))
        {
            binding.Action?.Invoke();
        }
    }

    private void HandleKeyUp(int keyCode)
    {
        // Movement keys released
        switch (keyCode)
        {
            case VK_W:
            case VK_A:
            case VK_S:
            case VK_D:
            case VK_UP:
            case VK_DOWN:
            case VK_LEFT:
            case VK_RIGHT:
                _gameState.Player.IsMoving = false;
                break;
        }
    }

    private static void HandleMouseMove(int x, int y)
    {
        // Mouse movement for camera/aiming
        // Not implemented in basic version
        _ = x; // Suppress unused parameter warning
        _ = y;
    }

    private void HandleMouseClick(bool leftButton)
    {
        if (leftButton)
        {
            // Left click - interact with target
            InteractWithTarget();
        }
        else
        {
            // Right click - turn to face mouse
            // Not implemented in basic version
        }
    }

    private void MoveForward(TimeSpan deltaTime)
    {
        var player = _gameState.Player;
        float distance = (float)(MovementSpeed * deltaTime.TotalSeconds);

        // Calculate new position based on direction
        float radians = MathF.PI * player.Direction / 180f;
        player.Position += new Vector3(
            MathF.Sin(radians) * distance,
            MathF.Cos(radians) * distance,
            0);

        player.IsMoving = true;
        player.State = EntityState.Moving;
    }

    private void MoveBackward(TimeSpan deltaTime)
    {
        var player = _gameState.Player;
        float distance = (float)(MovementSpeed * deltaTime.TotalSeconds * 0.5f); // Slower backward

        float radians = MathF.PI * (player.Direction + 180) / 180f;
        player.Position += new Vector3(
            MathF.Sin(radians) * distance,
            MathF.Cos(radians) * distance,
            0);

        player.IsMoving = true;
    }

    private void TurnLeft(TimeSpan deltaTime)
    {
        var player = _gameState.Player;
        float turnAmount = (float)(TurnSpeed * deltaTime.TotalSeconds);
        player.Direction = (player.Direction - turnAmount) % 360;
        if (player.Direction < 0) player.Direction += 360;
    }

    private void TurnRight(TimeSpan deltaTime)
    {
        var player = _gameState.Player;
        float turnAmount = (float)(TurnSpeed * deltaTime.TotalSeconds);
        player.Direction = (player.Direction + turnAmount) % 360;
    }

    private void Jump()
    {
        _gameState.Player.IsFalling = true;
        // In a full implementation, this would trigger a jump animation
        // and the player would briefly rise then fall back down
    }

    private void TargetNearestEnemy()
    {
        var nearest = _gameState.GetNearestHostileNpc(maxDistance: 50f);
        if (nearest != null)
        {
            var target = new TargetEntity
            {
                Id = nearest.Id,
                Name = nearest.Name,
                Level = nearest.Level,
                Health = nearest.Health,
                HealthMax = nearest.HealthMax,
                Position = nearest.Position,
                IsHostile = nearest.IsHostile,
                IsTagged = nearest.IsTagged,
                IsPlayerControlled = false
            };

            _gameState.SetTarget(target);
        }
    }

    private void ClearTarget()
    {
        _gameState.ClearTarget();
    }

    private void InteractWithTarget()
    {
        var target = _gameState.CurrentTarget;
        if (target == null)
            return;

        // Check if target is lootable (dead)
        if (target.IsDead)
        {
            // Find the corpse
            var corpse = _gameState.Corpses.FirstOrDefault(c => c.NpcName == target.Name);
            if (corpse != null)
            {
                _gameState.LootCorpse(corpse);
            }
        }
    }

    private void UseActionBarSlot(int slot)
    {
        if (slot < 0 || slot >= _gameState.Player.ActionBars.Length)
            return;

        var actionBar = _gameState.Player.ActionBars[slot];
        if (!actionBar.IsUsable || actionBar.CooldownRemaining > 0)
            return;

        // Cast the spell/ability
        // In a full implementation, this would check spell requirements and effects
        actionBar.CooldownRemaining = 1500; // 1.5s GCD

        // Apply spell effect
        if (_gameState.CurrentTarget != null && !_gameState.CurrentTarget.IsDead)
        {
            // Simple auto-attack damage
            int damage = 10;
            _gameState.CurrentTarget.TakeDamage(damage);

            // Also damage the original NPC (TargetEntity is a copy)
            var originalNpc = _gameState.Npcs.FirstOrDefault(n => n.Id == _gameState.CurrentTarget.Id);
            if (originalNpc != null)
            {
                originalNpc.TakeDamage(damage);
            }

            _gameState.RecordCombatAction(actionBar.SpellName ?? "Auto Attack", damage);

            if (!_gameState.InCombat)
            {
                _gameState.StartCombat();
            }
        }
    }

    private void InitializeDefaultKeyBindings()
    {
        // Custom action keys (using Alt modifier)
        // These are typically set up by the addon

        // Stop Attack: Alt-Delete
        _keyBindings[VK_DELETE] = new KeyBinding
        {
            Name = "StopAttack",
            Action = () =>
            {
                _gameState.Player.IsAutoAttacking = false;
            }
        };

        // Clear Target: Alt-Insert
        _keyBindings[VK_INSERT] = new KeyBinding
        {
            Name = "ClearTarget",
            Action = ClearTarget
        };

        // Toggle Config Mode: Shift-PageUp
        _keyBindings[VK_PRIOR] = new KeyBinding
        {
            Name = "ToggleConfig",
            Action = () =>
            {
                // This would toggle the mock client's config mode
            }
        };
    }

    public void RegisterKeyBinding(int keyCode, KeyBinding binding)
    {
        _keyBindings[keyCode] = binding;
    }
}

/// <summary>
/// Represents a queued input event.
/// </summary>
public readonly struct InputEvent
{
    public int KeyCode { get; }
    public InputAction Action { get; }
    public int MouseX { get; }
    public int MouseY { get; }

    public InputEvent(int keyCode, InputAction action, int mouseX = 0, int mouseY = 0)
    {
        KeyCode = keyCode;
        Action = action;
        MouseX = mouseX;
        MouseY = mouseY;
    }
}

/// <summary>
/// Input action types.
/// </summary>
public enum InputAction
{
    KeyDown,
    KeyUp,
    MouseMove,
    MouseLeftClick,
    MouseRightClick
}

/// <summary>
/// Thread-safe input queue.
/// </summary>
public sealed class InputQueue
{
    private readonly Queue<InputEvent> _queue = new();
    private readonly object _lock = new();

    public void Enqueue(InputEvent evt)
    {
        lock (_lock)
        {
            _queue.Enqueue(evt);
        }
    }

    public bool TryDequeue(out InputEvent evt)
    {
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                evt = _queue.Dequeue();
                return true;
            }
        }

        evt = default;
        return false;
    }

    public int Count
    {
        get
        {
            lock (_lock) return _queue.Count;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
        }
    }
}

/// <summary>
/// Key binding definition.
/// </summary>
public class KeyBinding
{
    public string Name { get; set; } = string.Empty;
    public Action? Action { get; set; }
    public bool RequiresModifier { get; set; }
}
