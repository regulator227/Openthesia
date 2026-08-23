namespace Openthesia.Core.Songs;

public sealed record LegacyHandAssignmentCandidate(
    string Path,
    bool IsUnambiguous,
    IReadOnlyList<int>? CanonicalToLegacyNoteIndices = null);
