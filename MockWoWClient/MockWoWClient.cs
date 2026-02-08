using Microsoft.Extensions.Logging;
using MockWoWClient.Contracts;
using MockWoWClient.GameState;
using MockWoWClient.InputHandling;
using MockWoWClient.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MockWoWClient;

/// <summary>
/// The main MockWoWClient class that simulates a World of Warcraft client.
/// This is the entry point for the testing environment.
/// </summary>
public sealed class MockWoWClient : IDisposable
{
    private readonly ILogger<MockWoWClient> _logger;
    private readonly CancellationTokenSource _cts;
    private Task? _simulationTask;

    // Core components
    public SimulationClock Clock { get; }
    public PixelGridRenderer Renderer { get; }
    public GameStateManager GameState { get; }
    public InputProcessor InputProcessor { get; }
    public GameStateFrameMapper FrameMapper { get; }

    // Configuration
    public bool IsRunning { get; private set; }
    public bool IsConfigMode => Renderer.IsConfigMode;

    // Events
    public event Action? OnStarted;
    public event Action? OnStopped;
    public event Action<TimeSpan>? OnTick;

    public MockWoWClient(ILogger<MockWoWClient>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MockWoWClient>.Instance;
        _cts = new CancellationTokenSource();

        // Initialize components
        Clock = new SimulationClock();
        Renderer = new PixelGridRenderer();
        GameState = new GameStateManager(Clock);
        InputProcessor = new InputProcessor(GameState);
        FrameMapper = new GameStateFrameMapper(GameState, Renderer);

        // Wire up events
        SetupEventHandlers();
    }

    /// <summary>
    /// Starts the simulation loop.
    /// </summary>
    public void Start()
    {
        if (IsRunning)
        {
            _logger.LogWarning("MockWoWClient is already running");
            return;
        }

        _logger.LogInformation("Starting MockWoWClient simulation");
        IsRunning = true;

        // Start simulation loop
        _simulationTask = RunSimulationLoop(_cts.Token);

        OnStarted?.Invoke();
    }

    /// <summary>
    /// Stops the simulation loop.
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        _logger.LogInformation("Stopping MockWoWClient simulation");
        
        _cts.Cancel();
        
        if (_simulationTask != null)
        {
            try
            {
                await _simulationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        IsRunning = false;
        OnStopped?.Invoke();
    }

    /// <summary>
    /// Toggles config mode on/off.
    /// </summary>
    public void ToggleConfigMode()
    {
        Renderer.IsConfigMode = !Renderer.IsConfigMode;
        _logger.LogInformation("Config mode: {Mode}", Renderer.IsConfigMode ? "ON" : "OFF");
    }

    /// <summary>
    /// Captures the current screen.
    /// </summary>
    public Image<Bgra32> CaptureScreen()
    {
        return Renderer.CaptureScreen();
    }

    /// <summary>
    /// Advances the simulation by a specific time (for testing).
    /// </summary>
    public void Advance(TimeSpan time)
    {
        // Process any pending inputs first
        InputProcessor.ProcessFrame(time);
        Clock.Advance(time);
        GameState.Update(time);
        FrameMapper.UpdateFrames();
    }

    /// <summary>
    /// Processes all pending inputs immediately.
    /// </summary>
    public void ProcessInputs()
    {
        InputProcessor.ProcessFrame(TimeSpan.FromMilliseconds(16));
    }

    /// <summary>
    /// Spawns an NPC for testing.
    /// </summary>
    public NpcEntity SpawnNpc(string name, int level, int health, System.Numerics.Vector3 position, bool hostile = true)
    {
        var npc = GameState.SpawnNpc(name, level, health, position, hostile);
        _logger.LogInformation("Spawned NPC: {Name} (Level {Level}) at {Position}", name, level, position);
        return npc;
    }

    /// <summary>
    /// Clears all game state (for test isolation).
    /// </summary>
    public void Reset()
    {
        _logger.LogInformation("Resetting MockWoWClient state");
        GameState.Reset();
        Renderer.Clear();
        InputProcessor.Queue.Clear();
        Clock.Reset();
    }

    private async Task RunSimulationLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Update clock
                TimeSpan deltaTime = Clock.Update();

                // Process inputs
                InputProcessor.ProcessFrame(deltaTime);

                // Update game state
                GameState.Update(deltaTime);

                // Update pixel frames
                FrameMapper.UpdateFrames();

                // Notify tick
                OnTick?.Invoke(deltaTime);

                // Yield to prevent CPU spinning
                await Task.Delay(16, cancellationToken); // ~60 FPS
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in simulation loop");
            throw;
        }
    }

    private static void SetupEventHandlers()
    {
        // Handle chat commands
        // Future: Wire up chat command events
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts.Dispose();
        Renderer.Dispose();
    }
}
