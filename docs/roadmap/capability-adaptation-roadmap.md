# Capability-adaptation roadmap

Status: approved implementation sequence as of 2026-08-25. This roadmap resolves [issue #16](https://github.com/regulator227/Openthesia/issues/16) within the [Synthesia-inspired piano-learning roadmap](https://github.com/regulator227/Openthesia/issues/10).

Openthesia selects **Capability Adaptations** for beginner-to-intermediate self-directed Learners; it does not pursue feature parity. The sequence favors the shortest path to a reliable, understandable Practice journey while respecting the product boundaries in `CONTEXT.md` and the decisions in `docs/adr/`.

## Sequence

```text
Phase 0: Delivered foundation
    |
    v
Phase 1: Practice-ready setup
    |
    v
Phase 2: Find and resume practice
    |
    v
Phase 3: Read and author guidance
    |
    v
Phase 4: Read generated notation
    |
    v
Phase 5: Bring authored notation into Practice
```

Phase numbers express release order. A later phase may be explored earlier, but its production work must not displace the earlier learner outcome or bypass a hard dependency. Each prospective phase is an independently releasable vertical slice rather than an architecture-only milestone.

The evidence baseline is the [Openthesia feature inventory](../research/openthesia-feature-inventory.md), the [Synthesia capability inventory](../research/synthesia-piano-feature-inventory.md), and the resolved issues and ADRs linked below. Relative effort describes the approved MVP only; it is not a calendar estimate.

## Release rules

Every prospective phase must meet the following definition of delivered:

- The learner outcome works through the native ImGui application; a throwaway prototype does not count as delivery.
- Domain behavior is deterministic and covered through public, in-process seams before device, file-system, renderer, or ImGui adapters are exercised.
- New durable data is versioned, migrates conservatively, writes atomically, preserves corrupt input for recovery, and never silently transfers data across Song, Chart, Learner, or Device Settings owners.
- New controls and visual states meet the Windows Accessibility Baseline in [ADR 0005](../adr/0005-windows-first-accessibility-baseline.md). Each new surface also exposes stable domain semantics suitable for the Windows UI Automation architecture tracked in [issue #25](https://github.com/regulator227/Openthesia/issues/25).
- Error and unavailable-capability paths remain usable. Optional hardware, authored notation, or learning metadata may improve a journey but may not make its baseline path unusable.
- The full Release/x64 tests and application build pass. The changed journey also passes its documented Windows, input-device, display-scaling, keyboard, and effects/contrast manual matrix.

Data safety, migrations, error recovery, accessibility, and verification are release criteria. They cannot be moved into a later extension to make a phase appear complete.

### Deferral categories

Every non-MVP item must use one of these categories instead of disappearing from scope:

- **Phase extension**: useful after the MVP proves its learner outcome. The roadmap names the evidence that would justify reconsidering it.
- **Conditional capability**: blocked until a factual, architectural, algorithmic, or hardware prerequisite is validated.
- **Product exclusion**: conflicts with an established boundary. Reconsidering it requires an explicit product decision and, when the trade-off is hard to reverse, a replacement or superseding ADR.

## Phase 0 — Delivered foundation

**Learner outcome:** A Learner can identify a Chart durably, conduct a comparable Practice Session, receive understandable feedback, revisit a range, and use the journey with the Windows-first visual and keyboard baseline.

**Status:** Present on `master`; do not reimplement it as prospective roadmap work.

Delivered foundations are:

- Stable generated Song identity, pattern-addressed Charts, device-owned MIDI Sources, Chart-owned Hand Assignments, durable Learners, and active-Learner selection from [issue #11](https://github.com/regulator227/Openthesia/issues/11) and [ADR 0001](../adr/0001-song-and-chart-identity.md).
- A deterministic Chart-centered Practice Session with Wait for Notes, Play in Time, and Recital from [issue #17](https://github.com/regulator227/Openthesia/issues/17) and [ADR 0003](../adr/0003-practice-session-and-mode-model.md).
- Dimensional Practice Results and Comparable-Practice-Setup-specific Practice Progress from [issue #21](https://github.com/regulator227/Openthesia/issues/21) and [ADR 0004](../adr/0004-dimensional-practice-results-and-progress.md).
- Personal loops and bookmarks plus count-in, metronome, and restart behavior from [issue #20](https://github.com/regulator227/Openthesia/issues/20).
- The Windows Accessibility Baseline from [issue #15](https://github.com/regulator227/Openthesia/issues/15) and [ADR 0005](../adr/0005-windows-first-accessibility-baseline.md).
- Standard-MIDI Lighted Keyboard Guidance from [issue #18](https://github.com/regulator227/Openthesia/issues/18) and [ADR 0006](../adr/0006-standard-midi-lighted-keyboard-guidance.md).

Phase 0 is the architectural prerequisite for every later phase. Its seams and ownership boundaries should be deepened when needed, not bypassed with a second identity, session, progress, navigation, accessibility, or device-settings model.

## Phase 1 — Practice-ready setup

**Learner outcome:** A first-time or returning Learner can choose how they will interact with Openthesia, prove that the selected path works, and begin Practice with an explicit keyboard range and timing-calibration state.

**Evidence-backed gap:** Device selection is currently a collection of direct Settings controls. Practice assessment starts uncalibrated, and the product does not guide the Learner through input/output testing, range capture, or the difference between assessment timing and audio-driver latency. The selected direction is Variant A from [issue #19](https://github.com/regulator227/Openthesia/issues/19).

**Architectural fit and prerequisites:** This phase builds on Device Settings, Active Learner selection, Practice Session startup, the timing-calibration revision already carried by Practice Results, and the standard-MIDI lighting boundary. It must not place device configuration in Learner Chart Data or make a hardware keyboard mandatory.

**Relative effort:** Medium-to-large. The calibration policy is bounded, but the phase crosses device lifecycle, persistence, native UI, and manual hardware verification.

### MVP

- Use the same five-step flow for first run and **Settings -> MIDI setup**: choose capability, connect and test, capture range, align practice timing, then review.
- Offer **MIDI keyboard**, **computer keys + on-screen piano**, and **listen and explore** before showing device controls. No-keyboard paths remove irrelevant steps rather than reporting missing hardware.
- Select and test MIDI input separately from sound or MIDI output. Confirm incoming notes visibly and audibly where sound is enabled.
- Capture the lowest and highest reachable keys, with known-size presets as a fallback and validation against reversed or implausible endpoints. Warn when a selected Chart requires notes outside that range without silently transposing or changing the Chart.
- Calibrate assessment timing from four note-onset pulses. Explain that it is separate from audio-buffer latency, allow a skip, and mark resulting Timing as uncalibrated.
- Stage all reconfiguration until review and apply. Cancel leaves active Device Settings untouched; applying a changed timing setup advances its calibration revision so unlike Timing results are not silently compared.
- Keep the compact C4-C5 computer-key range with octave shifting and preserve a complete listen-only route that does not create assessed Practice Results.
- Test the existing standard-MIDI output and optional Lighted Keyboard Guidance within its declared channel/message boundary without promising model compatibility.
- Persist the setup through versioned Device Settings with conservative migration from current settings.

### Explicit deferrals

- **Phase extension:** richer troubleshooting and device-specific help may follow when setup failure data shows where Learners stop.
- **Conditional capability:** automatic assessment- or audio-latency measurement requires a repeatable algorithm and device matrix that improves on the explicit four-pulse calibration.
- **Conditional capability:** multiple simultaneous input instruments requires a Practice-input ownership and event-merging design plus demonstrated learner demand.
- **Conditional capability:** automatic accommodation or transposition for notes outside the captured keyboard range requires a separate musical-behavior decision and validation across Practice Modes.
- **Product exclusion:** vendor SysEx, model profiles, colored/flashing lights, and blanket lighted-keyboard compatibility remain outside [ADR 0006](../adr/0006-standard-midi-lighted-keyboard-guidance.md).

### Phase exit

A fresh installation and a reconfiguration must each complete, cancel, and recover safely through all three capability paths. At least one physical MIDI input/output path and the hardware-free paths must be manually exercised. Practice Results must demonstrate calibrated, skipped, changed-revision, and corrupt-settings behavior without cross-revision Personal Best comparison.

## Phase 2 — Find and resume practice

**Learner outcome:** A Learner can find the intended Song and Chart, understand its local source health and personal Practice Progress, and enter an exact Practice Session without navigating the file system as the product model.

**Evidence-backed gap:** The current browser scans configured folders and presents MIDI filenames. The Song/Chart catalog and Practice Progress already exist, but there is no production Song Library that exposes those distinctions or helps with renamed, moved, duplicate, recent, or missing sources. The selected direction is the Catalog Workbench from [issue #13](https://github.com/regulator227/Openthesia/issues/13).

**Architectural fit and prerequisites:** This phase consumes the existing SongCatalog, MIDI Source mappings, Active Learner, and Learner + Chart Practice Progress. Search and filters must query the catalog rather than create a second filename index. Shared Song Metadata, Chart Metadata, personal Learner Song Data, Learner Chart Data, and device-owned source state remain visibly and durably separate under [ADR 0007](../adr/0007-song-library-metadata-ownership.md).

**Relative effort:** Large. The core query model is straightforward, but the native responsive surface, metadata persistence, source repair, focus behavior, and migration paths are broad.

### MVP

- Present distinct Song rows with their Chart arrangements; selecting a Chart targets that exact Chart for Practice.
- Search by Song, Chart, and source-facing text and preserve filters for group, tag, Chart difficulty, and source health.
- Show Chart-specific recent activity, first completion, Personal Bests, and recent trends without rolling multiple Charts into a Song-wide score.
- Provide recent and missing-source smart views.
- Use a details surface that keeps Song Metadata, Chart Metadata, Learner Song Data, Learner Chart Data, and device-owned MIDI Sources visibly separate.
- Store descriptive tags as Song Metadata, arrangement difficulty as Chart Metadata, and groups and favorites as Learner Song Data. Support the minimal editing needed to populate those filters without transferring values among owners.
- Retain configured-folder refresh and direct opening of user-owned MIDI files. Allow a missing source to be located and relinked only after its normalized pattern is checked against the expected Chart.
- Preserve current direct-file access as a fallback when catalog persistence is unavailable or damaged.
- Meet keyboard navigation, focus restoration, scaling, contrast, and stable semantic-identity requirements across the dense catalog and details surface.

### Explicit deferrals

- **Phase extension:** nested groups and bulk metadata editing become candidates when real libraries make one-at-a-time organization materially burdensome.
- **Phase extension:** previews, personal ratings, and advanced search syntax require evidence that basic search, filters, and Progress do not support selection adequately.
- **Phase extension:** real-time file-system monitoring may replace explicit/configured-folder refresh when stale source state is a demonstrated recurring problem.
- **Phase extension:** full Learner-profile management remains separate; the Library uses the Active Learner established by Phase 0.
- **Product exclusion:** an online song store, redistribution of commercial music, cloud synchronization, and social/competitive discovery remain outside this roadmap.

### Phase exit

The native Library must remain responsive and keyboard-operable across an agreed large local fixture. Tests must cover exact Chart targeting, duplicate Sources, rename/move/relink, pattern-changing replacements, missing and corrupt Sources, per-Learner Progress isolation, filter persistence, and corrupt catalog/metadata recovery. The old direct-file journey remains usable during migration and recovery.

## Phase 3 — Read and author guidance

**Learner outcome:** A Learner can choose a compact, understandable guidance preset, read notes and keys using an appropriate label system, and use shared or personal fingering hints without affecting Performance Visualization.

**Evidence-backed gap:** Openthesia currently offers only a small renderer-level note-label choice. It does not have the preset-first Practice guidance, ownership-aware fingering, key labels, or Learner + Chart persistence selected in [issue #12](https://github.com/regulator227/Openthesia/issues/12).

**Architectural fit and prerequisites:** Guidance is a Practice presentation concern layered over the existing Practice Session, Chart pattern, Hand Assignments, Active Learner, and Accessibility Baseline. Shared finger hints extend Chart Metadata; personal overrides extend Learner Chart Data. Performance Visualization consumes neither.

**Relative effort:** Medium-to-large. Label rendering is bounded, while accessible preset/custom controls, annotation authoring, persistence, and collision-free layout carry most of the risk.

### MVP

- Add a compact **Guidance** control with **Clean**, **Read notes** as the default, **Learn fingering**, and **Custom** presets.
- Force labels, key labels, finger hints, and guidance controls off in Performance Visualization while remembering the Learner's Practice choices.
- Support falling-note labels for Off, English note names, scientific pitch, and fixed-Do solfege.
- Support piano-key labels for Off, octave-C markers, simplified A-G, fixed-Do solfege, and the typing-key mapping.
- Default labels to the Learner's assigned notes, with Required-Hands-only and both-hands scopes available.
- Render finger hints as 1 for thumb through 5 for pinky. Store recommended hints as Chart Metadata and personal overrides as Learner Chart Data, with personal values taking precedence only for that Learner.
- Provide a bounded passage action for authoring shared hints and personal overrides; do not mix annotation ownership into the Guidance display control.
- Persist the selected preset and custom layers for Learner + Chart with versioned, corruption-safe data.
- Keep Practice Status and non-color target/hand/feedback cues independent of optional guidance.

### Explicit deferrals

- **Phase extension:** scale-degree and movable-Do labels follow once a trusted tonal context is available from a compatible Score Interpretation; they must not guess a key from pitch frequency alone.
- **Phase extension:** importing finger hints from an external metadata format requires a separately declared mapping and conflict policy.
- **Conditional capability:** automatic fingering generation requires an explainable, independently validated model with explicit keyboard-range and hand constraints.
- **Product exclusion:** guidance in Performance Visualization would contradict the product-mode boundary and requires a new product decision.

### Phase exit

Automated coverage must prove preset/custom mapping, owner precedence, per-Learner isolation, corrupt-data fallback, Practice/Performance Visualization isolation, and label availability rules. Manual coverage must verify dense chords, short notes, both directions, all Required Hands settings, 225% text size, contrast themes, keyboard-only authoring, and collision behavior at the supported window bounds.

## Phase 4 — Read generated notation

**Learner outcome:** A Learner who reads staff notation can view the current Chart as a synchronized Practice Score without leaving Practice or changing the music being assessed.

**Evidence-backed gap:** Openthesia has a piano roll but no staff-notation view. [Issue #14](https://github.com/regulator227/Openthesia/issues/14) and [ADR 0002](../adr/0002-practice-score-product-boundary.md) select a read-only, Chart-centered Practice Score rather than a notation editor.

**Architectural fit and prerequisites:** The generated Score Interpretation consumes the immutable normalized Chart pattern and synchronizes through Practice Session time. Display-only quantization cannot change Chart identity, playback, Hand Assignments, or Practice Results. View and interpretation selection belong to Learner + Chart.

**Relative effort:** Large with high rendering uncertainty. Music semantics, readable layout, confidence reporting, input navigation, and accessibility must be isolated from the existing piano-roll renderer.

### MVP

- Generate a visibly identified, deterministic Score Interpretation from an existing Chart.
- Offer Piano Roll, Split, and Practice Score views, persisted per Learner + Chart.
- Render essential grand-staff notes and chords, common durations, rests, dots, ties, accidentals, barlines, clefs, and available time signatures in a continuous horizontal layout.
- Honor valid source time and key signatures, default missing time to 4/4, and do not guess a missing key signature.
- Synchronize highlighting with the Practice Session and provide input-safe click-to-seek plus an equivalent keyboard-operable seek action.
- Separate staff placement from Hand Assignment while retaining practice colors and Required Hands semantics through independent overlays.
- Make display-only quantization deterministic and visible. Warn on low confidence and fall back to Piano Roll when the interpretation is unusable.
- Keep the notation engine, layout result, and Practice synchronization behind testable seams independent of ImGui drawing.

### Explicit deferrals

- **Phase extension:** vertical or paged layout, printing-oriented pagination, and richer engraving require evidence that the continuous Practice layout does not serve learning tasks.
- **Phase extension:** guidance, loops, bookmarks, metronome markers, and detailed feedback overlays may be added after the base renderer remains readable across the verification corpus.
- **Next phase:** authored MusicXML input belongs to Phase 5 and cannot be used to make generated notation appear complete.
- **Product exclusion:** notation editing, publication-quality engraving, printing/export, and score authoring remain outside [ADR 0002](../adr/0002-practice-score-product-boundary.md).

### Phase exit

A versioned notation corpus must cover supported and deliberately unsupported rhythmic, key, time, polyphonic, and malformed patterns. Golden structural tests should validate interpretation and layout without relying only on screenshots. Manual review must cover synchronized navigation, fallback, scaling, keyboard operation, contrast, reduced effects, dense passages, and Piano Roll/Split/Practice Score switching without altering the Practice Session.

## Phase 5 — Bring authored notation into Practice

**Learner outcome:** A Learner can open compatible local MusicXML, retain its useful authored notation, and practice the resulting Chart without Openthesia claiming to be an editor or lossless publishing system.

**Evidence-backed gap:** Synthesia accepts MusicXML, while Openthesia accepts MIDI only. The generated renderer and Score Interpretation ownership selected by [ADR 0002](../adr/0002-practice-score-product-boundary.md) are prerequisites for preserving authored notation safely.

**Architectural fit and prerequisites:** MusicXML import creates or matches the normalized playable Chart pattern first, then attaches a compatible authored Score Interpretation without changing Chart identity. The local file remains a device-owned MusicXML Source. Exact pattern matches may reuse an existing Chart; an unmatched new source follows the Song/Chart creation rules in [ADR 0001](../adr/0001-song-and-chart-identity.md). Imports never silently replace the Learner's established interpretation selection.

**Relative effort:** Large with high data-correctness risk. Parsing is not the primary risk; trustworthy normalization, diagnostics, provenance, safe archive handling, and compatibility with generated interpretations are.

### MVP

- Open local `.musicxml` and compressed `.mxl` files transactionally.
- Support a versioned, learner-visible practice subset covering parts, staves, voices, measures, pitch and rhythm, rests, chords, ties, tuplets, clefs, key and time signatures, and accidentals.
- Reject the import without catalog mutation when pitch or timing cannot be trusted.
- Preserve compatible authored semantics as a Chart-owned Score Interpretation and report omitted optional presentation details visibly.
- Reuse an exact existing Chart pattern safely; otherwise create the Song and first Chart according to the existing identity rules rather than guessing a relationship from filenames or descriptive metadata. If a known MusicXML Source changes pattern, create a new Chart under the same Song without inheriting annotations or Practice Progress.
- Prefer compatible authored notation by default only when the Learner has no established interpretation choice. Never silently replace an existing selection.
- Constrain `.mxl` archive expansion, file types, sizes, and paths before parsing.
- Fall back to a generated Score Interpretation or Piano Roll when authored notation is unsupported but the playable Chart remains trustworthy.

### Explicit deferrals

- **Phase extension:** additional optional MusicXML presentation elements are admitted only by advancing the declared subset version with fixtures and visible compatibility behavior.
- **Phase extension:** alternate interpretations and manual selection management may deepen after the default/preference rules prove understandable.
- **Conditional capability:** automatic reconciliation between non-identical MusicXML and MIDI patterns requires a trustworthy mapping model that cannot transfer annotations or Practice Progress incorrectly.
- **Product exclusion:** notation editing, MusicXML export, lossless round-tripping, publication engraving, and silent best-effort recovery of untrustworthy pitch/timing remain outside [ADR 0002](../adr/0002-practice-score-product-boundary.md).

### Phase exit

The import corpus must include plain and compressed files, exact duplicate patterns, new patterns, multiple interpretations, every supported subset construct, unsupported optional details, corrupt XML, archive attacks, and untrustworthy pitch/timing. Tests must prove transactional catalog behavior and stable Chart identity. Manual verification must show diagnostics, preference preservation, fallback, synchronized Practice, accessibility, and safe reopen after source relocation or change.

## Parallel architecture stream — Windows UI Automation

[Issue #25](https://github.com/regulator227/Openthesia/issues/25) is a parallel Windows architecture stream, not a numbered Capability Adaptation phase. It may proceed independently and must not turn the already-delivered visual and keyboard baseline into an overstated Narrator claim.

Every numbered phase must expose stable domain-level roles, names, values, states, actions, focus relationships, and change descriptions for its new journey. The UIA provider can then consume those semantics without deriving meaning from glyphs or transient ImGui geometry. If a phase cannot describe its controls and live state stably, that is a phase defect even when the provider itself is not yet complete.

## Capabilities outside the sequence

The roadmap deliberately does not schedule cloud accounts or synchronization, online leaderboards, telemetry, social competition, an online song store, redistribution of commercial content, a DAW or six-track overdubbing workflow, cross-platform expansion, general UI localization, vendor-specific lighting, or notation authoring/export.

Localization remains a possible later architecture decision rather than a hidden Phase 1-5 commitment. Moving any product exclusion into the sequence requires evidence of learner value, a declared owner and persistence boundary, an architectural prerequisite analysis, an accessible MVP, and an explicit change to this roadmap.

## Ticketing and review cadence

Before production work begins on a phase, split its MVP into reviewable issues along domain, persistence/adapter, native journey, and verification seams. The phase issue remains the learner-outcome owner; component issues cannot redefine its boundary independently.

At each phase exit:

1. Record the implemented commit or pull request and verification evidence.
2. Classify unfinished work using the three deferral categories and its reconsideration trigger.
3. Recheck the next phase against the actual architecture and learner evidence.
4. Change sequence only through an explicit roadmap decision; do not silently promote an attractive extension ahead of an unmet earlier outcome.
