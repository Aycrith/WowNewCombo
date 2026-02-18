using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Core.AI.LLM;
using Core.AI.ProfileGenerator;
using Core.FeatureFlags;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace CoreUnitTests.AI;

public sealed class AIProfileGeneratorServiceTests
{
    private readonly NullLogger<AIProfileGeneratorService> logger = new();
    private readonly NullLogger<ProfileValidator> validatorLogger = new();

    private AIProfileGeneratorService CreateService(
        ILLMClientFactory? clientFactory = null,
        ProfileValidator? validator = null,
        AIProfileGeneratorOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        clientFactory ??= new FakeLLMClientFactory(new FakeLLMClient());
        validator ??= new ProfileValidator(validatorLogger);
        options ??= new AIProfileGeneratorOptions { RateLimitPerHour = 20 };

        return new AIProfileGeneratorService(
            logger,
            clientFactory,
            validator,
            Options.Create(options),
            timeProvider);
    }

    // -----------------------------------------------------------------------
    // SanitizeDescription tests
    // -----------------------------------------------------------------------

    [Fact]
    public void SanitizeDescription_TruncatesAtMaxDescriptionLength()
    {
        string longInput = new('A', AIProfileGeneratorService.MaxDescriptionLength + 100);

        string result = AIProfileGeneratorService.SanitizeDescription(longInput);

        result.Length.Should().BeLessThanOrEqualTo(AIProfileGeneratorService.MaxDescriptionLength);
    }

    [Fact]
    public void SanitizeDescription_StripsControlCharacters()
    {
        string input = "Level 30\x01\x02\x03 Mage";

        string result = AIProfileGeneratorService.SanitizeDescription(input);

        result.Should().Be("Level 30 Mage");
    }

    [Fact]
    public void SanitizeDescription_StripsNonAllowedCharacters()
    {
        // Backticks, angle brackets, curly braces, pipes, etc. are not in the allow list
        string input = "Level 30 Mage `<script>alert(1)</script>` {injected} |pipe|";

        string result = AIProfileGeneratorService.SanitizeDescription(input);

        result.Should().NotContain("`");
        result.Should().NotContain("<");
        result.Should().NotContain(">");
        result.Should().NotContain("{");
        result.Should().NotContain("}");
        result.Should().NotContain("|");
    }

    [Fact]
    public void SanitizeDescription_AllowsBasicPunctuation()
    {
        string input = "Level 30 Frost Mage, grinding in Hillsbrad! (solo)";

        string result = AIProfileGeneratorService.SanitizeDescription(input);

        result.Should().Be("Level 30 Frost Mage, grinding in Hillsbrad! (solo)");
    }

    [Fact]
    public void SanitizeDescription_TrimsWhitespace()
    {
        string input = "   Level 30 Mage   ";

        string result = AIProfileGeneratorService.SanitizeDescription(input);

        result.Should().Be("Level 30 Mage");
    }

    [Fact]
    public void SanitizeDescription_PreservesNewlinesAndTabs()
    {
        // Newlines (\n, \r) and tabs (\t) are NOT stripped by the control char regex
        // because the regex targets \x00-\x08, \x0B, \x0C, \x0E-\x1F, \x7F
        // \t is \x09, \n is \x0A, \r is \x0D - all outside the stripped range
        string input = "Line one\nLine two\tTabbed";

        string result = AIProfileGeneratorService.SanitizeDescription(input);

        result.Should().Contain("\n");
        result.Should().Contain("\t");
    }

    // -----------------------------------------------------------------------
    // GenerateProfileAsync - input validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateProfileAsync_ThrowsOnNullDescription()
    {
        AIProfileGeneratorService service = CreateService();

        Func<Task> act = () => service.GenerateProfileAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateProfileAsync_ThrowsOnEmptyDescription()
    {
        AIProfileGeneratorService service = CreateService();

        Func<Task> act = () => service.GenerateProfileAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateProfileAsync_ThrowsOnWhitespaceDescription()
    {
        AIProfileGeneratorService service = CreateService();

        Func<Task> act = () => service.GenerateProfileAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // GenerateProfileAsync - LLM not available
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateProfileAsync_ReturnsFailure_WhenLLMNotAvailable()
    {
        FakeLLMClient client = new() { Available = false };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client));

        ProfileGenerationResult result = await service.GenerateProfileAsync("Level 30 Frost Mage");

        result.Success.Should().BeFalse();
        result.Profile.Should().BeNull();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("not available");
    }

    // -----------------------------------------------------------------------
    // GenerateProfileAsync - rate limiting
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateProfileAsync_ReturnsFailure_WhenRateLimited()
    {
        FakeLLMClient client = new() { Available = true, Response = "{}" };
        AIProfileGeneratorOptions options = new() { RateLimitPerHour = 2 };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client),
            options: options);

        // Exhaust the rate limit
        await service.GenerateProfileAsync("Level 10 Warrior");
        await service.GenerateProfileAsync("Level 20 Mage");

        // Third request should be rate limited
        ProfileGenerationResult result = await service.GenerateProfileAsync("Level 30 Rogue");

        result.Success.Should().BeFalse();
        result.Profile.Should().BeNull();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public async Task GenerateProfileAsync_AllowsRequests_AfterRateLimitWindowExpires()
    {
        FakeTimeProvider timeProvider = new(DateTimeOffset.UtcNow);
        FakeLLMClient client = new() { Available = true, Response = "{}" };
        AIProfileGeneratorOptions options = new() { RateLimitPerHour = 1 };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client),
            options: options,
            timeProvider: timeProvider);

        // First request succeeds (though profile may fail validation, the rate limit check passes)
        await service.GenerateProfileAsync("Level 10 Warrior");

        // Advance time past the 1-hour window
        timeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Next request should not be rate limited (it may fail for other reasons, but not rate limit)
        ProfileGenerationResult result = await service.GenerateProfileAsync("Level 20 Mage");

        result.Errors.Should().NotContain(e => e.Contains("Rate limit exceeded"));
    }

    // -----------------------------------------------------------------------
    // GenerateProfileAsync - successful generation flow
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateProfileAsync_ReturnsFailure_WhenValidationFails()
    {
        // Return invalid JSON (missing required fields) so validator rejects it
        FakeLLMClient client = new()
        {
            Available = true,
            Response = """{"NotAValidField": "value"}"""
        };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client));

        ProfileGenerationResult result = await service.GenerateProfileAsync("Level 30 Frost Mage");

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateProfileAsync_ReturnsFailure_WhenLLMThrowsException()
    {
        FakeLLMClient client = new()
        {
            Available = true,
            ThrowOnComplete = new InvalidOperationException("LLM exploded")
        };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client));

        ProfileGenerationResult result = await service.GenerateProfileAsync("Level 30 Frost Mage");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("LLM exploded");
    }

    // -----------------------------------------------------------------------
    // ExtractClassHint (tested indirectly via BuildPrompt behavior)
    // The prompt output is not directly accessible, but we can verify that
    // the LLM receives class-specific spell hints when a class is mentioned.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("Level 30 Mage in Hillsbrad", "Frostbolt")]
    [InlineData("Warrior grinding in Duskwood", "Heroic Strike")]
    [InlineData("Hunter leveling in Barrens", "Arcane Shot")]
    [InlineData("Rogue farming in Arathi", "Sinister Strike")]
    [InlineData("Priest healing dungeons", "Smite")]
    [InlineData("Warlock with voidwalker", "Shadow Bolt")]
    [InlineData("Paladin tanking", "Seal of Righteousness")]
    [InlineData("Druid feral", "Wrath")]
    [InlineData("Shaman enhancement", "Lightning Bolt")]
    public async Task GenerateProfileAsync_UsesClassSpecificSpells_WhenClassMentioned(
        string description, string expectedSpell)
    {
        FakeLLMClient client = new() { Available = true, Response = "{}" };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client));

        // The generation will fail validation but we can inspect the prompt sent to the LLM
        await service.GenerateProfileAsync(description);

        client.LastPrompt.Should().Contain(expectedSpell);
    }

    [Fact]
    public async Task GenerateProfileAsync_UsesGenericSpells_WhenNoClassMentioned()
    {
        FakeLLMClient client = new() { Available = true, Response = "{}" };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client));

        await service.GenerateProfileAsync("Level 30 character grinding");

        // When no class is detected, generic spell list is used
        client.LastPrompt.Should().Contain("Fireball");
        client.LastPrompt.Should().Contain("Shadow Bolt");
    }

    // -----------------------------------------------------------------------
    // IsAvailableAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_WhenLLMIsAvailable()
    {
        FakeLLMClient client = new() { Available = true };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client));

        bool result = await service.IsAvailableAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenLLMIsNotAvailable()
    {
        FakeLLMClient client = new() { Available = false };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client));

        bool result = await service.IsAvailableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenFactoryThrows()
    {
        ThrowingLLMClientFactory factory = new();
        AIProfileGeneratorService service = CreateService(clientFactory: factory);

        bool result = await service.IsAvailableAsync();

        result.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // SanitizeDescription - only safe characters pass through for prompt
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateProfileAsync_ReturnsFailure_WhenDescriptionBecomesEmptyAfterSanitization()
    {
        // All characters are stripped by sanitization
        FakeLLMClient client = new() { Available = true, Response = "{}" };
        AIProfileGeneratorService service = CreateService(
            clientFactory: new FakeLLMClientFactory(client));

        // Use only characters that will be stripped
        ProfileGenerationResult result = await service.GenerateProfileAsync("$@^&*=[]{}|\\~`<>");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("no valid characters");
    }

    // =======================================================================
    // Test doubles
    // =======================================================================

    private sealed class FakeLLMClient : ILLMClient
    {
        public string ProviderName => "FakeProvider";
        public bool Available { get; init; } = true;
        public string Response { get; init; } = "{}";
        public Exception? ThrowOnComplete { get; init; }
        public long LastLatencyMs => 0;
        public string? LastPrompt { get; private set; }

        public Task<bool> IsAvailableAsync() => Task.FromResult(Available);

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            if (ThrowOnComplete != null)
            {
                return Task.FromException<string>(ThrowOnComplete);
            }
            return Task.FromResult(Response);
        }
    }

    private sealed class FakeLLMClientFactory(ILLMClient client) : ILLMClientFactory
    {
        public ILLMClient CreateClient(string providerName) => client;
        public ILLMClient GetDefaultClient() => client;
    }

    private sealed class ThrowingLLMClientFactory : ILLMClientFactory
    {
        public ILLMClient CreateClient(string providerName) =>
            throw new InvalidOperationException("No LLM configured");
        public ILLMClient GetDefaultClient() =>
            throw new InvalidOperationException("No LLM configured");
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset current;

        public FakeTimeProvider(DateTimeOffset start)
        {
            current = start;
        }

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration)
        {
            current = current.Add(duration);
        }
    }
}
