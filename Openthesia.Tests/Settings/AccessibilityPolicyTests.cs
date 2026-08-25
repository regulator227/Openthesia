using Openthesia.Settings;
using System.Numerics;
using Xunit;

namespace Openthesia.Tests.Settings;

public sealed class AccessibilityPolicyTests
{
    [Fact]
    public void SystemPreferenceHonorsWindowsScaleContrastAndReducedEffects()
    {
        var windows = new WindowsAccessibilityState(
            DpiScale: 1.5f,
            TextScale: 2.25f,
            AnimationsEnabled: false,
            AdvancedEffectsEnabled: false,
            HighContrastEnabled: true);

        var presentation = AccessibilityPolicy.Resolve(
            AccessibilitySettings.Default,
            windows);

        Assert.Equal(3.375f, presentation.UiScale);
        Assert.False(presentation.AllowDecorativeMotion);
        Assert.False(presentation.AllowGlow);
        Assert.False(presentation.AllowTransparency);
        Assert.True(presentation.UseSystemContrast);
    }

    [Fact]
    public void ContrastRatioUsesWcagRelativeLuminance()
    {
        var ratio = AccessibilityPolicy.ContrastRatio(Vector4.Zero, Vector4.One);

        Assert.Equal(21d, ratio, precision: 3);
    }

    [Theory]
    [InlineData(1f, 2.25f, 2.25f)]
    [InlineData(1.5f, 2.25f, 3.375f)]
    [InlineData(2f, 2.25f, 4.5f)]
    public void SupportedWindowsDisplayAndTextScalesCombine(
        float displayScale,
        float textScale,
        float expected)
    {
        var presentation = AccessibilityPolicy.Resolve(
            AccessibilitySettings.Default,
            new WindowsAccessibilityState(displayScale, textScale, true, true, false));

        Assert.Equal(expected, presentation.UiScale);
    }

    [Fact]
    public void ReducedEffectsDisableDecorativeMotionGlowAndTransparency()
    {
        var presentation = AccessibilityPolicy.Resolve(
            new AccessibilitySettings(VisualEffectsPreference.Reduce),
            new WindowsAccessibilityState(1f, 1f, true, true, false));

        Assert.False(presentation.AllowDecorativeMotion);
        Assert.False(presentation.AllowGlow);
        Assert.False(presentation.AllowTransparency);
    }

    [Fact]
    public void WindowsContrastOverridesFullEffects()
    {
        var presentation = AccessibilityPolicy.Resolve(
            new AccessibilitySettings(VisualEffectsPreference.Full),
            new WindowsAccessibilityState(1f, 1f, true, true, true));

        Assert.False(presentation.AllowDecorativeMotion);
        Assert.False(presentation.AllowGlow);
        Assert.False(presentation.AllowTransparency);
        Assert.True(presentation.UseSystemContrast);
    }
}
