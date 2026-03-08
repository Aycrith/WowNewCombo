namespace Core;

public interface IScreenCapture
{
    void Request();
    ScreenCaptureResult Capture(string reason, string? correlationId = null, string? incidentId = null, int timeoutMs = 1500);
}
