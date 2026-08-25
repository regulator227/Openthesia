# Bridge Practice semantics to Windows UI Automation

Openthesia keeps an adapter-neutral accessibility tree between the learner-facing domain and Dear ImGui, then projects the active frame through a Windows server-side UI Automation provider. This preserves domain names, state, actions, and stable identity without deriving meaning from glyphs or coupling Practice code to Windows COM interfaces; replacing the renderer or adding another platform adapter does not require redefining Practice semantics.

## Boundary

The semantic layer owns immutable snapshots of the active learner journey: application and screen roots, MIDI Source selection, Chart setup, Practice playback and configuration, visual guidance, loops, bookmarks, results, and accessibility-related Device Settings. Nodes use stable semantic keys. Dynamic entries add durable domain or source identity to that key, while runtime IDs remain stable when bounds, values, focus, scrolling, DPI, or text scale change between immediate-mode frames.

Dear ImGui remains responsible for drawing, layout, and keyboard focus. Its accessibility adapter captures each rendered control's client bounds and focused, enabled, selected, expanded, and offscreen state. UI Automation actions are queued and dispatched to the same application commands used by visible controls at the start of the next frame; UI Automation does not mutate Practice state directly.

The Windows adapter attaches to the native window, returns an `IRawElementProviderFragmentRoot` for `WM_GETOBJECT`, maps semantic roles to UIA control types and patterns, converts bounds to screen coordinates, and raises structure, property, focus, live-region, invoked, and selection events. The persistent `Practice Status` names mode and playback state plus available pitch, octave, Required Hands, target, feedback, navigation, completion, and result information. Polite changes are coalesced to at most one announcement per 750 ms and keep the latest value; completion uses an assertive update.

## Consequences

The application targets Windows UI Automation APIs in its Windows build and provider objects must be safe for calls outside the render thread. Automated tests exercise the adapter-neutral tree and the provider's stable runtime IDs, roles, names, values, patterns, queued actions, navigation, focus, and change events.

This decision adds a platform bridge; it does not replace or retroactively broaden the visual and keyboard Accessibility Baseline in [ADR 0005](0005-windows-first-accessibility-baseline.md). Passing provider tests is not a formal WCAG, legal-compliance, Narrator-conformance, or accessibility-certification claim. Narrator and Inspect behavior, focus restoration, scrolling, reflow, Windows text size, DPI, and contrast themes remain release-tested with the [Windows UI Automation verification matrix](../testing/windows-ui-automation.md).
