using MockWoWClient.GameState;
using System.Text;

namespace MockWoWClient.InputHandling;

/// <summary>
/// Handles chat commands from the bot.
/// </summary>
public sealed class ChatCommandHandler
{
    private readonly GameStateManager _gameState;
    private readonly StringBuilder _chatBuffer = new();
    private bool _isTypingCommand;

    public ChatCommandHandler(GameStateManager gameState)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
    }

    /// <summary>
    /// Attempts to handle a character as chat input.
    /// Returns true if the character was consumed as part of a command.
    /// </summary>
    public bool TryHandleChatInput(char c)
    {
        // Check for command prefix
        if (c == '/')
        {
            _isTypingCommand = true;
            _chatBuffer.Clear();
            _chatBuffer.Append(c);
            return true;
        }

        if (!_isTypingCommand)
            return false;

        // Accumulate command
        if (char.IsLetterOrDigit(c) || c == ' ')
        {
            _chatBuffer.Append(c);
            return true;
        }

        // Execute command on Enter or special terminator
        if (c == '\r' || c == '\n')
        {
            ExecuteCommand(_chatBuffer.ToString().Trim());
            _chatBuffer.Clear();
            _isTypingCommand = false;
            return true;
        }

        // Cancel on Escape
        if (c == (char)0x1B)
        {
            _chatBuffer.Clear();
            _isTypingCommand = false;
            return true;
        }

        return false;
    }

    private void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        // Parse command and arguments
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        string cmd = parts[0].ToLowerInvariant();
        string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        switch (cmd)
        {
            // Config mode toggle
            case "/dc":
                ToggleConfigMode();
                break;

            // Flush state
            case "/dcflush":
                FlushState();
                break;

            // Setup bindings
            case "/dcbindings":
                SetupBindings();
                break;

            // Setup actions
            case "/dcactions":
                SetupActions();
                break;

            // Help
            case "/dchelp":
                ShowHelp();
                break;

            default:
                // Unknown command
                break;
        }
    }

    private void ToggleConfigMode()
    {
        // In the real addon, this toggles between config mode and data mode
        // Config mode displays frame indices as RGB values
        // Data mode displays actual game data
        
        // This will be handled by the MockWoWClient's mode property
        OnConfigModeToggled?.Invoke();
    }

    private void FlushState()
    {
        // In the real addon, this resets the addon's internal state
        // Useful when game state gets out of sync
        OnStateFlushed?.Invoke();
    }

    private void SetupBindings()
    {
        // In the real addon, this sets up default key bindings
        // For the mock, we just acknowledge the command
        OnBindingsSetup?.Invoke();
    }

    private void SetupActions()
    {
        // In the real addon, this creates secure action buttons
        // For the mock, we just acknowledge the command
        OnActionsSetup?.Invoke();
    }

    private static void ShowHelp()
    {
        // Display available commands
    }

    public event Action? OnConfigModeToggled;
    public event Action? OnStateFlushed;
    public event Action? OnBindingsSetup;
    public event Action? OnActionsSetup;
}
