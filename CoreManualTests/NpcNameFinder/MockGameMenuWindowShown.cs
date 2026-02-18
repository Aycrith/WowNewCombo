using Core;

namespace CoreManualTests;

internal sealed class MockGameMenuWindowShown : IGameMenuWindowShown
{
    public bool GameMenuWindowShown() => false;
}
