using System.Numerics;

namespace Openthesia.Settings;

public sealed record WindowsAccessibilityState(
    float DpiScale,
    float TextScale,
    bool AnimationsEnabled,
    bool AdvancedEffectsEnabled,
    bool HighContrastEnabled);

public sealed record AccessibilityPresentation(
    float UiScale,
    bool AllowDecorativeMotion,
    bool AllowGlow,
    bool AllowTransparency,
    bool UseSystemContrast);

public static class AccessibilityPolicy
{
    public static double ContrastRatio(Vector4 first, Vector4 second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    public static AccessibilityPresentation Resolve(
        AccessibilitySettings settings,
        WindowsAccessibilityState windows)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(windows);

        var dpiScale = Math.Clamp(windows.DpiScale, 1f, 4f);
        var textScale = Math.Clamp(windows.TextScale, 1f, 2.25f);
        var allowRequestedEffects = settings.VisualEffects switch
        {
            VisualEffectsPreference.Reduce => false,
            VisualEffectsPreference.Full => true,
            _ => windows.AnimationsEnabled
        };
        var allowAdvancedEffects = settings.VisualEffects switch
        {
            VisualEffectsPreference.Reduce => false,
            VisualEffectsPreference.Full => true,
            _ => windows.AdvancedEffectsEnabled
        };
        var allowDecorativeEffects = allowRequestedEffects && !windows.HighContrastEnabled;

        return new AccessibilityPresentation(
            dpiScale * textScale,
            AllowDecorativeMotion: allowDecorativeEffects,
            AllowGlow: allowDecorativeEffects && allowAdvancedEffects,
            AllowTransparency: allowAdvancedEffects && !windows.HighContrastEnabled,
            UseSystemContrast: windows.HighContrastEnabled);
    }

    private static double RelativeLuminance(Vector4 color)
    {
        static double Linear(float channel)
        {
            var value = Math.Clamp(channel, 0f, 1f);
            return value <= 0.04045f
                ? value / 12.92d
                : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
        }

        return 0.2126d * Linear(color.X) +
               0.7152d * Linear(color.Y) +
               0.0722d * Linear(color.Z);
    }
}
