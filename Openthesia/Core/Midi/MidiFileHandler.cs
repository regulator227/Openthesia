using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using Openthesia.Core.FileDialogs;
using Openthesia.Core.Plugins;
using Openthesia.Core.Practice;
using Openthesia.Core.Songs;
using Openthesia.Settings;
using Vanara.PInvoke;

namespace Openthesia.Core.Midi;

public static class MidiFileHandler
{
    public static void LoadMidiFile(string filePath)
    {
        var sourcePath = Path.GetFullPath(filePath);
        var midiFile = MidiFile.Read(sourcePath);
        LoadMidiFileCore(midiFile);

        MidiFileData.FileName = Path.GetFileName(sourcePath);
        var chartId = ChartIdentity.FromMidi(midiFile);
        ResolvedSongChart? songChart = null;
        try
        {
            songChart = new SongCatalog(ProgramData.DataPath)
                .ResolveMidiSource(sourcePath, chartId);
        }
        catch (Exception exception)
        {
            User32.MessageBox(
                IntPtr.Zero,
                $"{exception.Message}\n\nThe MIDI can still be played, but its Song metadata was preserved and was not updated.",
                "Couldn't load Song metadata",
                User32.MB_FLAGS.MB_ICONERROR | User32.MB_FLAGS.MB_TOPMOST);
        }
        MidiFileData.Context = MidiLoadContext.FromSource(sourcePath, chartId, songChart);

        Program._window.Title = $"Openthesia ({MidiFileData.FileName})";
    }

    public static void LoadMidiFile(MidiFile midi)
    {
        LoadMidiFileCore(midi);
        MidiFileData.FileName = "Unsaved recording";
        MidiFileData.Context = MidiLoadContext.Transient(ChartIdentity.FromMidi(midi));
        Program._window.Title = $"Openthesia ({MidiFileData.FileName})";
    }

    private static void LoadMidiFileCore(MidiFile midiFile)
    {
        MidiPracticeSession.Deactivate();
        MidiFileData.MidiFile = midiFile;
        MidiFileData.TempoMap = midiFile.GetTempoMap();
        MidiFileData.Notes = ChartPattern.GetCanonicalNotes(midiFile)
            .Select(note => note.Note)
            .ToArray();

        if (MidiPlayer.Playback != null)
        {
            MidiPlayer.Playback.Stop();
            MidiPlayer.Playback.EventPlayed -= IOHandle.OnEventReceived;

            PlaybackCurrentTimeWatcher.Instance.Stop();
            PlaybackCurrentTimeWatcher.Instance.CurrentTimeChanged -= MidiPlayer.OnCurrentTimeChanged;
            PlaybackCurrentTimeWatcher.Instance.RemovePlayback(MidiPlayer.Playback);
        }

        MidiPlayer.Playback = DevicesManager.ODevice != null
            ? midiFile.GetPlayback(DevicesManager.ODevice) : midiFile.GetPlayback();

        MidiPlayer.Playback.TrackNotes = true;
        MidiPlayer.Playback.TrackProgram = true;
        MidiPlayer.Playback.EventPlayed += IOHandle.OnEventReceived;
        MidiPlayer.Playback.Finished += MidiPlayer.Playback_Finished;
        MidiPlayer.Playback.NoteCallback = NoteCallback.HandMutingNoteCallback;

        PlaybackCurrentTimeWatcher.Instance.AddPlayback(MidiPlayer.Playback, TimeSpanType.Midi);
        PlaybackCurrentTimeWatcher.Instance.CurrentTimeChanged += MidiPlayer.OnCurrentTimeChanged;
        PlaybackCurrentTimeWatcher.Instance.Start();
    }

    public static bool OpenMidiDialog()
    {
        var dialog = new OpenFileDialog()
        {
            Title = "Select a midi file",
            Filter = "MIDI files (*.mid;*.midi)|*.mid;*.midi"
        };
        dialog.ShowOpenFileDialog();

        if (dialog.Success)
        {
            var file = new FileInfo(dialog.Files.First());
            try
            {
                LoadMidiFile(file.FullName);
                return true;
            }
            catch (Exception exception)
            {
                User32.MessageBox(
                    IntPtr.Zero,
                    $"Couldn't open {file.Name}: {exception.Message}",
                    "Couldn't open MIDI file",
                    User32.MB_FLAGS.MB_ICONERROR | User32.MB_FLAGS.MB_TOPMOST);
            }
        }
        return false;
    }
}
