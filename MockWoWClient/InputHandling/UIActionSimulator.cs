using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using MockWoWClient.GameState;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MockWoWClient.InputHandling;

/// <summary>
/// Interface for simulating UI actions in the MockWoWClient.
/// Used for testing bot behavior with realistic user interactions.
/// </summary>
public interface IUIActionSimulator
{
    /// <summary>
    /// Simulates a mouse click at screen coordinates.
    /// </summary>
    Task ClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates a double-click.
    /// </summary>
    Task DoubleClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates a mouse drag operation.
    /// </summary>
    Task DragAsync(int fromX, int fromY, int toX, int toY, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates a key press.
    /// </summary>
    Task KeyPressAsync(int keyCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates pressing multiple keys simultaneously.
    /// </summary>
    Task KeyComboAsync(int[] keyCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates typing a string of characters.
    /// </summary>
    Task TypeTextAsync(string text, int delayMs = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates casting a spell by clicking its position.
    /// </summary>
    Task CastSpellAsync(int spellSlot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates interacting with a game object (loot, talk, etc.).
    /// </summary>
    Task InteractAsync(Vector3 worldPosition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates clicking on an NPC.
    /// </summary>
    Task ClickNpcAsync(Guid npcId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates opening a UI panel (bags, character, etc.).
    /// </summary>
    Task OpenPanelAsync(string panelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates closing the current UI panel.
    /// </summary>
    Task ClosePanelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the recorded action history.
    /// </summary>
    IReadOnlyList<UIActionRecord> GetActionHistory();

    /// <summary>
    /// Clears the action history.
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Enables or disables action recording.
    /// </summary>
    void SetRecordingEnabled(bool enabled);

    /// <summary>
    /// Replays recorded actions.
    /// </summary>
    Task ReplayActionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Mouse button types.
/// </summary>
public enum MouseButton
{
    Left,
    Right,
    Middle
}

/// <summary>
/// Record of a UI action for playback and analysis.
/// </summary>
public sealed class UIActionRecord
{
    /// <summary>
    /// Timestamp when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Type of action performed.
    /// </summary>
    public UIActionType ActionType { get; init; }

    /// <summary>
    /// X coordinate (for mouse actions).
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Y coordinate (for mouse actions).
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Key code (for keyboard actions).
    /// </summary>
    public int KeyCode { get; init; }

    /// <summary>
    /// Additional data for the action.
    /// </summary>
    public string Data { get; init; } = string.Empty;

    /// <summary>
    /// Time elapsed since the previous action.
    /// </summary>
    public TimeSpan DelayFromPrevious { get; init; }
}

/// <summary>
/// Types of UI actions.
/// </summary>
public enum UIActionType
{
    MouseClick,
    MouseDoubleClick,
    MouseDrag,
    KeyPress,
    KeyCombo,
    TextType,
    SpellCast,
    Interact,
    ClickNpc,
    OpenPanel,
    ClosePanel
}

/// <summary>
/// Simulates UI actions in MockWoWClient for testing purposes.
/// </summary>
public sealed class UIActionSimulator : IUIActionSimulator, IDisposable
{
    private readonly GameStateManager _gameState;
    private readonly InputProcessor _inputProcessor;
    private readonly ILogger<UIActionSimulator> _logger;
    private readonly List<UIActionRecord> _actionHistory = new();
    private readonly object _historyLock = new();
    private bool _isRecording = true;
    private DateTime _lastActionTime;
    private bool _disposed;

    public UIActionSimulator(
        GameStateManager gameState,
        InputProcessor inputProcessor,
        ILogger<UIActionSimulator>? logger = null)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _inputProcessor = inputProcessor ?? throw new ArgumentNullException(nameof(inputProcessor));
        _logger = logger ?? NullLogger<UIActionSimulator>.Instance;
        _lastActionTime = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public async Task ClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Click at ({X},{Y}) with {Button} button", x, y, button);

        int buttonCode = button switch
        {
            MouseButton.Left => 0,
            MouseButton.Right => 1,
            MouseButton.Middle => 2,
            _ => 0
        };

        // Simulate the click
        _inputProcessor.MouseMove(x, y);
        await Task.Delay(50, cancellationToken);
        _inputProcessor.MouseClick(buttonCode, x, y);

        RecordAction(UIActionType.MouseClick, x, y, keyCode: buttonCode);
        await Task.Delay(50, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DoubleClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Double-click at ({X},{Y})", x, y);

        await ClickAsync(x, y, button, cancellationToken);
        await Task.Delay(100, cancellationToken);
        await ClickAsync(x, y, button, cancellationToken);

        RecordAction(UIActionType.MouseDoubleClick, x, y);
    }

    /// <inheritdoc />
    public async Task DragAsync(int fromX, int fromY, int toX, int toY, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Drag from ({FromX},{FromY}) to ({ToX},{ToY})", fromX, fromY, toX, toY);

        // Move to start position
        _inputProcessor.MouseMove(fromX, fromY);
        await Task.Delay(50, cancellationToken);

        // Press and hold left mouse button
        _inputProcessor.MouseClick(0, fromX, fromY);

        // Move to end position (interpolated)
        int steps = 10;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = (int)(fromX + (toX - fromX) * t);
            int y = (int)(fromY + (toY - fromY) * t);
            _inputProcessor.MouseMove(x, y);
            await Task.Delay(20, cancellationToken);
        }

        // Release at end position
        _inputProcessor.MouseClick(0, toX, toY);

        RecordAction(UIActionType.MouseDrag, toX, toY, data: $"{fromX},{fromY}");
        await Task.Delay(50, cancellationToken);
    }

    /// <inheritdoc />
    public async Task KeyPressAsync(int keyCode, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Key press: {KeyCode}", keyCode);

        _inputProcessor.KeyDown(keyCode);
        await Task.Delay(50, cancellationToken);
        _inputProcessor.KeyUp(keyCode);

        RecordAction(UIActionType.KeyPress, keyCode: keyCode);
        await Task.Delay(50, cancellationToken);
    }

    /// <inheritdoc />
    public async Task KeyComboAsync(int[] keyCodes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Key combo: {Keys}", string.Join(",", keyCodes));

        // Press all keys
        foreach (int keyCode in keyCodes)
        {
            _inputProcessor.KeyDown(keyCode);
            await Task.Delay(50, cancellationToken);
        }

        // Release in reverse order
        for (int i = keyCodes.Length - 1; i >= 0; i--)
        {
            _inputProcessor.KeyUp(keyCodes[i]);
            await Task.Delay(50, cancellationToken);
        }

        RecordAction(UIActionType.KeyCombo, keyCode: keyCodes[0], data: string.Join(",", keyCodes));
    }

    /// <inheritdoc />
    public async Task TypeTextAsync(string text, int delayMs = 50, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Type text: {Length} chars", text.Length);

        foreach (char c in text)
        {
            int keyCode = char.ToUpper(c);
            await KeyPressAsync(keyCode, cancellationToken);
            await Task.Delay(delayMs, cancellationToken);
        }

        RecordAction(UIActionType.TextType, data: text);
    }

    /// <inheritdoc />
    public async Task CastSpellAsync(int spellSlot, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Cast spell in slot {Slot}", spellSlot);

        // Spell slots use number keys
        int keyCode = InputProcessor.VK_0 + spellSlot;
        await KeyPressAsync(keyCode, cancellationToken);

        RecordAction(UIActionType.SpellCast, keyCode: keyCode, data: $"Slot{spellSlot}");
    }

    /// <inheritdoc />
    public Task InteractAsync(Vector3 worldPosition, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Interact at {Position}", worldPosition);

        // Right-click to interact
        int screenX = WorldToScreenX(worldPosition);
        int screenY = WorldToScreenY(worldPosition);
        return ClickAsync(screenX, screenY, MouseButton.Right, cancellationToken);
    }

    /// <inheritdoc />
    public Task ClickNpcAsync(Guid npcId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Click NPC {NpcId}", npcId);

        // Find NPC and click on it
        var npc = _gameState.Npcs.FirstOrDefault(n => n.Id == npcId);
        if (npc == null)
        {
            _logger.LogWarning("[UIActionSim  ] NPC {NpcId} not found", npcId);
            return Task.CompletedTask;
        }

        int screenX = WorldToScreenX(npc.Position);
        int screenY = WorldToScreenY(npc.Position);
        return ClickAsync(screenX, screenY, MouseButton.Right, cancellationToken);
    }

    /// <inheritdoc />
    public async Task OpenPanelAsync(string panelName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Open panel: {Panel}", panelName);

        int keyCode = panelName.ToLower() switch
        {
            "bags" => 'B',  // 0x42
            "character" => 'C', // 0x43
            "spellbook" => 'P', // 0x50
            "talents" => 'N', // 0x4E
            "questlog" => 'L', // 0x4C
            "map" => 'M', // 0x4D
            _ => InputProcessor.VK_ESCAPE
        };

        await KeyPressAsync(keyCode, cancellationToken);
        RecordAction(UIActionType.OpenPanel, keyCode: keyCode, data: panelName);
    }

    /// <inheritdoc />
    public async Task ClosePanelAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogDebug("[UIActionSim  ] Close panel");

        await KeyPressAsync(InputProcessor.VK_ESCAPE, cancellationToken);
        RecordAction(UIActionType.ClosePanel, keyCode: InputProcessor.VK_ESCAPE);
    }

    /// <inheritdoc />
    public IReadOnlyList<UIActionRecord> GetActionHistory()
    {
        lock (_historyLock)
        {
            return _actionHistory.ToList().AsReadOnly();
        }
    }

    /// <inheritdoc />
    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _actionHistory.Clear();
            _logger.LogDebug("[UIActionSim  ] Action history cleared");
        }
    }

    /// <inheritdoc />
    public void SetRecordingEnabled(bool enabled)
    {
        _isRecording = enabled;
        _logger.LogDebug("[UIActionSim  ] Recording {Status}", enabled ? "enabled" : "disabled");
    }

    /// <inheritdoc />
    public async Task ReplayActionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        IReadOnlyList<UIActionRecord> actions = GetActionHistory();
        _logger.LogInformation("[UIActionSim  ] Replaying {Count} actions", actions.Count);

        foreach (UIActionRecord action in actions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Wait for the delay
            if (action.DelayFromPrevious > TimeSpan.Zero)
            {
                await Task.Delay(action.DelayFromPrevious, cancellationToken);
            }

            // Replay the action
            switch (action.ActionType)
            {
                case UIActionType.MouseClick:
                    await ClickAsync(action.X, action.Y, cancellationToken: cancellationToken);
                    break;
                case UIActionType.KeyPress:
                    await KeyPressAsync(action.KeyCode, cancellationToken);
                    break;
                case UIActionType.SpellCast:
                    await CastSpellAsync(action.KeyCode - InputProcessor.VK_0, cancellationToken);
                    break;
                case UIActionType.OpenPanel:
                    await OpenPanelAsync(action.Data, cancellationToken);
                    break;
                case UIActionType.ClosePanel:
                    await ClosePanelAsync(cancellationToken);
                    break;
                default:
                    _logger.LogWarning("[UIActionSim  ] Cannot replay action type: {Type}", action.ActionType);
                    break;
            }
        }

        _logger.LogInformation("[UIActionSim  ] Replay completed");
    }

    private void RecordAction(UIActionType actionType, int x = 0, int y = 0, int keyCode = 0, string data = "")
    {
        if (!_isRecording)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        TimeSpan delay = now - _lastActionTime;
        _lastActionTime = now;

        lock (_historyLock)
        {
            _actionHistory.Add(new UIActionRecord
            {
                Timestamp = now,
                ActionType = actionType,
                X = x,
                Y = y,
                KeyCode = keyCode,
                Data = data,
                DelayFromPrevious = delay
            });
        }
    }

    private static int WorldToScreenX(Vector3 worldPos)
    {
        // Simple projection - in real implementation this would use proper camera transform
        return (int)(worldPos.X / 10);
    }

    private static int WorldToScreenY(Vector3 worldPos)
    {
        return (int)(worldPos.Y / 10);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UIActionSimulator));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            ClearHistory();
        }
    }
}
