using Openthesia.Core.Audio;
using Xunit;

namespace Openthesia.Tests.Core.Audio;

public sealed class AudioOutputStartupTests
{
    [Fact]
    public void StartsCreatedOutputInOrder()
    {
        var output = new FakeOutput();
        var calls = new List<string>();

        var started = AudioOutputStartup.TryStart(
            () =>
            {
                calls.Add("create");
                return output;
            },
            _ => calls.Add("initialize"),
            _ => calls.Add("play"),
            _ => calls.Add("dispose"),
            out var startedOutput,
            out var error);

        Assert.True(started);
        Assert.Same(output, startedOutput);
        Assert.Null(error);
        Assert.Equal(new[] { "create", "initialize", "play" }, calls);
    }

    [Fact]
    public void ConstructionFailureDoesNotAttemptCleanup()
    {
        var startupError = new InvalidOperationException("create failed");
        var disposed = false;

        var started = AudioOutputStartup.TryStart<FakeOutput>(
            () => throw startupError,
            _ => { },
            _ => { },
            _ => disposed = true,
            out var output,
            out var error);

        Assert.False(started);
        Assert.Null(output);
        Assert.Same(startupError, error);
        Assert.False(disposed);
    }

    [Fact]
    public void InitializationFailureDisposesPartialOutput()
    {
        var startupError = new InvalidOperationException("initialize failed");
        var output = new FakeOutput();
        FakeOutput? disposedOutput = null;

        var started = AudioOutputStartup.TryStart(
            () => output,
            _ => throw startupError,
            _ => { },
            value => disposedOutput = value,
            out var startedOutput,
            out var error);

        Assert.False(started);
        Assert.Null(startedOutput);
        Assert.Same(startupError, error);
        Assert.Same(output, disposedOutput);
    }

    [Fact]
    public void PlaybackFailureDisposesPartialOutput()
    {
        var startupError = new InvalidOperationException("play failed");
        var output = new FakeOutput();
        FakeOutput? disposedOutput = null;

        var started = AudioOutputStartup.TryStart(
            () => output,
            _ => { },
            _ => throw startupError,
            value => disposedOutput = value,
            out var startedOutput,
            out var error);

        Assert.False(started);
        Assert.Null(startedOutput);
        Assert.Same(startupError, error);
        Assert.Same(output, disposedOutput);
    }

    [Fact]
    public void CleanupFailureDoesNotHideStartupFailure()
    {
        var startupError = new InvalidOperationException("play failed");

        var started = AudioOutputStartup.TryStart(
            () => new FakeOutput(),
            _ => { },
            _ => throw startupError,
            _ => throw new InvalidOperationException("dispose failed"),
            out var output,
            out var error);

        Assert.False(started);
        Assert.Null(output);
        Assert.Same(startupError, error);
    }

    private sealed class FakeOutput
    {
    }
}
