using System;
using System.Threading;
using System.Threading.Tasks;

using Core.AI.LLM;
using Core.FeatureFlags;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace CoreUnitTests.AI;

public sealed class LLMClientFactoryTests
{
    [Fact]
    public void CreateClient_NullOrWhitespace_ThrowsArgumentException()
    {
        LLMClientFactory factory = CreateFactory();

        Action actNull = () => factory.CreateClient(null!);
        Action actEmpty = () => factory.CreateClient(string.Empty);
        Action actWhitespace = () => factory.CreateClient("   ");

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
        actWhitespace.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateClient_UnknownProvider_ThrowsArgumentException()
    {
        LLMClientFactory factory = CreateFactory();

        Action act = () => factory.CreateClient("nonexistent_provider_xyz");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*nonexistent_provider_xyz*");
    }

    [Fact]
    public void CreateClient_UnknownProvider_CalledConcurrently_AllThrowConsistently()
    {
        // Arrange — barrier ensures all threads start simultaneously to maximize race window
        LLMClientFactory factory = CreateFactory();
        Exception?[] results = new Exception?[16];
        CountdownEvent barrier = new(16);

        // Act — 16 concurrent threads all request the same unknown provider
        Parallel.For(0, 16, i =>
        {
            barrier.Signal();
            barrier.Wait();
            try
            {
                factory.CreateClient("unknown_provider_race_test");
                results[i] = null;
            }
            catch (Exception ex)
            {
                results[i] = ex;
            }
        });

        // Assert — every call must throw ArgumentException (Lazy caches exceptions too)
        foreach (Exception? result in results)
        {
            result.Should().BeOfType<ArgumentException>(
                "all concurrent calls to an unknown provider must throw ArgumentException");
        }
    }

    [Fact]
    public void CreateClient_UnknownProvider_CalledTwiceSequentially_ThrowsBothTimes()
    {
        // Verify Lazy exception caching: second call re-throws cached exception
        LLMClientFactory factory = CreateFactory();

        Action first = () => factory.CreateClient("unknown_sequential");
        Action second = () => factory.CreateClient("unknown_sequential");

        first.Should().Throw<ArgumentException>();
        second.Should().Throw<ArgumentException>();
    }

    private static LLMClientFactory CreateFactory()
    {
        return new LLMClientFactory(
            serviceProvider: new MinimalServiceProvider(),
            NullLogger<LLMClientFactory>.Instance,
            Options.Create(new AIProfileGeneratorOptions()));
    }

    /// <summary>Minimal IServiceProvider that returns null for all services.</summary>
    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
