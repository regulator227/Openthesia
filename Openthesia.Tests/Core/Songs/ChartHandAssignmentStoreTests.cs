using Openthesia.Core.Songs;
using Xunit;

namespace Openthesia.Tests.Core.Songs;

public sealed class ChartHandAssignmentStoreTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "Openthesia.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void AssignmentsPersistByChartIdentity()
    {
        var firstChart = ChartIdFor('1');
        var secondChart = ChartIdFor('2');
        var store = new ChartHandAssignmentStore(_dataDirectory);

        store.Save(firstChart, new[] { PianoHand.Left, PianoHand.Right });

        var saved = new ChartHandAssignmentStore(_dataDirectory).Load(firstChart, noteCount: 2);
        var unrelated = store.Load(secondChart, noteCount: 2);

        Assert.Equal(new[] { PianoHand.Left, PianoHand.Right }, saved.Hands);
        Assert.Null(saved.Warning);
        Assert.Equal(new[] { PianoHand.Right, PianoHand.Right }, unrelated.Hands);
    }

    [Fact]
    public void UnambiguousCompatibleLegacyAssignmentsAreCopiedToChartStorage()
    {
        Directory.CreateDirectory(_dataDirectory);
        var legacyPath = Path.Combine(_dataDirectory, "Prelude.xml");
        File.WriteAllText(
            legacyPath,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<LeftRightData xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
            "<IsRightNote><boolean>false</boolean><boolean>true</boolean></IsRightNote>" +
            "</LeftRightData>");
        var chartId = ChartIdFor('3');
        var store = new ChartHandAssignmentStore(_dataDirectory);

        var migrated = store.Load(
            chartId,
            noteCount: 2,
            new LegacyHandAssignmentCandidate(legacyPath, IsUnambiguous: true));

        Assert.Equal(new[] { PianoHand.Left, PianoHand.Right }, migrated.Hands);
        Assert.True(migrated.MigratedLegacyData);
        Assert.True(File.Exists(legacyPath));
        Assert.Equal(
            migrated.Hands,
            new ChartHandAssignmentStore(_dataDirectory).Load(chartId, noteCount: 2).Hands);
    }

    [Fact]
    public void LegacyAssignmentsAreRemappedIntoCanonicalChartNoteOrder()
    {
        var legacyPath = WriteLegacyAssignments("Reordered.xml", false, true);
        var chartId = ChartIdFor('8');
        var store = new ChartHandAssignmentStore(_dataDirectory);

        var migrated = store.Load(
            chartId,
            noteCount: 2,
            new LegacyHandAssignmentCandidate(
                legacyPath,
                IsUnambiguous: true,
                CanonicalToLegacyNoteIndices: new[] { 1, 0 }));

        Assert.Equal(new[] { PianoHand.Right, PianoHand.Left }, migrated.Hands);
        Assert.True(migrated.MigratedLegacyData);
    }

    [Fact]
    public void AmbiguousLegacyAssignmentsArePreservedAndNotImported()
    {
        var legacyPath = WriteLegacyAssignments("Ambiguous.xml", false, true);
        var store = new ChartHandAssignmentStore(_dataDirectory);

        var result = store.Load(
            ChartIdFor('4'),
            noteCount: 2,
            new LegacyHandAssignmentCandidate(legacyPath, IsUnambiguous: false));

        Assert.Equal(new[] { PianoHand.Right, PianoHand.Right }, result.Hands);
        Assert.False(result.MigratedLegacyData);
        Assert.Contains("ambiguous", result.Warning, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(legacyPath));
    }

    [Fact]
    public void IncompatibleLegacyAssignmentsArePreservedAndNotImported()
    {
        var legacyPath = WriteLegacyAssignments("Mismatch.xml", false, true);
        var store = new ChartHandAssignmentStore(_dataDirectory);

        var result = store.Load(
            ChartIdFor('5'),
            noteCount: 3,
            new LegacyHandAssignmentCandidate(legacyPath, IsUnambiguous: true));

        Assert.Equal(Enumerable.Repeat(PianoHand.Right, 3), result.Hands);
        Assert.False(result.MigratedLegacyData);
        Assert.NotNull(result.Warning);
        Assert.True(File.Exists(legacyPath));
    }

    [Fact]
    public void CorruptChartAssignmentsArePreservedAndNotOverwritten()
    {
        var chartId = ChartIdFor('6');
        var store = new ChartHandAssignmentStore(_dataDirectory);
        store.Save(chartId, new[] { PianoHand.Left });
        var assignmentPath = Directory.GetFiles(
            Path.Combine(_dataDirectory, "ChartHandAssignments"),
            "*.json").Single();
        File.WriteAllText(assignmentPath, "not valid JSON");

        var loaded = store.Load(chartId, noteCount: 1);
        var saved = store.Save(chartId, new[] { PianoHand.Right });

        Assert.Equal(new[] { PianoHand.Right }, loaded.Hands);
        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.NotNull(saved.Warning);
        Assert.Equal("not valid JSON", File.ReadAllText(assignmentPath));
    }

    [Theory]
    [InlineData(false, "Left")]
    [InlineData(true, "99")]
    public void IncompatibleChartAssignmentsArePreservedAndNotOverwritten(
        bool includeVersion,
        string hand)
    {
        var chartId = ChartIdFor('7');
        var version = includeVersion ? "\"Version\":1," : string.Empty;
        var incompatible = $"{{{version}\"ChartId\":\"{chartId.Value}\",\"Hands\":[\"{hand}\"]}}";
        var store = new ChartHandAssignmentStore(_dataDirectory);
        store.Save(chartId, new[] { PianoHand.Left });
        var assignmentPath = Directory.GetFiles(
            Path.Combine(_dataDirectory, "ChartHandAssignments"),
            "*.json").Single();
        File.WriteAllText(assignmentPath, incompatible);

        var loaded = store.Load(chartId, noteCount: 1);
        var saved = store.Save(chartId, new[] { PianoHand.Right });

        Assert.Equal(new[] { PianoHand.Right }, loaded.Hands);
        Assert.NotNull(loaded.Warning);
        Assert.False(saved.Saved);
        Assert.Equal(incompatible, File.ReadAllText(assignmentPath));
    }

    [Fact]
    public void StorageFailureReturnsAWarningInsteadOfThrowing()
    {
        Directory.CreateDirectory(_dataDirectory);
        var blockedRoot = Path.Combine(_dataDirectory, "Blocked");
        File.WriteAllText(blockedRoot, "This path is a file, not a directory.");

        var result = new ChartHandAssignmentStore(blockedRoot).Save(
            ChartIdFor('b'),
            new[] { PianoHand.Right });

        Assert.False(result.Saved);
        Assert.NotNull(result.Warning);
    }

    private static ChartId ChartIdFor(char hexadecimalDigit)
    {
        return ChartId.Parse($"chart-v1-sha256:{new string(hexadecimalDigit, 64)}");
    }

    private string WriteLegacyAssignments(string fileName, params bool[] isRight)
    {
        Directory.CreateDirectory(_dataDirectory);
        var values = string.Concat(isRight.Select(value => $"<boolean>{value.ToString().ToLowerInvariant()}</boolean>"));
        var path = Path.Combine(_dataDirectory, fileName);
        File.WriteAllText(
            path,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            $"<LeftRightData><IsRightNote>{values}</IsRightNote></LeftRightData>");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, recursive: true);
    }
}
