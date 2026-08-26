# Upstream pull-request audit (2026-08-22)

## Recommendation

There are eight open pull requests in [`ImAxel0/Openthesia`](https://github.com/ImAxel0/Openthesia/pulls). None has an upstream review or automated GitHub check. The best near-term imports are #61 and #62; #60 addresses a serious startup trap but should be strengthened before adoption. #58 is a useful clue rather than a complete build fix. The remaining PRs should be conditional, adapted, or skipped.

| PR | Claimed feature or fix | Does the code do it? | Fork plan |
| --- | --- | --- | --- |
| [#61](https://github.com/ImAxel0/Openthesia/pull/61) | Make the note-label menu reachable and selectable | **Yes, with a manual UI check still needed.** It moves the menu under the button, keeps the top bar open while the button/menu is hovered, and finally calls `SetTextType` when an item is selected. | **Plan to merge.** Reproduce issue #5 before and after, then import as a small fork-local PR. |
| [#62](https://github.com/ImAxel0/Openthesia/pull/62) | Ctrl+mouse-wheel timeline scrubbing while preserving bare-wheel speed changes | **Yes by code inspection.** Ctrl+wheel clamps and moves playback time; bare wheel retains the existing 0.25× speed adjustment. | **Plan to merge.** Add a short manual input check for direction, endpoints, pause, and active playback. |
| [#60](https://github.com/ImAxel0/Openthesia/pull/60) | Warn and fall back to WaveOut when ASIO is unavailable | **Partly.** Missing drivers and `AsioOut` constructor failures fall back correctly, but `AsioOut.Init` and `Play` remain outside the `try`, so sample-rate/device-initialization failures can still crash startup. | **Adapt manually, high priority.** Wrap the complete ASIO setup and test failure at discovery, construction, initialization, and playback. |
| [#58](https://github.com/ImAxel0/Openthesia/pull/58) | Make x64 the default platform so the project builds | **Partly.** The direct `.csproj` build succeeds without a platform argument, but the normal solution build still selects `AnyCPU` and fails ScreenRecorderLib's platform check. | **Adapt manually.** Fix the solution/default configuration coherently instead of importing only the one-line project property. |
| [#57](https://github.com/ImAxel0/Openthesia/pull/57) | Fix delayed touchscreen taps and home-title/logo overlap | **Likely.** The touch code correctly roots and installs a replacement Windows window procedure, and the layout calculation places the title above the logo. The author reports real Windows 11 touchscreen/3K testing; this audit had no matching hardware. | **Conditional.** Cherry-pick its two commits separately if touchscreen or high-resolution layout matters to this fork. |
| [#63](https://github.com/ImAxel0/Openthesia/pull/63) | Replace glow toggle with intensity slider; render glows behind all notes | **Mostly.** The slider, 0/100 keyboard toggle, layered glow, and glow/body/label draw order are implemented. However, a fresh run defaults to 70 rather than the PR body's claimed 0, while old settings silently migrate to 0; maximum intensity also lightens note fills all the way toward white. | **Defer and adapt.** Decide the intended default/migration and profile the 20-layer-per-note renderer before importing. |
| [#59](https://github.com/ImAxel0/Openthesia/pull/59) | Recursive search, broad crash/error handling, optional recorder, dependency updates | **Mixed and unsafe as a bundle.** Recursive search and the note-index guard are real, but the crash claims lack a reproducible trace; the recorder is replaced with a large reflection layer; the app jumps to .NET 9; and a newly pinned ImageSharp version has multiple known vulnerabilities. | **Do not merge directly.** Reimplement recursive search and the index guard as separate, tested changes; evaluate runtime/dependency and recorder work independently. |
| [#8](https://github.com/ImAxel0/Openthesia/pull/8) | Map physical MIDI control buttons to play/pause/stop/record/seek | **Not reliably.** The old head compiles and routes/persists mappings, but it assumes exact CC values 127/0, leaves its mapping array uninitialized on a true first run, and is based on the pre-refactor architecture. | **Skip direct merge.** If wanted, design a new device-control feature against current code with configurable press/release semantics and tests. |

## Verification baseline

- The fork baseline is `master` at [`174ad20`](https://github.com/regulator227/Openthesia/commit/174ad20), which includes the fork's sustain-state fix and five regression tests.
- I inspected every open PR's description, commits, changed files, comments, reviews, merge state, and exact diff. The inventory was taken from GitHub's PR data and the immutable head commits linked below, not from PR titles alone.
- All eight exact PR heads compile locally in Release/x64. This is only compile evidence; none of the upstream PRs supplies tests or has GitHub checks.
- #57, #58, #60, #61, #62, and #63 each merge cleanly into the fork and individually preserve all five fork tests. They also stack together cleanly and preserve the same tests. Those tests cover sustain behavior, not the imported UI/audio features.
- #59 textually merges, but the combined solution cannot restore: its app targets `net9.0-windows`, while the fork's test project still targets `net6.0`, producing `NU1201`.
- #8's historical head compiles on its old tree, but merging it into the fork produces six source conflicts because the project was reorganized after its 2024 base.

## PR #61 — note-label menu selection

**Verdict: merge candidate.**

The original report says the menu is separated from its button by a large gap, so moving the cursor closes it before an option can be selected ([issue #5](https://github.com/ImAxel0/Openthesia/issues/5)). The patch:

- records the label button's rectangle;
- places a child menu immediately below it;
- treats either the button or child menu as the active hover region;
- keeps the top control bar visible while that state is active; and
- calls `SetTextType(textType)` on selection.

Those changes directly repair both reachability and actual click selection in the [single `ScreenCanvas.cs` commit](https://github.com/ImAxel0/Openthesia/commit/9c47b6a1bae3f545e4919922fa4c4480529fdebd). The patch does not address the later color-picker-gap comment on issue #5, so the fork should describe its local issue narrowly. Manual checks should cover 1080p and window resizing because the behavior depends on immediate-mode hover geometry.

## PR #62 — Ctrl+wheel timeline scrubbing

**Verdict: merge candidate.**

The [one-file change](https://github.com/ImAxel0/Openthesia/commit/214c6cab8dc64af88ae48c1cb951a645b59d7aa2) branches existing mouse-wheel handling on Ctrl. With Ctrl held, it subtracts `MouseWheel * 0.5` seconds, clamps to the song duration, calls `MoveToTime`, and synchronizes `MidiPlayer.Seconds` and the falling-note timer. ImGui's positive wheel direction therefore goes backward and negative goes forward, matching the PR body. Without Ctrl it applies the same 0.25× step and 0.25×–4× clamp as before.

The code performs the claim and has a narrow integration surface. A manual test should still verify high-resolution wheels, start/end clamps, paused playback, active playback, and that hovering the note-label control continues to suppress speed/scrub input.

## PR #60 — ASIO fallback

**Verdict: valuable bug fix, but adapt before merging.**

Issues [#44](https://github.com/ImAxel0/Openthesia/issues/44) and [#51](https://github.com/ImAxel0/Openthesia/issues/51) describe users becoming locked out after ASIO is persisted: startup crashes before they can return to settings. The [PR diff](https://github.com/ImAxel0/Openthesia/commit/cdd8f7fd4b1d421207a6b9918abaa31a9a6380d5) adds a shared `TryCreateAsioOut` path for both SoundFont and VST playback. It correctly:

- detects an empty driver list;
- substitutes the first installed driver when the stored name is invalid;
- catches constructor failure;
- warns the user; and
- creates WaveOut for that session.

The weakness is the boundary: `_asioOut.Init(...)` and `_asioOut.Play()` execute after the helper returns, outside its `try`. Those operations can be the point where an unavailable format or device fails, particularly relevant to issue #44's sample-rate discussion. The fork should model "try complete ASIO startup" rather than "try construct ASIO object," dispose partial output on failure, and test the state transition behind a small audio-output factory seam. Direct hardware tests should include no ASIO installed, stale stored name, busy/disconnected device, and unsupported sample rate.

## PR #58 — default x64 platform

**Verdict: useful diagnosis, incomplete repository fix.**

The [entire PR](https://github.com/ImAxel0/Openthesia/commit/2404662cb2f7517788e5ecdc9a4702236473992a) adds `<Platform>x64</Platform>` to the application project. That makes a direct `dotnet build Openthesia/Openthesia.csproj` select x64 and compile successfully, validating the contributor's immediate claim.

It does not fix `dotnet build Openthesia.sln`: the solution's global `Any CPU` selection overrides the project-local default, and ScreenRecorderLib still rejects the build. The same failure occurs after importing it into this fork. A fork-local build issue should instead define x64 coherently in the solution/default build workflow and then verify both the solution and test project without a special command-line property.

## PR #57 — touch input and home title

**Verdict: conditional import.**

The first [commit](https://github.com/ImAxel0/Openthesia/commit/0ff209ed25e10d84a6eae4f6342d826330e7e501) handles `WM_TABLET_QUERYSYSTEMGESTURESTATUS`, returns the standard flags disabling press-and-hold and feedback gestures, installs the procedure through the 64-bit Unicode Windows API, and retains the delegate in a static field so it cannot be garbage-collected. That implementation matches the claimed input path. The contributor reports it was tested on a Windows 11 touchscreen, but the change has no automated or independent hardware result.

The second [commit](https://github.com/ImAxel0/Openthesia/commit/4abde91c281b40ff77510ed8df0f3240b0c381b4) derives the title's Y position from the logo's top edge and skips it if there is insufficient space. This prevents overlap at tall resolutions, though the pre-existing `<1079px` early return still hides the title on shorter displays. Because the concerns are independent, preserve the two-commit split if importing them.

## PR #63 — configurable note glow

**Verdict: real feature, but revise before adoption.**

The [implementation commit](https://github.com/ImAxel0/Openthesia/commit/dc5bf1d540171b62da272d78e40514328e1ed8d6) genuinely replaces the boolean with a clamped 0–100 integer, adds sliders in settings and playback controls, makes `G` select 0 or 100, and separates rendering into glow, fill, and label passes. This resolves the overlap problem described in the PR and makes glow intensity visibly adjustable.

Three details prevent a direct recommendation:

1. The PR body says the first-start value is 0, but `CoreSettings` initializes it to 70. A genuinely fresh run therefore starts at 70.
2. Existing JSON has `NeonFx` but not `NotesGlowIntensity`; deserialization supplies integer 0, silently turning glow off for existing users regardless of their old choice. This needs an explicit migration rule.
3. Each glowing note can add up to 20 filled rectangles every frame, and intensity 100 blends note fills fully toward white. The screenshots demonstrate the visual result, but there is no frame-time or dense-MIDI evidence.

Import the idea after choosing a default and migration policy, capping or profiling render cost on dense songs, and deciding how much the hand colors should be lightened.

## PR #59 — recursive search, crashes, recorder, and dependencies

**Verdict: do not merge as a bundle.**

The three commits under [PR #59](https://github.com/ImAxel0/Openthesia/pull/59) contain some valid small changes:

- MIDI discovery changes to recursive `Directory.GetFiles(..., SearchOption.AllDirectories)` and skips directories that throw, implementing the recursive-search portion of [issue #56](https://github.com/ImAxel0/Openthesia/issues/56).
- `IOHandle` checks `FindIndex` before indexing `NoteRects`, preventing the claimed `ArgumentOutOfRangeException` at that site.
- Several nullable accesses are guarded.

The PR then couples those fixes to a .NET 6 → .NET 9 migration, multiple package updates, an `AutoFont`/settings rendering refactor, and a 238-line ScreenRecorder reflection rewrite. The reflection delegates bind and the standalone application compiles, but there is no supplied reproduction showing that this fixes the fresh-install menu crash in [issue #50](https://github.com/ImAxel0/Openthesia/issues/50). The "optional" recorder is also still a package reference in the project; the reflection layer mainly changes behavior if the deployed DLL is missing.

More seriously for this fork:

- merging the runtime change leaves the test project at .NET 6, so solution restore fails with `NU1201`;
- restore reports several moderate/high advisories for the newly pinned `SixLabors.ImageSharp 1.0.4`, including [GHSA-2cmq-823j-5qj8](https://github.com/advisories/GHSA-2cmq-823j-5qj8), [GHSA-63p8-c4ww-9cg7](https://github.com/advisories/GHSA-63p8-c4ww-9cg7), and [GHSA-65x7-c272-7g7r](https://github.com/advisories/GHSA-65x7-c272-7g7r); and
- broad exception swallowing in recursive search hides every failure without identifying skipped directories.

Port recursive discovery with an injectable enumerator and observable skipped-path errors; port the note-index guard with a regression test; keep runtime/package modernization and screen recording as separate proposals.

## PR #8 — physical playback controls

**Verdict: preserve the idea, skip the code.**

The feature commits [route ControlChange events](https://github.com/ImAxel0/Openthesia/commit/f870c88bef69e2de518b631f767965d210b69777) and [persist five mappings](https://github.com/ImAxel0/Openthesia/commit/b1c98bd8cdca0961b590c483da497fcde9f99629). On its historical tree, the code compiles and can invoke play/pause, stop, record, and repeated forward/backward movement.

It is not safe to import:

- `ControlNumberValues` is initialized only when an existing settings file is loaded; a true first run can reach settings with the array still null and crash when drawing the mapping buttons.
- It treats only value 127 as press and 0 as release. Devices using other momentary values, toggles, transport-specific messages, or MMC will not behave reliably.
- It uses one global scrolling cancellation source and untestable static routing.
- It contains unrelated UI scaling, shutdown, README, and version changes.
- GitHub reports the PR as conflicting, its source fork is archived, and the current fork produces six conflicts because the old flat file layout no longer exists.

If hardware transport control is desired, start a current-code design: capture raw incoming messages, let the user define press/release semantics, persist validated bindings, and drive a small playback-command interface that can be tested without a physical controller.

## Suggested adoption order

1. Import #61 through a local issue/branch after reproducing issue #5.
2. Import #62 through a separate local issue/branch and manually verify wheel behavior.
3. Rebuild #60 around complete ASIO initialization and failure-injection tests.
4. Fix the fork's solution-wide x64 default using #58 as evidence, not as the whole solution.
5. Take #57 only if touchscreen/high-resolution behavior is relevant.
6. Revisit #63 after its settings migration and rendering-cost decisions.
7. Salvage the two small #59 fixes separately; reject the bundle.
8. Reimplement #8 only in response to a concrete hardware-control need.
