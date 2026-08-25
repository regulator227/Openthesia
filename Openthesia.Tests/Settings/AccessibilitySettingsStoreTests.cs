using Openthesia.Settings;
using Xunit;

namespace Openthesia.Tests.Settings;

public sealed class AccessibilitySettingsStoreTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void NewInstallationsFollowWindowsByDefault()
    {
        var loaded = new AccessibilitySettingsStore(_dataDirectory).Load();

        Assert.Equal(AccessibilitySettings.Default, loaded.Settings);
        Assert.Equal(VisualEffectsPreference.System, loaded.Settings.VisualEffects);
        Assert.Null(loaded.Warning);
        Assert.False(Directory.Exists(_dataDirectory));
    }

    [Fact]
    public void VisualEffectsPreferencePersistsForTheDevice()
    {
        var settings = new AccessibilitySettings(VisualEffectsPreference.Reduce);
        var store = new AccessibilitySettingsStore(_dataDirectory);

        var saved = store.Save(settings);
        var loaded = new AccessibilitySettingsStore(_dataDirectory).Load();

        Assert.True(saved.Saved);
        Assert.Null(saved.Warning);
        Assert.Equal(settings, loaded.Settings);
        Assert.Null(loaded.Warning);
    }

    [Fact]
    public void CorruptSettingsFallBackSafelyAndAreNotOverwritten()
    {
        Directory.CreateDirectory(_dataDirectory);
        var path = Path.Combine(_dataDirectory, "Accessibility.json");
        File.WriteAllText(path, "not valid JSON");
        var store = new AccessibilitySettingsStore(_dataDirectory);

        var loaded = store.Load();
        var saved = store.Save(new AccessibilitySettings(VisualEffectsPreference.Full));

        Assert.Equal(AccessibilitySettings.Default, loaded.Settings);
        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.NotNull(saved.Warning);
        Assert.Equal("not valid JSON", File.ReadAllText(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }
}
