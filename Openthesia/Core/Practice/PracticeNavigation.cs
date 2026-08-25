namespace Openthesia.Core.Practice;

public sealed record PracticeLoop(
    Guid Id,
    string Name,
    PracticeRange Range);

public sealed record PracticeBookmark(
    Guid Id,
    string Name,
    ChartTime Position);

public enum PracticeNavigationDirection
{
    Previous,
    Next
}

public sealed class PracticeNavigation : IEquatable<PracticeNavigation>
{
    private readonly PracticeBookmark[] _sortedBookmarks;

    public static PracticeNavigation Empty { get; } = new(
        Array.Empty<PracticeLoop>(),
        Array.Empty<PracticeBookmark>());

    public PracticeNavigation(
        IReadOnlyList<PracticeLoop> loops,
        IReadOnlyList<PracticeBookmark> bookmarks)
    {
        ArgumentNullException.ThrowIfNull(loops);
        ArgumentNullException.ThrowIfNull(bookmarks);
        Loops = loops.ToArray();
        Bookmarks = bookmarks.ToArray();
        _sortedBookmarks = Bookmarks
            .OrderBy(bookmark => bookmark.Position)
            .ThenBy(bookmark => bookmark.Id)
            .ToArray();
    }

    public IReadOnlyList<PracticeLoop> Loops { get; }
    public IReadOnlyList<PracticeBookmark> Bookmarks { get; }

    public PracticeNavigation SaveLoop(
        Guid id,
        string? name,
        PracticeRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        var loop = new PracticeLoop(id, NormalizeName(name, "Loop"), range);
        return new PracticeNavigation(
            Upsert(Loops, loop, item => item.Id),
            Bookmarks);
    }

    public PracticeNavigation DeleteLoop(Guid id)
    {
        return new PracticeNavigation(
            Loops.Where(loop => loop.Id != id).ToArray(),
            Bookmarks);
    }

    public PracticeNavigation SaveBookmark(
        Guid id,
        string? name,
        ChartTime position)
    {
        var bookmark = new PracticeBookmark(
            id,
            NormalizeName(name, "Bookmark"),
            position);
        return new PracticeNavigation(
            Loops,
            Upsert(Bookmarks, bookmark, item => item.Id));
    }

    public PracticeNavigation DeleteBookmark(Guid id)
    {
        return new PracticeNavigation(
            Loops,
            Bookmarks.Where(bookmark => bookmark.Id != id).ToArray());
    }

    public PracticeBookmark? FindBookmark(
        ChartTime playhead,
        PracticeNavigationDirection direction)
    {
        if (Bookmarks.Count == 0)
            return null;

        var boundary = UpperBound(playhead);
        if (direction == PracticeNavigationDirection.Next)
            return boundary < _sortedBookmarks.Length ? _sortedBookmarks[boundary] : _sortedBookmarks[0];

        var previous = LowerBound(playhead) - 1;
        return previous >= 0 ? _sortedBookmarks[previous] : _sortedBookmarks[^1];
    }

    public bool IsValid(ChartTime chartDuration)
    {
        return chartDuration.CompareTo(ChartTime.Zero) > 0 &&
               Loops.Select(loop => loop.Id).Distinct().Count() == Loops.Count &&
               Bookmarks.Select(bookmark => bookmark.Id).Distinct().Count() == Bookmarks.Count &&
               Loops.All(loop =>
                   loop.Id != Guid.Empty &&
                   ValidName(loop.Name) &&
                   loop.Range.Start.CompareTo(ChartTime.Zero) >= 0 &&
                   loop.Range.End.CompareTo(loop.Range.Start) > 0 &&
                   loop.Range.End.CompareTo(chartDuration) <= 0) &&
               Bookmarks.All(bookmark =>
                   bookmark.Id != Guid.Empty &&
                   ValidName(bookmark.Name) &&
                   bookmark.Position.CompareTo(ChartTime.Zero) >= 0 &&
                   bookmark.Position.CompareTo(chartDuration) <= 0);
    }

    public bool Equals(PracticeNavigation? other)
    {
        return other is not null &&
               Loops.SequenceEqual(other.Loops) &&
               Bookmarks.SequenceEqual(other.Bookmarks);
    }

    public override bool Equals(object? obj)
    {
        return obj is PracticeNavigation other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var loop in Loops)
            hash.Add(loop);
        foreach (var bookmark in Bookmarks)
            hash.Add(bookmark);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<T> Upsert<T>(
        IReadOnlyList<T> items,
        T replacement,
        Func<T, Guid> id)
    {
        var replaced = false;
        var result = items.Select(item =>
        {
            if (id(item) != id(replacement))
                return item;
            replaced = true;
            return replacement;
        }).ToList();
        if (!replaced)
            result.Add(replacement);
        return result;
    }

    private int LowerBound(ChartTime position)
    {
        var low = 0;
        var high = _sortedBookmarks.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_sortedBookmarks[middle].Position.CompareTo(position) < 0)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private int UpperBound(ChartTime position)
    {
        var low = 0;
        var high = _sortedBookmarks.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_sortedBookmarks[middle].Position.CompareTo(position) <= 0)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private static string NormalizeName(string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= 80 ? normalized : normalized[..80];
    }

    private static bool ValidName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Length <= 80;
    }
}
