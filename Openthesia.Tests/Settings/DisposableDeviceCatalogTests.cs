using Openthesia.Settings;
using Xunit;

namespace Openthesia.Tests.Settings;

public sealed class DisposableDeviceCatalogTests
{
    [Fact]
    public void DescribeGivesDuplicateNamesDistinctStableTokensAndDisposesEveryHandle()
    {
        var first = new FakeDevice("Keyboard");
        var second = new FakeDevice("Keyboard");
        var output = new FakeDevice("Piano");
        var catalog = Catalog(first, second, output);

        var descriptors = catalog.Describe();

        Assert.Equal(new[] { "Keyboard", "Keyboard", "Piano" }, descriptors.Select(x => x.Name));
        Assert.Equal(3, descriptors.Select(x => x.Token).Distinct().Count());
        Assert.All(new[] { first, second, output }, device => Assert.True(device.IsDisposed));
    }

    [Fact]
    public void TakeUsesOneSnapshotKeepsOnlyTheSelectedHandleAndDisposesTheRest()
    {
        var first = new FakeDevice("Keyboard");
        var second = new FakeDevice("Keyboard");
        var output = new FakeDevice("Piano");
        var catalog = Catalog(first, second, output);
        var token = DisposableDeviceCatalog<FakeDevice>.CreateToken("Keyboard", 1);

        var selected = catalog.Take(token);

        Assert.Same(second, selected);
        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.True(output.IsDisposed);
    }

    [Fact]
    public void TakeDisposesEverythingAndReturnsNullWhenADeviceDisappeared()
    {
        var first = new FakeDevice("Keyboard");
        var output = new FakeDevice("Piano");
        var catalog = Catalog(first, output);

        var selected = catalog.Take(
            DisposableDeviceCatalog<FakeDevice>.CreateToken("Missing", 0));

        Assert.Null(selected);
        Assert.True(first.IsDisposed);
        Assert.True(output.IsDisposed);
    }

    [Fact]
    public void DuplicateRemovalInvalidatesTheOldOccurrenceToken()
    {
        var firstSnapshot = new[]
        {
            new FakeDevice("Keyboard"),
            new FakeDevice("Keyboard")
        };
        var survivor = new FakeDevice("Keyboard");
        var call = 0;
        var catalog = new DisposableDeviceCatalog<FakeDevice>(
            () => call++ == 0 ? firstSnapshot : new[] { survivor },
            device => device.Name);
        var removedSnapshotToken = catalog.Describe()[0].Token;

        var selected = catalog.Take(removedSnapshotToken);

        Assert.Null(selected);
        Assert.True(survivor.IsDisposed);
    }

    private static DisposableDeviceCatalog<FakeDevice> Catalog(params FakeDevice[] devices)
    {
        return new DisposableDeviceCatalog<FakeDevice>(
            () => devices,
            device => device.Name);
    }

    private sealed class FakeDevice : IDisposable
    {
        public FakeDevice(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
