# Upstream issue candidates (2026-08-22)

## Recommendation

Start with [#24, SoundFont sustain pedal behavior](https://github.com/ImAxel0/Openthesia/issues/24). It is the best combination of a confirmed user-facing bug, a source-visible cause, bounded implementation scope, and deterministic acceptance tests. If we want the safest small first contribution instead, choose [#37, playback speed slider](https://github.com/ImAxel0/Openthesia/issues/37). [#36, repeated notes in play-along mode](https://github.com/ImAxel0/Openthesia/issues/36) is also valuable, but only as an explicit takeover/review of the contributor's stale unmerged solution, not as greenfield work.

Ranked shortlist:

1. [#24 — Sustain Pedal doesn't work as expected](https://github.com/ImAxel0/Openthesia/issues/24)
2. [#37 — Slider for playback speed](https://github.com/ImAxel0/Openthesia/issues/37)
3. [#6 — Colored keypresses only ever use the right-hand color](https://github.com/ImAxel0/Openthesia/issues/6)
4. [#29 — MIDI input octave shift](https://github.com/ImAxel0/Openthesia/issues/29)
5. [#38 — Keyboard Mapping](https://github.com/ImAxel0/Openthesia/issues/38)

## Baseline and method

The fork's `master`, its `origin/master`, and upstream `master` all point to commit [`04d6e37`](https://github.com/ImAxel0/Openthesia/commit/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc). Therefore, none of the current fork-specific code fixes an upstream issue; the fork is a clean upstream 1.5.3 baseline. The assessment used the open upstream issues, upstream PRs and commits, and source at that exact commit. It did not rely on third-party descriptions.

There is no test project in the current [solution](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia.sln), so each implementation should introduce a small testable seam around the affected state transition rather than relying only on manual MIDI-device testing.

## Candidate detail: #24 — SoundFont sustain pedal behavior

**Verdict: best first issue.**

- **Clarity and impact:** The reporter gives an exact sequence: play with sustain on, keep a key physically held, then release the pedal; the held key is incorrectly silenced. The owner confirmed that this occurs in the SoundFonts engine, and a second user independently confirmed the issue. This affects basic piano technique in the built-in sound path ([issue and owner confirmation](https://github.com/ImAxel0/Openthesia/issues/24)).
- **Source evidence:** The current handler adds a pitch to `_sustainedNotes` both when it is pressed during sustain and when it is released during sustain. Pedal-up then sends NoteOff for every pitch in that set without checking whether the key remains physically held ([`IOHandle.cs` lines 13–119](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia/Core/IOHandle.cs#L13-L119)). That directly explains the reported behavior. The `HashSet<int>` also discards multiplicity, so repeated NoteOn events for one pitch deserve explicit coverage.
- **Scope and risk:** Bounded primarily to `IOHandle`'s note/sustain state, with medium musical-state risk. A robust fix should distinguish physically held notes from pedal-latched notes and define same-pitch repeated NoteOn/NoteOff behavior. The SoundFont path should be changed without disrupting the existing pass-through behavior for plugin instruments.
- **Reproducibility/testability:** Drive the handler with NoteOn(C4), pedal-on, NoteOn/NoteOff variants, and pedal-off events while recording calls to a fake SoundFont output. At pedal-up, only latched-but-not-held pitches should receive NoteOff; a still-held pitch should stop only after its own NoteOff.
- **Overlap:** No open PR directly claims #24. [PR #59](https://github.com/ImAxel0/Openthesia/pull/59) touches `IOHandle.cs`, but only guards a missing `NoteRects` index; it does not alter sustain state. Expect a small integration/rebase consideration, not duplicate work.
- **Already fixed in this fork?** No; the source-visible failure path is present at the fork's current commit.

## Existing-work candidate: #36 — Require a fresh press for consecutive notes

**Verdict: excellent takeover candidate if we coordinate, adopt, and validate the existing work.**

- **Clarity and impact:** The issue includes a concrete demonstration: when the song contains consecutive notes of the same pitch, keeping the key held incorrectly satisfies every note. That undermines the core play-along exercise ([issue #36](https://github.com/ImAxel0/Openthesia/issues/36)).
- **Source evidence:** Learning mode currently decides that a note is satisfied solely from whether `PressedKeys` contains that pitch as the next block reaches the keyboard ([`ScreenCanvas.cs` lines 253–274](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia/Ui/ScreenCanvas.cs#L253-L274)). It has no key-release edge or per-note attempt state, so a held pitch necessarily satisfies adjacent same-pitch notes.
- **Scope and risk:** The direct behavior is localized to learning-mode gating. Risk is medium because state must reset correctly on seek, restart, song change, hand toggles, and overlapping same-pitch notes.
- **Reproducibility/testability:** Use a two-note MIDI fixture containing adjacent C4 notes. Holding C4 through the boundary must pause at the second note; releasing and pressing C4 again must continue. Repeat after seeking and restarting.
- **Overlap:** A contributor linked a small, mostly functional [25-line commit `8f54bfd`](https://github.com/ImAxel0/Openthesia/commit/8f54bfda360a16bb384adf9c353376756f332fe5) that tracks played note indices and the last note per pitch. It was never merged and the current master still contains the old condition. The best task is to port/review that idea, add reset behavior and tests, and credit the original author. [PR #59](https://github.com/ImAxel0/Openthesia/pull/59) also edits `ScreenCanvas.cs` but not this learning-mode block.
- **Already fixed in this fork?** No. The linked commit is not on upstream or fork `master`.

## Candidate detail: #6 — Preserve left/right hand color on the keyboard

**Verdict: a good medium-sized visual correctness issue.**

- **Clarity and impact:** The report says notes reassigned to the other hand use the correct falling-note color, but the corresponding keyboard key always lights with the right-hand color. A second user confirmed the behavior ([issue #6](https://github.com/ImAxel0/Openthesia/issues/6)).
- **Source evidence:** When `Colored keypresses` is enabled, both white and black pressed keys explicitly use `ThemeManager.RightHandCol`; no left/right provenance reaches the keyboard renderer ([`PianoRenderer.cs` lines 84–151](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia/Ui/PianoRenderer.cs#L84-L151)). This is still present despite prior work that added hand activation and per-note hand data.
- **Scope and risk:** Medium. The renderer needs the active playback note's hand, not merely a global pressed-pitch list. The main edge case is simultaneous or overlapping notes of the same pitch assigned to different hands; define a deterministic color rule rather than adding a boolean keyed only by pitch.
- **Reproducibility/testability:** Load a minimal MIDI with one left-hand and one right-hand note, enable colored keypresses, and assert the renderer's chosen key color for each. Add an overlapping-same-pitch case.
- **Overlap:** No open upstream PR or linked commit currently claims this issue, and [PR #59](https://github.com/ImAxel0/Openthesia/pull/59) does not touch `PianoRenderer.cs`.
- **Already fixed in this fork?** No; the hard-coded right-hand color remains.

## Candidate detail: #29 — Add a MIDI input octave shift

**Verdict: a good contained enhancement after the correctness bugs.**

- **Clarity and impact:** The reported 61-key controller starts at C2 and cannot reach the application's lowest octave. The owner classified this as expected hardware range rather than a bug, but explicitly proposed an input-octave-shift setting ([issue #29 and owner response](https://github.com/ImAxel0/Openthesia/issues/29)).
- **Source evidence:** Computer-keyboard input already has a `-36..+36` semitone octave shift, but its key map and shift are private to `VirtualKeyboard` ([`VirtualKeyboard.cs` lines 10–74](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia/Core/VirtualKeyboard.cs#L10-L74)). Physical MIDI input is subscribed directly to `IOHandle.OnEventReceived` with no transform layer ([`DevicesManager.cs` lines 11–44](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia/Settings/DevicesManager.cs#L11-L44)).
- **Scope and risk:** Medium-small: a persisted setting, a settings control, and a reusable NoteOn/NoteOff transposition step. Clamp or reject results outside MIDI note range 0–127, and ensure NoteOn/NoteOff pairs use the same transformed pitch.
- **Reproducibility/testability:** Parameterized tests for shifts of -36 through +36 semitones, boundaries at notes 0 and 127, velocity-zero NoteOn handling, and matched NoteOff output.
- **Overlap:** No open upstream PR claims the feature. PR #59 changes other portions of `SettingsWindow` and `ProgramData`, so coordination may reduce trivial merge conflicts.
- **Already fixed in this fork?** No.

## Candidate detail: #37 — Playback speed slider

**Verdict: lowest-risk polish task, but lower user impact and one UX choice is still open.**

- **Clarity and impact:** The request asks to replace discrete speed multipliers with either a continuous slider or a BPM control ([issue #37](https://github.com/ImAxel0/Openthesia/issues/37)). It is clear enough once one of those alternatives is chosen, but the issue itself does not decide between them.
- **Source evidence:** Playback speed is currently a dropdown enumerating multipliers from `0.25x` to `4x` in `0.25x` increments ([`ScreenCanvas.cs` lines 900–913](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia/Ui/ScreenCanvas.cs#L900-L913)). A multiplier slider maps directly to the existing `Playback.Speed` model; BPM input would require tempo-map semantics and is a broader feature.
- **Scope and risk:** Small if scoped to a clamped multiplier slider, larger if interpreted as editing/displaying song BPM. Keep the first version as a multiplier slider and preserve keyboard/mouse interaction accessibility.
- **Reproducibility/testability:** Assert clamping, chosen granularity, persistence during playback, and synchronization after seeking. Manual UI checks cover keyboard and mouse adjustment.
- **Overlap / fork status:** No open PR claims #37. [PR #62](https://github.com/ImAxel0/Openthesia/pull/62) changes mouse-wheel speed input in the same file while preserving the existing 0.25x steps; semantic overlap is low, but the slider work should incorporate or follow that small input patch. The current fork still has the dropdown.

## Candidate detail: #38 — Configurable computer-keyboard mapping

**Verdict: a good medium-sized feature once the binding UX is agreed.**

- **Clarity and impact:** The user asks to remap which computer keys play each note ([issue #38](https://github.com/ImAxel0/Openthesia/issues/38)). This is useful for alternate keyboard layouts and personal/accessibility preferences, though the issue does not specify the binding interface.
- **Source evidence:** The note bindings are a private, read-only dictionary from `ImGuiKey` to MIDI pitch ([`VirtualKeyboard.cs` lines 13–28](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia/Core/VirtualKeyboard.cs#L13-L28)). Current settings persist only whether keyboard input is enabled, not the map ([`SettingsData.cs`](https://github.com/ImAxel0/Openthesia/blob/04d6e378f178fc3e2d9c4b91793cab1b4f9841bc/Openthesia/Settings/SettingsData.cs)).
- **Scope and risk:** Medium: editable bindings, persistence, reset-to-default, duplicate/reserved-key validation, and a capture UI. The mapping and serialization are straightforward; the interaction design is the main uncertainty.
- **Reproducibility/testability:** Unit-test default and custom mappings, serialization round-trips, duplicates, and reserved octave/velocity keys. Manually test key capture and reset behavior.
- **Overlap / fork status:** No PR claims #38. PR #59 makes broad changes in `SettingsWindow`, so rebasing after or incorporating that branch would reduce merge friction. The fork still has the hard-coded map.

## Issues to defer or coordinate

- **Do not duplicate #44/#51 right now:** open [PR #60](https://github.com/ImAxel0/Openthesia/pull/60) explicitly fixes both ASIO startup-crash issues by falling back to WaveOut. The fork does not contain it, but upstream work is active.
- **Do not take #56 as one ticket:** it combines six unrelated features. Its recursive-search portion already overlaps open [PR #59](https://github.com/ImAxel0/Openthesia/pull/59). Any remaining item should first become a separate issue with acceptance criteria.
- **Investigate #50 before implementing:** it has high impact and multiple confirmations, but no crash trace or precise failing dependency. PR #59 claims an ImGui crash fix and broad error-handling changes, so first verify whether its branch resolves the report ([issue #50](https://github.com/ImAxel0/Openthesia/issues/50), [PR #59](https://github.com/ImAxel0/Openthesia/pull/59)).
- **Ask for fixtures on #35/#55:** skipped notes are core-impacting, but neither report supplies a MIDI fixture and #55 has no mode/device details. The behavior also follows earlier merged play-along fixes, so reproducibility comes before code changes ([#35](https://github.com/ImAxel0/Openthesia/issues/35), [#55](https://github.com/ImAxel0/Openthesia/issues/55), prior merged [PR #25](https://github.com/ImAxel0/Openthesia/pull/25)).
- **Defer the platform/large-feature requests:** macOS packaging (#49), project files (#48), sheet music/passage practice (#30), scale-degree visualization (#33), and the three-feature bundle (#22) are substantially broader or underspecified. The upstream owner explicitly says sheet music would require an experienced contributor ([issue #30](https://github.com/ImAxel0/Openthesia/issues/30)).
- **Defer #32 unless it becomes reproducible:** upstream labels the duplicate virtual-key press report `cannot reproduce`, and it has no sample environment or event trace ([issue #32](https://github.com/ImAxel0/Openthesia/issues/32)).

## Suggested first ticket boundary

For #24, keep the first contribution narrowly defined: model physical key-down state separately from pedal-latched state for the SoundFont engine; add deterministic tests for held, released, repeated, and same-pitch notes; leave unrelated MIDI routing and volume changes out. That boundary is small enough to review and valuable enough to validate the fork's contribution workflow.
