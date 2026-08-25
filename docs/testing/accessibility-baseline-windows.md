# Windows Accessibility Baseline verification

Use this matrix for release validation of the learner-facing Practice journey. The automated policy tests cover the scale combinations and platform decisions; this checklist verifies actual Windows and Dear ImGui rendering.

## Journey

For each environment below, complete this route without a pointer:

1. Home → Play MIDI File → choose a Song and Chart.
2. Configure Practice mode, required hands, accompaniment, tempo, count-in, and range.
3. Start Practice; use Play, Pause, Stop, View options, and Loops & bookmarks.
4. Create, enable, visit, rename, and remove a loop and bookmark.
5. Read current target, feedback, completion/result, and return to the browser.
6. Open Device Settings and switch Visual effects among System, Reduce, and Full.

Confirm Tab and Shift+Tab follow visual order, arrows operate composites, Enter and Space activate the focused control, Escape dismisses or returns, and focus remains visibly outlined. Confirm computer-piano input does not consume focused-control or navigation input.

## Scale matrix

Run the journey at Windows display scaling of 100%, 150%, and 200%, each with text size at 225%. At every combination:

- all non-spatial controls remain reachable through reflow, vertical scrolling, or labelled horizontal overflow;
- text is not clipped and controls do not overlap;
- pointer targets remain at least 24 device-independent pixels;
- moving between monitors updates scale without restarting;
- the piano roll remains spatial while Practice Status names the current pitch, octave, hand, and state.

## Visual matrix

- In each of the four built-in Windows contrast themes, text, control boundaries, necessary graphics, and focus cues use the system palette.
- Hand, target, judgment, selection, and control state remain understandable without color.
- The `Pitch + octave` note-label mode renders compact labels such as `C4` and `F#4` with a contrast-safe backdrop.
- System follows Windows animation and advanced-effects settings.
- Reduce removes the matrix background, grid transparency, pulse, glow, velocity opacity, easing/inertial panning, and other transparency-dependent cues.
- Full never overrides a Windows contrast theme.
- Falling notes retain static current-target/status text and keyboard-operable Pause and Stop.
- No effect flashes more than three times per second.

## Automated coverage

The public seams `AccessibilitySettingsStore.Load/Save`, `AccessibilityPolicy.Resolve`, `PracticeAccessibility.Describe`, and `PracticeCommandMap.TryMap` cover persistence and safe fallback, the 100/150/200% × 225% scale policy, effects and contrast precedence, non-color Practice descriptions, and shortcut ownership. `ImGuiFontAtlasTests` rasterizes the actual atlas at the known-safe 200% display scale and the maximum supported combined scale to guard against texture exhaustion.

During issue #15 verification on 2026-08-25, the built x64 application was also launched in a hidden native Windows session and kept alive long enough to exercise DPI setup, font-atlas creation, and graphics initialization. Repeat that manual smoke check whenever graphics, fonts, or DPI initialization changes.
