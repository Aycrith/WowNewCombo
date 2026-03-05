namespace Frontend.Controllers;

// Shared contracts for diagnostics controllers.
public record InputSecurityModeInfo(
    string Mode,
    bool BackgroundCompatible,
    bool Enabled,
    bool FocusGuard,
    bool HybridModifiers,
    bool EmitWmChar);

public record SetInputSecurityModeRequest(bool BackgroundCompatible);

public record PlaceRequest(int Slot, string Name);

public record FixResult(bool Success, string Message, int ChangesApplied = 0);

public record SlashCommandRequest(
    string Command,
    bool UseBackgroundCompatibleInput = false,
    int PreDelayMs = 200,
    int PostDelayMs = 500);

public record SlashCommandResult(
    bool Success,
    string Command,
    string DispatchPath,
    long ElapsedMs,
    string? Error = null);

public record MailboxInteractDiagnostics(
    bool MailFrameShown,
    bool CursorFound,
    string CursorType,
    int CursorX,
    int CursorY,
    string InteractionStep,
    int Attempts,
    long ElapsedMs);
