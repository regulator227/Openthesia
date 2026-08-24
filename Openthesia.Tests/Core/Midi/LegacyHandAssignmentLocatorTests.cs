using Openthesia.Core.Midi;
using Xunit;

namespace Openthesia.Tests.Core.Midi;

public sealed class LegacyHandAssignmentLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SameNamedMidiSourcesMakeLegacyAssignmentsAmbiguous()
    {
        var firstDirectory = Directory.CreateDirectory(Path.Combine(_root, "First")).FullName;
        var secondDirectory = Directory.CreateDirectory(Path.Combine(_root, "Second")).FullName;
        var firstSource = Path.Combine(firstDirectory, "Prelude.mid");
        File.WriteAllBytes(firstSource, Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(secondDirectory, "Prelude.mid"), Array.Empty<byte>());
        var locator = new LegacyHandAssignmentLocator(
            Path.Combine(_root, "Data"),
            Path.Combine(_root, "Legacy"),
            new[] { firstDirectory, secondDirectory });

        var candidate = locator.Find(firstSource);

        Assert.False(candidate.IsUnambiguous);
        Assert.Equal(Path.Combine(_root, "Legacy", "Prelude.xml"), candidate.Path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
