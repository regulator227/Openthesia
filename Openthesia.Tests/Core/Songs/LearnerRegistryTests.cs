using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Songs;

public sealed class LearnerRegistryTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void FirstUseCreatesOneDurableDefaultLearnerAndSelectsIt()
    {
        var active = new LearnerRegistry(_dataDirectory).GetOrCreateActive();

        var reloaded = new LearnerRegistry(_dataDirectory);

        Assert.Equal("Default Learner", active.Name);
        Assert.Equal(active, reloaded.GetOrCreateActive());
        Assert.Equal(new[] { active }, reloaded.GetAll());
        Assert.True(File.Exists(Path.Combine(_dataDirectory, "Learners.json")));
        Assert.True(File.Exists(Path.Combine(_dataDirectory, "DeviceSettings.json")));
    }

    [Fact]
    public void AdditionalLearnersAndDeviceSelectionRemainDurable()
    {
        var registry = new LearnerRegistry(_dataDirectory);
        var original = registry.GetOrCreateActive();
        var second = registry.Create("Alex");

        registry.SetActive(second.Id);

        var reloaded = new LearnerRegistry(_dataDirectory);
        Assert.Equal(second, reloaded.GetOrCreateActive());
        Assert.Equal(new[] { original, second }, reloaded.GetAll());
    }

    [Fact]
    public void CorruptLearnerMetadataIsPreservedAndNotOverwritten()
    {
        Directory.CreateDirectory(_dataDirectory);
        var learnersPath = Path.Combine(_dataDirectory, "Learners.json");
        File.WriteAllText(learnersPath, "not valid JSON");

        Assert.ThrowsAny<Exception>(() =>
            new LearnerRegistry(_dataDirectory).GetOrCreateActive());
        Assert.Equal("not valid JSON", File.ReadAllText(learnersPath));
        Assert.False(File.Exists(Path.Combine(_dataDirectory, "DeviceSettings.json")));
    }

    [Fact]
    public void LearnerMetadataWithoutVersionIsPreservedAndRejected()
    {
        Directory.CreateDirectory(_dataDirectory);
        var learnersPath = Path.Combine(_dataDirectory, "Learners.json");
        const string unversioned = "{\"Learners\":[]}";
        File.WriteAllText(learnersPath, unversioned);

        Assert.ThrowsAny<Exception>(() =>
            new LearnerRegistry(_dataDirectory).GetOrCreateActive());
        Assert.Equal(unversioned, File.ReadAllText(learnersPath));
    }

    [Fact]
    public void DeviceSettingsWithoutVersionArePreservedAndRejected()
    {
        var registry = new LearnerRegistry(_dataDirectory);
        registry.GetOrCreateActive();
        var settingsPath = Path.Combine(_dataDirectory, "DeviceSettings.json");
        const string unversioned = "{\"ActiveLearnerId\":null}";
        File.WriteAllText(settingsPath, unversioned);

        Assert.ThrowsAny<Exception>(() => registry.GetOrCreateActive());
        Assert.Equal(unversioned, File.ReadAllText(settingsPath));
    }

    [Fact]
    public void UnknownActiveLearnerIsPreservedAndRejected()
    {
        var registry = new LearnerRegistry(_dataDirectory);
        registry.GetOrCreateActive();
        var settingsPath = Path.Combine(_dataDirectory, "DeviceSettings.json");
        var incompatible =
            $"{{\"Version\":1,\"ActiveLearnerId\":\"{Guid.NewGuid()}\"}}";
        File.WriteAllText(settingsPath, incompatible);

        Assert.Throws<InvalidDataException>(() => registry.GetOrCreateActive());
        Assert.Equal(incompatible, File.ReadAllText(settingsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }
}
