# Sustain pedal held-note regression

This check reproduces the SoundFont sustain-pedal bug described in local issue #1 and verifies the fix.

## Equipment and setup

- A MIDI keyboard with a sustain pedal that sends MIDI control change 64 (CC64)
- A configured SoundFont, MIDI input, and audio output in Openthesia
- SoundFonts selected as the sound engine

## Reproduce the original bug

1. Open Play Mode.
2. Press and hold the sustain pedal.
3. Press and continue holding middle C (MIDI note 60).
4. Release the sustain pedal while keeping middle C physically held.

Before the fix, middle C stops sounding at step 4. With the fix, it continues sounding until the key itself is released.

## Control check

1. Press and hold the sustain pedal.
2. Press middle C, then release the key while continuing to hold the pedal.
3. Release the sustain pedal.

Middle C should continue sounding after step 2 and stop at step 3.

## Automated reproduction

Run the focused regression test from the repository root:

```powershell
dotnet test Openthesia.Tests\Openthesia.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~ReleasingPedalDoesNotStopNoteThatIsStillHeld
```

The test fails against the original sustain-state behavior because pedal release returns note 60 as a note to stop. It passes with the corrected behavior.
