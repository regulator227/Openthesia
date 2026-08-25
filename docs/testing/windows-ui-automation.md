# Windows UI Automation verification

Use this matrix for release validation of the Windows provider introduced by issue #25. It supplements the visual and keyboard checks delivered by issue #15 in [Windows Accessibility Baseline verification](accessibility-baseline-windows.md); it does not turn either checklist into a formal conformance claim.

## Tools and setup

- Build and run the x64 Windows application.
- Use Accessibility Insights for Windows or Inspect to examine the UI Automation tree, properties, patterns, bounding rectangles, and events.
- Run Narrator for the speech and focus pass.
- Provide at least one valid MIDI Source that opens a Chart with left- and right-hand assignments, and retain a completed Practice Result for the progress pass.

## Pointer-free journey

Complete this route with UI Automation actions and keyboard focus, without a pointer:

1. From Home, invoke Song and Chart Selection.
2. Find or invoke a MIDI Source and confirm Practice setup identifies the selected Chart.
3. Select Practice Mode, Required Hands, Accompaniment, tempo, count-in, metronome, loop count-in, and Practice range; then invoke Start Practice.
4. Invoke Play, Pause, Stop, screen recording, playback position, View options, note direction, note labels, label content, top-bar visibility, full screen, fall speed, glow, hand colors, SoundFont selection or plugin instrument/effect controls, sustain, and Loops and bookmarks.
5. In Practice tools, change the active setup and timing controls; create, name, save, enable, visit, edit, and delete a loop and a bookmark; close the tools.
6. Confirm Practice Status communicates playback state, target pitch and octave, Required Hands, feedback, navigation, completion, and result/progress when those values exist; return to the browser.
7. Open Device Settings and operate MIDI input/output, Lighted Keyboard Guidance and channel, Visual effects, and Theme; return Home.

For every element, record its stable AutomationId, appropriate ControlType, concise Name, HelpText when needed, current value/state, enabled and focus state, bounding rectangle, and supported Invoke, Toggle, Value, RangeValue, Selection, or SelectionItem pattern. Names must remain meaningful with icon-font glyphs unavailable.

## Focus, events, and live status

- Tab and Shift+Tab follow the visual reading order. Setting UIA focus produces a visible focus cue and an automation focus-changed event.
- Invoking a popup or Practice tools moves focus into it. Escape or Close returns focus to the opener. Scrolling an offscreen element into view does not change its AutomationId or runtime identity.
- Resizing, reflowing, moving between monitors, and changing DPI or text size update bounding rectangles without replacing semantic elements.
- State and value changes raise the corresponding property events; completed commands raise Invoke events, while SelectionItem events are raised only after the newly selected state is present in the published tree; screen or popup membership changes raise a structure event.
- Narrator identifies each focused control, its state/value, and the available action. Practice Status announces important discrete changes, does not speak every rendered frame, and announces the latest coalesced value after rapid target changes.

## Display and contrast matrix

Run the complete journey at Windows display scaling of 100%, 150%, and 200%, each with Windows text size at 225%. At every scale, repeat the focus and bounding-rectangle pass in all four built-in Windows contrast themes.

Confirm controls remain reachable after reflow or scrolling, text and semantic names remain complete, focus does not disappear when content moves, hit testing returns the deepest element at the reported screen bounds, and Narrator speech is unchanged by visual theme. Also move the running window between monitors with different DPI and repeat popup open/close focus restoration.

As of 2026-08-25, no completed record of this full manual matrix is attached. It remains a release-validation gate; the native smoke below does not satisfy it.

## Automated coverage

`AccessibilityTreeTests` cover stable semantic identity across immediate-mode frames, queued domain actions and focus, property/focus/structure events, selection-event ordering, and throttled live-region updates. `WindowsUiAutomationProviderTests` cover UIA roles, names, values, stable runtime IDs, fragment navigation and bounds, focus lookup, control patterns, queued actions, standard invoked/selection events, and single-selection behavior. `DisposableDeviceCatalogTests` cover duplicate MIDI-device identity, temporary-handle disposal, and hot-plug-safe selection from one enumeration snapshot. These tests are necessary regression coverage, but they do not replace the manual Inspect, Narrator, display-scaling, text-size, reflow, and contrast-theme matrix above.

## Native smoke record

On 2026-08-25, the built x64 application was launched in a native Windows desktop session and queried through `System.Windows.Automation`. The client resolved the root as Name `Openthesia` and AutomationId `application`, traversed the named Home commands, invoked Song and Chart Selection and Back without a pointer, invoked Device Settings, traversed its named device and accessibility controls and selection items, and set keyboard focus to `device-settings.back`; the provider reported that focus on the following frame. The process remained alive through the exercise and was stopped afterward.

After the composite-control and event review fixes, the native client also expanded the `Visual effects` combo box, confirmed its `Reduce` option became visible and focusable, selected it through `SelectionItem`, observed the combo return to `Collapsed`, and confirmed focus was restored to `device-settings.visual-effects`. The process remained alive while bounding-rectangle, state, and value changes were published.

This smoke proves that the native `WM_GETOBJECT` bridge is reachable from a real UIA client and that queued actions cross the render boundary. It is not a substitute for the complete MIDI Source/Chart Practice journey, Narrator speech review, event inspection, or the display/text/contrast matrix above.
