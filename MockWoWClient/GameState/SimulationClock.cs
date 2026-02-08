namespace MockWoWClient.GameState;

/// <summary>
/// Controls the simulation time with support for time scaling.
/// </summary>
public sealed class SimulationClock
{
    private readonly DateTime _startTime;
    private TimeSpan _elapsed;
    private float _timeScale = 1.0f;
    private DateTime _lastUpdate;

    /// <summary>
    /// Gets the time when the simulation started.
    /// </summary>
    public DateTime StartTime => _startTime;

    /// <summary>
    /// Gets the current simulated time.
    /// </summary>
    public DateTime CurrentTime => _startTime + _elapsed;

    /// <summary>
    /// Gets the total elapsed time since simulation started.
    /// </summary>
    public TimeSpan ElapsedTime => _elapsed;

    /// <summary>
    /// Gets or sets the time scale factor.
    /// 1.0 = real-time, 2.0 = 2x speed, 10.0 = 10x speed, etc.
    /// </summary>
    public float TimeScale
    {
        get => _timeScale;
        set => _timeScale = Math.Max(0.1f, value); // Minimum 0.1x speed
    }

    /// <summary>
    /// Gets whether the simulation is paused.
    /// </summary>
    public bool IsPaused => _timeScale <= 0;

    public SimulationClock()
    {
        _startTime = DateTime.UtcNow;
        _lastUpdate = DateTime.UtcNow;
        _elapsed = TimeSpan.Zero;
    }

    /// <summary>
    /// Updates the clock. Should be called every frame.
    /// </summary>
    /// <returns>The delta time for this frame (scaled).</returns>
    public TimeSpan Update()
    {
        DateTime now = DateTime.UtcNow;
        TimeSpan realDelta = now - _lastUpdate;
        _lastUpdate = now;

        TimeSpan scaledDelta = TimeSpan.FromTicks((long)(realDelta.Ticks * _timeScale));
        _elapsed += scaledDelta;

        return scaledDelta;
    }

    /// <summary>
    /// Advances the simulation by a specific amount of time.
    /// Useful for testing time-dependent scenarios.
    /// </summary>
    public void Advance(TimeSpan time)
    {
        _elapsed += time;
    }

    /// <summary>
    /// Advances the simulation by a specific amount of real time.
    /// Useful for speeding up long-running tests.
    /// </summary>
    public void AdvanceRealTime(TimeSpan realTime)
    {
        Advance(TimeSpan.FromTicks((long)(realTime.Ticks * _timeScale)));
    }

    /// <summary>
    /// Pauses the simulation.
    /// </summary>
    public void Pause()
    {
        _timeScale = 0;
    }

    /// <summary>
    /// Resumes the simulation.
    /// </summary>
    public void Resume(float timeScale = 1.0f)
    {
        _timeScale = timeScale;
        _lastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Resets the simulation clock.
    /// </summary>
    public void Reset()
    {
        _elapsed = TimeSpan.Zero;
        _lastUpdate = DateTime.UtcNow;
    }
}
