namespace Openthesia.Core.Midi;

public sealed class SustainState
{
    private readonly Dictionary<int, int> _heldNoteCounts = new();
    private readonly HashSet<int> _sustainedNotes = new();

    public bool IsPedalActive { get; private set; }

    public void NotePressed(int noteNumber)
    {
        _heldNoteCounts[noteNumber] = _heldNoteCounts.GetValueOrDefault(noteNumber) + 1;
    }

    public bool NoteReleased(int noteNumber)
    {
        var heldCount = _heldNoteCounts.GetValueOrDefault(noteNumber);
        if (heldCount > 1)
        {
            _heldNoteCounts[noteNumber] = heldCount - 1;
            return false;
        }

        if (heldCount == 1)
        {
            _heldNoteCounts.Remove(noteNumber);
        }

        if (IsPedalActive)
        {
            _sustainedNotes.Add(noteNumber);
            return false;
        }

        return true;
    }

    public void PressPedal()
    {
        IsPedalActive = true;
    }

    public IReadOnlyCollection<int> ReleasePedal()
    {
        IsPedalActive = false;
        var notesToStop = _sustainedNotes
            .Where(noteNumber => !_heldNoteCounts.ContainsKey(noteNumber))
            .ToArray();
        _sustainedNotes.Clear();
        return notesToStop;
    }
}
