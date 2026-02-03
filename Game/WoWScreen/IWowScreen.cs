using SharedLib;

using System;

namespace Game;

public interface IWowScreen : IRectProvider, IScreenImageProvider, IMinimapImageProvider, IDisposable
{
    bool Enabled { get; set; }

    bool MinimapEnabled { get; set; }

    bool EnablePostProcess { get; set; }
    void PostProcess();

    event Action OnChanged;

    void Update();
    
    /// <summary>
    /// Waits for a successful screen update with retries.
    /// </summary>
    /// <param name="maxAttempts">Maximum number of attempts</param>
    /// <param name="delayMs">Delay between attempts in milliseconds</param>
    /// <returns>True if a frame was successfully acquired</returns>
    bool WaitForUpdate(int maxAttempts = 10, int delayMs = 50);
}
