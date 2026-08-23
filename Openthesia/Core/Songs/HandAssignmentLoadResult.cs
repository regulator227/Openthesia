namespace Openthesia.Core.Songs;

public sealed record HandAssignmentLoadResult(
    IReadOnlyList<PianoHand> Hands,
    string? Warning,
    bool MigratedLegacyData = false);
