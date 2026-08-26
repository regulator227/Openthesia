# Openthesia feature inventory

Inventory date: 2026-08-22. Source baseline: working tree at `e7b0edf` on `codex/fix-main-menu-hover-flicker`; `origin/master` is `5fe307e`. This describes capabilities present in the local fork, not Synthesia and not unmerged upstream proposals. It is an inventory only, with no porting or priority recommendations.

## Product shape and primary workflows

Openthesia is a local, single-user Windows desktop application for MIDI piano-roll visualization, play-along practice, live performance visualization, MIDI recording, and window video capture. The README names two main modes—MIDI playback and Play Mode—and lists customization, learning mode, hand separation, SoundFonts, video capture, and VST2 support (`README.md:7-32`). The application registers six full-screen ImGui views: Home, MIDI Browser, Mode Selection, MIDI Playback, Play Mode, and Settings (`Openthesia/Core/Application.cs:23-37`; `Openthesia/Enums/Windows.cs:4-12`).

The user-visible flow is:

1. **Home:** choose Play MIDI File, Play Mode, Settings, or Exit (`Openthesia/Ui/Windows/HomeWindow.cs:87-118`).
2. **MIDI file discovery:** search configured folders, reverse alphabetical order, select a listed `.mid`, or open a specific file via a native file dialog (`Openthesia/Ui/Windows/MidiBrowserWindow.cs:22-35`, `:66-89`, `:117-130`). Folder scanning is top-level only (`Directory.GetFiles(path, "*.mid")`), not recursive (`Openthesia/Ui/Windows/MidiBrowserWindow.cs:66-71`).
3. **Playback mode choice:** View and listen, Play along, or Edit mode (`Openthesia/Ui/Windows/ModeSelectionWindow.cs:33-42`, `:104-135`).
4. **Performance view:** Play Mode skips the file-selection flow and visualizes live input; it can record MIDI, save it, or open the last capture in the playback view (`Openthesia/Ui/ScreenCanvas.cs:1123-1173`, `:1188-1205`).

## Implemented feature areas

### MIDI playback and visualization

- Reads `.mid` files with DryWetMIDI, extracts the tempo map and note collection, creates a tracked playback, routes played events through the shared input handler, and tracks current time (`Openthesia/Core/Midi/MidiFileHandler.cs:12-49`; `Openthesia/Core/Midi/MidiPlayer.cs:19-29`).
- Renders a 52-white-key/88-key-range piano plus a vertically aligned note grid; pressed keys support mouse interaction and visual feedback (`Openthesia/Ui/PianoRenderer.cs:25-58`, `:60-112`, `:114-163`; `Openthesia/Ui/ScreenCanvas.cs:44-60`).
- Draws falling or rising note blocks with per-hand colors, optional glow, configurable corner rounding, and optional velocity-derived opacity (`Openthesia/Ui/ScreenCanvas.cs:206-229`, `:398-496`; `Openthesia/Settings/CoreSettings.cs:14-30`).
- Note labels can show note name, velocity, or octave (`Openthesia/Enums/TextTypes.cs:3-8`; `Openthesia/Ui/ScreenCanvas.cs:434-495`, `:892-929`).
- Playback controls include play, pause, stop/reset, a seek bar, and video record (`Openthesia/Ui/ScreenCanvas.cs:688-730`, `:732-799`).
- Playback speed is selectable from 0.25x to 4x in 0.25x steps. Bare mouse-wheel adjusts speed; Ctrl+wheel seeks by 0.5 seconds per wheel unit. Arrow keys seek by one second or 0.1 seconds with Ctrl; right/middle-button vertical panning also seeks (`Openthesia/Ui/ScreenCanvas.cs:506-587`, `:873-889`).
- Visual fall speed has four presets independent of MIDI playback speed (`Openthesia/Enums/FallSpeeds.cs:3-9`; `Openthesia/Core/ScreenCanvasControls.cs:54-73`). Note direction can be toggled outside learning/edit mode (`Openthesia/Ui/ScreenCanvas.cs:801-817`).
- The top controls can auto-hide or stay locked, and playback supports borderless fullscreen from the UI or F11 (`Openthesia/Ui/ScreenCanvas.cs:659-684`, `:832-851`; `Openthesia/Program.cs:68-74`).

### Practice, hand separation, and editing

- **Play along / learning mode** stops playback when an enabled note reaches the keyboard and its pitch is not currently held, then resumes once no required note is missing (`Openthesia/Ui/ScreenCanvas.cs:253-276`, `:499-503`). Playback-generated key events are suppressed in learning mode so the learner's input is the gate (`Openthesia/Core/IOHandle.cs:140-175`).
- Left- and right-hand enable buttons filter visualization and mute disabled-hand notes through the playback note callback (`Openthesia/Ui/ScreenCanvas.cs:72-90`, `:940-958`; `Openthesia/Core/Midi/NoteCallback.cs:7-25`).
- **Edit mode** assigns notes to left hand with left-click and right hand with right-click; Ctrl-drag rectangles batch-assign notes. Changes are saved automatically to per-song XML hand-data files (`Openthesia/Ui/ScreenCanvas.cs:278-383`; `Openthesia/Core/Midi/MidiEditing.cs:8-54`). This edits hand classification only; it does not edit pitch, timing, duration, tempo, or the source MIDI file.
- Sustain pedal CC64 is handled both from physical/playback events and from an on-screen pedal button. The fork tracks held-note multiplicity separately from pedal-latched notes (`Openthesia/Core/IOHandle.cs:93-104`, `:123-136`; `Openthesia/Core/Midi/SustainState.cs:3-51`; `Openthesia/Ui/ScreenCanvas.cs:1109-1120`).

### Live input and MIDI recording

- Physical MIDI input and output devices are enumerated and selectable; input events subscribe to the shared handler, while an optional output device receives emitted events (`Openthesia/Settings/DevicesManager.cs:11-45`, `:53-87`; `Openthesia/Ui/Windows/SettingsWindow.cs:64-114`).
- The on-screen piano is playable with the mouse. An optional computer-keyboard layout maps A–K plus black-key rows to C4–C5, with Z/X octave shift (up to ±36 semitones) and C/V velocity changes (`Openthesia/Ui/PianoRenderer.cs:64-81`, `:124-140`; `Openthesia/Core/VirtualKeyboard.cs:10-28`, `:30-75`). The mapping and current shift/velocity are not user-configurable or persisted.
- Play Mode draws live notes as rising blocks (`Openthesia/Ui/ScreenCanvas.cs:93-203`, `:638-655`).
- MIDI recording requires a selected input device, uses the default tempo map, and can stop, export to `.mid`, or preview the last capture in the playback view (`Openthesia/Core/Midi/MidiRecording.cs:14-49`, `:52-76`; `Openthesia/Ui/ScreenCanvas.cs:1130-1151`, `:1188-1205`).

### Audio engines, providers, and models

- The selectable sound engines are **None**, **SoundFonts**, and **Plugins** (`Openthesia/Enums/SoundEngine.cs:9-14`; `Openthesia/Ui/Windows/SettingsWindow.cs:182-207`). Changing the engine requires an application restart (`Openthesia/Ui/Windows/SettingsWindow.cs:190-199`).
- **SoundFonts:** `.sf2` files are discovered in the application `SoundFonts` directory and additional configured folders. MeltySynth renders stereo audio at 44.1 kHz with maximum polyphony 256; the active SoundFont is switchable during playback (`Openthesia/Core/SoundFonts/SoundFontPlayer.cs:26-56`, `:59-92`; `Openthesia/Ui/ScreenCanvas.cs:1012-1034`).
- **VST:** the host accepts VST2 `.dll` instruments and effects. One instrument can be active; effects form an ordered, enable/disable-able chain. Plugin editor windows can be opened, effects reordered/removed, and paths/order persisted (`Openthesia/Ui/Windows/SettingsWindow.cs:365-421`, `:423-527`; `Openthesia/Core/Plugins/PluginsChain.cs:12-55`, `:88-139`). The `IPlugin` interface is a genuine extension seam for non-VST implementations, although the current UI only loads `VstPlugin` DLLs (`Openthesia/Core/Plugins/IPlugin.cs:11-65`; `Openthesia/Core/Plugins/VstPlugin.cs:16-71`).
- VST MIDI translation covers note on/off, control change, pitch bend, channel/polyphonic aftertouch, program change, SysEx, and selected realtime/system events (`Openthesia/Core/Plugins/VstMidiHandler.cs:34-164`).
- **Audio drivers:** internal SoundFont and plugin audio use WaveOut or ASIO. WaveOut exposes a 15–300 ms latency control; ASIO exposes installed-driver selection and the driver control panel (`Openthesia/Ui/Windows/SettingsWindow.cs:209-290`). If ASIO discovery, construction, initialization, or playback fails, the fork warns the user and falls back to WaveOut for that session (`Openthesia/Settings/AudioDriverManager.cs:35-59`, `:73-118`).
- There is no provider marketplace, online content provider, generative model, or model-selection concept in the source. “Provider” support is limited to local MIDI devices, `.sf2` files, and VST2 DLLs; the package list contains UI, MIDI, audio, serialization, recording, and Windows/graphics libraries but no network or cloud client (`Openthesia/Openthesia.csproj:52-65`).

### Video and export pipeline

- Video recording captures the Openthesia application window, includes the system output device audio, writes MP4, and uses the selected 30/60/120 FPS setting (`Openthesia/Core/ScreenRecorder.cs:19-53`; `Openthesia/Ui/Windows/SettingsWindow.cs:540-569`).
- Video settings include destination folder, auto-starting MIDI playback, opening the destination folder, and auto-playing the completed recording (`Openthesia/Settings/SettingsData.cs:41-46`; `Openthesia/Core/ScreenRecorder.cs:60-71`; `Openthesia/Ui/Windows/SettingsWindow.cs:546-587`).
- Export outputs are `.mid` for a live MIDI recording and `.mp4` for window capture. No audio-only render, image export, MusicXML/sheet-music export, project bundle, or edited-MIDI export path is implemented (`Openthesia/Core/Midi/MidiRecording.cs:52-70`; `Openthesia/Core/ScreenRecorder.cs:21-53`).

### Appearance and interaction customization

- Presets are Sky, Volcano, and Synthesia, with editable background, left-hand, and right-hand colors (`Openthesia/Enums/Themes.cs:3-8`; `Openthesia/Settings/ThemeManager.cs:7-37`; `Openthesia/Ui/Windows/SettingsWindow.cs:591-633`).
- Toggles include note glow, colored pressed keys, velocity-as-opacity, animated background, FPS counter, and velocity-zero NoteOn interpretation (`Openthesia/Ui/Windows/SettingsWindow.cs:64-86`, `:595-618`).
- A known behavior encoded in the current renderer is that “colored keypresses” always uses the right-hand color, because pressed-key state records pitch but not hand provenance (`Openthesia/Ui/PianoRenderer.cs:84-88`, `:143-147`; `Openthesia/Core/IOHandle.cs:13-19`).

## Persistence and project model

- Openthesia has **settings**, not a project/document model. A single `%APPDATA%\Openthesia\Settings.json` stores device names, scan paths, plugin paths, input/visual options, theme/colors, playback-control display state, sound engine/driver, video options, and plugin-startup behavior (`Openthesia/Core/ProgramData.cs:13-22`, `:68-128`, `:136-204`; `Openthesia/Settings/SettingsData.cs:6-46`). Settings are loaded at startup and saved on application exit (`Openthesia/Program.cs:55-58`, `:106-107`).
- Per-song hand assignments are separate XML files under `%APPDATA%\Openthesia\HandsData`, keyed only by the MIDI filename with `.mid` removed (`Openthesia/Core/ProgramData.cs:15-21`; `Openthesia/Core/Midi/MidiEditing.cs:13-25`, `:34-48`). Files with the same name from different directories therefore share one hand-data key.
- The “library” is a live scan of configured local folders. Default roots are Documents, Downloads, and Music; there is no database, copy/import operation, metadata index, playlist, favorite, tag, search cache, recently played list, or content download workflow (`Openthesia/Settings/MidiPathsManager.cs:5-21`; `Openthesia/Ui/Windows/MidiBrowserWindow.cs:60-89`).
- There is no persisted lesson state, score, accuracy history, practice session, tempo per song, bookmark/loop region, user profile, or saved workspace in `SettingsData` (`Openthesia/Settings/SettingsData.cs:6-46`).
- The working tree contains an untracked `Openthesia/Songs/` directory with local MIDI files, but `git ls-files Openthesia/Songs/**` returns no tracked content. It is not counted as a shipped repository capability.

## Localization, integrations, and multi-user capabilities

### Localization

The application UI is English-only in the current source. User-facing labels are inline literals throughout the views (for example, the entire settings surface in `Openthesia/Ui/Windows/SettingsWindow.cs:64-121`, `:182-205`, `:295-365`, `:532-633`), and the project embeds fonts/images/shaders but no `.resx` or translation catalog (`Openthesia/Openthesia.csproj:22-50`). Even the VST host reports its host language as unsupported (`Openthesia/Core/Plugins/HostCommandStub.cs:109-114`). No locale selection or localization abstraction was found.

### Integrations

Implemented integrations are local/desktop:

- OS MIDI input/output through DryWetMIDI (`Openthesia/Settings/DevicesManager.cs:6-93`).
- SoundFont synthesis through MeltySynth and audio output through NAudio (`Openthesia/Core/SoundFonts/SoundFontPlayer.cs:11-21`, `:48-57`).
- VST2 hosting through VST.NET2 (`Openthesia/Core/Plugins/VstPlugin.cs:63-71`, `:94-150`).
- Windows folder/file pickers, ASIO control panels, Explorer opening after capture, and Win32 message boxes (`Openthesia/Ui/Windows/SettingsWindow.cs:243-275`, `:573-587`; `Openthesia/Core/ScreenRecorder.cs:60-76`).
- Clicking the home logo opens the public Openthesia website (`Openthesia/Ui/Windows/HomeWindow.cs:49-60`).

No application runtime HTTP client, update service, cloud storage, account/authentication, telemetry, social sharing, lesson/content service, or third-party web API was found. README links and the home-logo URL are navigation/documentation links, not data integrations.

### Collaboration and enterprise

No collaboration or enterprise layer is implemented: there are no accounts, roles, organizations/tenants, shared libraries, real-time or asynchronous collaboration, comments/review, permissions, audit logs, admin controls, deployment policy, SSO, licensing server, or cloud sync. The only durable state is local JSON/XML, and the package references contain no server/client or identity stack (`Openthesia/Core/ProgramData.cs:13-22`, `:68-204`; `Openthesia/Openthesia.csproj:52-65`).

## Architecture and extension constraints

- **Desktop/UI stack:** one .NET 6 executable using Veldrid/SDL2 and immediate-mode ImGui. The main loop owns all view rendering; full-screen views subclass `ImGuiWindow`, and adding a new main view requires registration plus enum/routing changes (`Openthesia/Openthesia.csproj:3-12`, `:52-65`; `Openthesia/Program.cs:23-104`; `Openthesia/Core/ImGuiWindow.cs:6-75`; `Openthesia/Core/WindowsManager.cs:4-21`).
- **Global state:** playback, selected file, MIDI I/O, settings, canvas controls, recorder, and active view are mostly static singletons. This makes cross-feature access simple but tightly couples behavior to process-global state (`Openthesia/Core/Midi/MidiFileData.cs:6-18`; `Openthesia/Core/Midi/MidiPlayer.cs:7-17`; `Openthesia/Core/ScreenCanvasControls.cs:6-27`; `Openthesia/Settings/DevicesManager.cs:6-10`).
- **Renderer concentration:** visualization, seeking/input handling, learning gating, hand editing, all playback/play-mode controls, sound selection, and recording controls are concentrated in the 1,235-line static `ScreenCanvas` class (`Openthesia/Ui/ScreenCanvas.cs:23-43`, `:206-638`, `:688-1235`).
- **Extension seams:** `IPlugin`/`PluginsChain` abstract audio/MIDI processing and ordered effects (`Openthesia/Core/Plugins/IPlugin.cs:11-65`; `Openthesia/Core/Plugins/PluginsChain.cs:29-55`, `:88-139`). `AudioOutputStartup.TryStart` is a small injectable lifecycle seam used to make complete ASIO startup/fallback testable (`Openthesia/Core/Audio/AudioOutputStartup.cs:3-44`; `Openthesia/Settings/AudioDriverManager.cs:95-105`). Views have a common base, but there is no dependency-injection or module/plugin discovery system for application features.
- **Manual persistence expansion:** each new persisted setting requires a `SettingsData` field and corresponding explicit load/save plumbing (`Openthesia/Settings/SettingsData.cs:6-46`; `Openthesia/Core/ProgramData.cs:84-128`, `:163-197`). No schema version or migration framework is present.
- **Platform/runtime:** the target is .NET 6. README declares official Windows support and experimental Linux-via-Wine operation, with video recording unavailable under Wine (`README.md:34-47`). Screen capture and VST editor hosting use Windows-specific libraries/APIs (`Openthesia/Openthesia.csproj:58-64`; `Openthesia/Core/ScreenRecorder.cs:25-43`; `Openthesia/Core/Plugins/VstPlugin.cs:152-188`). The process is force-killed at shutdown as a temporary ASIO4ALL workaround (`Openthesia/Program.cs:106-113`).
- **Display/build constraints:** the main window starts maximized at 1280×720 and enforces a DPI-scaled minimum based on that size (`Openthesia/Program.cs:28-50`). The application project declares AnyCPU and x64, but VST.NET supplies x64 host artifacts and the dependable test invocation uses `-p:Platform=x64` (`Openthesia/Openthesia.csproj:3-11`, `:64-65`; `Openthesia.sln:11-39`).

## Recent fork-specific changes and verification

Recent local history adds or completes five user-visible/reliability changes on top of upstream 1.5.3:

- `174ad20`: corrected held-note/sustain-pedal state and added regression tests.
- `b73879c` / merge `16b2905`: made the note-label type menu selectable.
- `d418312` / merge `f448b42`: added Ctrl+wheel timeline scrubbing.
- `758ddd2`, `e50e56d` / merge `5fe307e`: completed ASIO startup failure handling and WaveOut fallback.
- `e7b0edf`: fixed home-menu hover flicker; this commit is on the current branch and not yet in `origin/master` at inventory time.

The current automated suite covers sustain state and ASIO/output-startup selection/lifecycle, not end-to-end UI, file playback, learning behavior, recording, VST devices, or video capture (`Openthesia.Tests/Core/Midi/SustainStateTests.cs:6-77`; `Openthesia.Tests/Core/Audio/AudioOutputStartupTests.cs:6-114`; `Openthesia.Tests/Settings/AudioDriverManagerTests.cs:6-36`). On 2026-08-22, `dotnet test Openthesia.Tests\Openthesia.Tests.csproj -c Release -p:Platform=x64 --no-restore` passed all 13 tests after granting access to the user NuGet configuration. The build emitted dependency/version and VST deployment warnings.

## Uncertainties and evidence limits

- This is source/test/history evidence, not a hands-on UI/device certification. Physical MIDI, ASIO hardware, third-party VSTs, Wine behavior, and ScreenRecorder output were not manually exercised.
- The exact contents of packaged releases (installer payload, bundled SoundFonts, or songs) cannot be inferred from this repository alone. The source expects `SoundFonts/SalamanderGrandPiano.sf2` when present but falls back to the first `.sf2` (`Openthesia/Core/SoundFonts/SoundFontPlayer.cs:59-80`).
- The README calls SoundFonts “built-in and external,” but no `.sf2` is tracked in this checkout; whether releases bundle the credited fonts is packaging-dependent (`README.md:30`, `:72-79`).
- Absence claims are based on a repository-wide search of code, project files, docs, and tests; generated icon data and untracked local song files were excluded from semantic searches.
