using IconFonts;
using ImGuiNET;
using NAudio.Wave;
using System.Numerics;
using Vanara.PInvoke;
using static Openthesia.Settings.CoreSettings;
using static Openthesia.Settings.AudioDriverManager;
using static Openthesia.Settings.DevicesManager;
using static Openthesia.Settings.MidiPathsManager;
using static Openthesia.Settings.SoundFontsPathsManager;
using static Openthesia.Settings.ThemeManager;
using Openthesia.Ui.Helpers;
using Openthesia.Core;
using Openthesia.Core.Accessibility;
using Openthesia.Core.FileDialogs;
using Openthesia.Core.Midi;
using Openthesia.Core.Practice;
using Openthesia.Core.SoundFonts;
using Openthesia.Enums;
using Openthesia.Settings;
using Openthesia.Core.Plugins;
using System.IO;
using Openthesia.Ui.Accessibility;

namespace Openthesia.Ui.Windows;

public class SettingsWindow : ImGuiWindow
{
    private const string NoDeviceToken = "none";

    public SettingsWindow()
    {
        _id = Enums.Windows.Settings.ToString();
        _active = false;
    }

    protected override void OnImGui()
    {
        ImGui.BeginChild("Settings", ImGui.GetContentRegionAvail(), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar);
        ImGui.PushFont(FontController.GetFontOfSize(22));

        if (AnimatedBackground && AccessibilityRuntime.Presentation.AllowDecorativeMotion)
        {
            Drawings.RenderMatrixBackground();
        }

        var display = ImGui.GetIO().DisplaySize;
        var margin = Math.Max(8f, Math.Min(ImGuiUtils.FixedSize(new Vector2(22)).X, display.X * 0.04f));
        var buttonHeight = ImGuiUtils.FixedSize(new Vector2(50)).Y;
        ImGui.PushFont(FontController.Font16_Icon16);
        ImGui.SetCursorScreenPos(new Vector2(margin, Math.Min(buttonHeight, display.Y * 0.08f)));
        void GoBack() => WindowsManager.SetWindow(Enums.Windows.Home);
        var backInvoked = ImGui.Button(
                              $"{FontAwesome6.ArrowLeftLong} Back",
                              ImGuiUtils.FixedSize(new Vector2(120, 50))) ||
                          (!ImGui.GetIO().WantTextInput &&
                           !ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId) &&
                           ImGui.IsKeyPressed(ImGuiKey.Escape, false));
        if (backInvoked)
        {
            GoBack();
        }
        ImGuiAccessibility.Button(
            "device-settings.back",
            "Back",
            GoBack,
            "Return to Home.",
            invoked: backInvoked);
        ImGui.PopFont();

        ImGui.PushFont(FontController.Title);
        var textPos = new Vector2(display.X / 2 - ImGui.CalcTextSize("SETTINGS").X / 2, display.Y / 20);
        ImGui.SetCursorPos(textPos);
        ImGui.Text("SETTINGS");
        ImGui.PopFont();

        ImGuiTheme.Style.FramePadding = ImGuiUtils.FixedSize(new Vector2(15));
        ImGuiTheme.PushButton(ImGuiTheme.HtmlToVec4("#0284C7"), ImGuiTheme.HtmlToVec4("#0284C7"), ImGuiTheme.HtmlToVec4("#0284C7"));
        ImGuiTheme.Style.WindowPadding = ImGuiUtils.FixedSize(new Vector2(10));

        var controlsTop = Math.Min(ImGuiUtils.FixedSize(new Vector2(135)).Y, display.Y * 0.22f);
        var controlsSize = new Vector2(
            Math.Max(200f, display.X - margin * 2),
            Math.Max(200f, display.Y - controlsTop - margin));
        ImGui.SetCursorScreenPos(new Vector2(margin, controlsTop));
        ImGui.BeginChild(
            "Settings controls",
            controlsSize,
            ImGuiChildFlags.AlwaysUseWindowPadding,
            ImGuiWindowFlags.HorizontalScrollbar);

        // MIDI DEVICES
        ImGui.Text($"MIDI DEVICES {FontAwesome6.Keyboard}");
        ImGui.Spacing();

        var inputDevices = GetInputDeviceDescriptors();
        var hasInputDevices = inputDevices.Count > 0;
        var selectedInputToken = ActiveInputDeviceToken ?? NoDeviceToken;
        IEnumerable<(string Id, string Name, string Value)> inputOptions = new[]
            {
                ("device-settings.input-device.none", "None", NoDeviceToken)
            }
            .Concat(inputDevices.Select(device => (
                ImGuiAccessibility.StableId("device-settings.input-device", device.Token),
                device.Name,
                device.Token)));
        if (ActiveInputDeviceToken is not null &&
            inputDevices.All(device => device.Token != ActiveInputDeviceToken))
        {
            inputOptions = inputOptions.Append((
                ImGuiAccessibility.StableId(
                    "device-settings.input-device",
                    ActiveInputDeviceToken),
                $"{ActiveInputDeviceName ?? "MIDI input device"} (Disconnected)",
                ActiveInputDeviceToken));
        }
        ImGuiAccessibility.ComboBox(
            "device-settings.input-device",
            "MIDI input device",
            selectedInputToken,
            inputOptions,
            token =>
            {
                if (token == NoDeviceToken)
                {
                    ReleaseInputDevice();
                    return;
                }

                TrySetInputDevice(token);
            },
            "Choose the keyboard used for Practice input.",
            enabled: true);

        ImGui.SameLine();
        if (!hasInputDevices)
            ImGui.BeginDisabled();
        ImGui.Checkbox("Velocity 0 is Note-Off", ref VelocityZeroIsNoteOff);
        ImGuiAccessibility.Toggle(
            "device-settings.velocity-zero-note-off",
            "Velocity zero is Note-Off",
            VelocityZeroIsNoteOff,
            () => VelocityZeroIsNoteOff = !VelocityZeroIsNoteOff,
            enabled: hasInputDevices);

        if (!hasInputDevices)
            ImGui.EndDisabled();

        ImGui.Dummy(new(10));

        var outputDevices = GetOutputDeviceDescriptors();
        var selectedOutputToken = ActiveOutputDeviceToken ?? NoDeviceToken;
        IEnumerable<(string Id, string Name, string Value)> outputOptions = new[]
            {
                ("device-settings.output-device.none", "None", NoDeviceToken)
            }
            .Concat(outputDevices.Select(device => (
                ImGuiAccessibility.StableId("device-settings.output-device", device.Token),
                device.Name,
                device.Token)));
        if (ActiveOutputDeviceToken is not null &&
            outputDevices.All(device => device.Token != ActiveOutputDeviceToken))
        {
            outputOptions = outputOptions.Append((
                ImGuiAccessibility.StableId(
                    "device-settings.output-device",
                    ActiveOutputDeviceToken),
                $"{ActiveOutputDeviceName ?? "MIDI output device"} (Disconnected)",
                ActiveOutputDeviceToken));
        }
        ImGuiAccessibility.ComboBox(
            "device-settings.output-device",
            "MIDI output device",
            selectedOutputToken,
            outputOptions,
            token =>
            {
                if (token == NoDeviceToken)
                {
                    ReleaseOutputDevice();
                    return;
                }

                TrySetOutputDevice(token);
            },
            "Choose the keyboard output used for Lighted Keyboard Guidance.",
            enabled: true);

        ImGui.Dummy(new(10));

        var lightedKeyboard = CoreSettings.LightedKeyboard;
        var lightedKeyboardEnabled = lightedKeyboard.Enabled;
        if (ImGui.Checkbox("Lighted Keyboard Guidance", ref lightedKeyboardEnabled))
        {
            CoreSettings.SetLightedKeyboardSettings(
                lightedKeyboardEnabled,
                lightedKeyboard.MidiChannel);
            MidiPracticeSession.RefreshLightedKeyboardGuidance();
        }
        ImGuiAccessibility.Toggle(
            "device-settings.lighted-keyboard-guidance",
            "Lighted Keyboard Guidance",
            CoreSettings.LightedKeyboard.Enabled,
            () =>
            {
                CoreSettings.SetLightedKeyboardSettings(
                    !CoreSettings.LightedKeyboard.Enabled,
                    CoreSettings.LightedKeyboard.MidiChannel);
                MidiPracticeSession.RefreshLightedKeyboardGuidance();
            },
            "Send the next Required Hands target to a keyboard's light or navigate channel.");

        var lightedKeyboardChannel = CoreSettings.LightedKeyboard.MidiChannel;
        ImGui.SetNextItemWidth(ImGuiUtils.FixedSize(new Vector2(180)).X);
        if (ImGui.SliderInt("Light channel", ref lightedKeyboardChannel, 1, 16))
        {
            CoreSettings.SetLightedKeyboardSettings(
                CoreSettings.LightedKeyboard.Enabled,
                lightedKeyboardChannel);
            MidiPracticeSession.RefreshLightedKeyboardGuidance();
        }
        ImGuiAccessibility.Slider(
            "device-settings.lighted-keyboard-channel",
            "Lighted Keyboard Guidance channel",
            CoreSettings.LightedKeyboard.MidiChannel,
            1,
            16,
            1,
            value =>
            {
                CoreSettings.SetLightedKeyboardSettings(
                    CoreSettings.LightedKeyboard.Enabled,
                    (int)Math.Round(value));
                MidiPracticeSession.RefreshLightedKeyboardGuidance();
            },
            "Match the keyboard's light or navigate MIDI channel.");
        ImGui.TextWrapped(
            "Uses standard low-velocity MIDI notes for the next Required Hands Practice Target in Wait for Notes and Play in Time. " +
            "Match this channel to the keyboard's light or navigate channel. The keyboard may sound guide notes unless its guide sound is disabled.");
        if (CoreSettings.LightedKeyboard.Enabled && ODevice is null)
            ImGui.TextWrapped("Select a MIDI output device before using Lighted Keyboard Guidance.");

        ImGuiTheme.PopButton();

        ImGui.Dummy(new(50));

        // MIDI PATHS
        ImGui.Text($"MIDI PATHS {FontAwesome6.FolderOpen}");
        ImGui.Spacing();

        ImGui.BeginTable("Midi paths scan", 3, ImGuiTableFlags.PadOuterX | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
        ImGui.TableSetupColumn("Path");
        ImGui.TableSetupColumn("N° of midi", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("##delete midi path", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableHeadersRow();

        int index = 0;
        foreach (var path in MidiPaths.ToList())
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            ImGui.Text(path);

            int nMidis = 0;
            foreach (var midiFile in Directory.GetFiles(path))
            {
                if (Path.GetExtension(midiFile) == ".mid")
                {
                    nMidis++;
                }
            }
            ImGui.TableSetColumnIndex(1);
            ImGui.Text(nMidis.ToString());
            ImGui.TableSetColumnIndex(2);
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = new Vector4(1, 0, 0.2f, 1);
            ImGui.PushFont(FontController.Font16_Icon12);
            ImGui.PushID(index.ToString());
            if (ImGui.SmallButton($"{FontAwesome6.CircleXmark}##remove_midi_path"))
            {
                MidiPaths.Remove(path);
            }
            ImGui.PopID();
            ImGui.PopFont();
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = new Vector4(1);
            index++;
        }

        ImGui.EndTable();

        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - ImGuiUtils.FixedSize(new Vector2(100)).X);
        if (ImGui.Button($"{FontAwesome6.FolderPlus}##addMidiPath", ImGuiUtils.FixedSize(new Vector2(100, 50))))
        {
            var dlg = new FolderPicker();
            dlg.InputPath = "C:\\";
            if (dlg.ShowDialog(Program._window.Handle) == true)
            {
                if (MidiPaths.Contains(dlg.ResultPath))
                {
                    User32.MessageBox(IntPtr.Zero, "Specified folder is already present", "Error", User32.MB_FLAGS.MB_ICONERROR | User32.MB_FLAGS.MB_TOPMOST);
                }
                else
                {
                    MidiPaths.Add(dlg.ResultPath);
                }
            }
        }

        // SOUND
        ImGui.Text($"SOUND {FontAwesome6.Music}");
        ImGui.Spacing();

        if (ImGui.BeginCombo("Sound Engine", CoreSettings.SoundEngine.ToString()))
        {
            foreach (var engine in Enum.GetValues<SoundEngine>())
            {
                if (ImGui.Selectable(engine.ToString(), engine == CoreSettings.SoundEngine))
                {
                    SetSoundEngine(engine);

                    User32.MessageBox(IntPtr.Zero, "A restart of the application is required to apply the changes.\n" +
                        "The app will automatically close after closing this window.", "Info",
                        User32.MB_FLAGS.MB_ICONINFORMATION | User32.MB_FLAGS.MB_TOPMOST);

                    Application.AppInstance.Quit();
                }

                if (engine == Enums.SoundEngine.SoundFonts)
                    ImGui.SetItemTooltip("Built in or external soundfonts will be used for audio playback");
                else if (engine == Enums.SoundEngine.Plugins)
                    ImGui.SetItemTooltip("VST Plugins will be used for audio playback and processing");
            }
            ImGui.EndCombo();
        }

        if (CoreSettings.SoundEngine != Enums.SoundEngine.None)
        {
            ImGui.Dummy(new(10));

            if (ImGui.BeginCombo("Audio driver", AudioDriverType.ToString()))
            {
                foreach (var driver in Enum.GetValues<AudioDriverTypes>())
                {
                    var flag = driver == AudioDriverTypes.ASIO && !AsioOut.GetDriverNames().Any()
                        ? ImGuiSelectableFlags.Disabled : ImGuiSelectableFlags.None;

                    if (ImGui.Selectable(driver.ToString(), false, flag))
                    {
                        if (driver == AudioDriverTypes.ASIO)
                        {
                            // if switching to ASIO, select the first availible driver
                            SetAsioDriverDevice(AsioOut.GetDriverNames()[0]);
                        }
                        // change driver type
                        SetAudioDriverType(driver);

                        User32.MessageBox(IntPtr.Zero, "A restart of the application is required.\n" +
                            "The app will automatically close after closing this window.", "Info",
                            User32.MB_FLAGS.MB_TOPMOST | User32.MB_FLAGS.MB_ICONINFORMATION);

                        Application.AppInstance.Quit();
                    }
                }
                ImGui.EndCombo();
            }
            Drawings.Tooltip("Driver used by SoundFonts or Plugins for sound playback\n" +
                "- WaveOut: higher latency, good enough for listening only\n" +
                "- ASIO: lower latency, ideal if playing a midi instrument");

            if (AudioDriverType == AudioDriverTypes.ASIO)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.32f, 0.94f, 1));
                ImGui.SameLine();
                if (ImGui.Button("ASIO settings"))
                {
                    if (CoreSettings.SoundEngine == Enums.SoundEngine.SoundFonts)
                        MidiPlayer.SoundFontEngine?.AsioOut?.ShowControlPanel();
                    else if (CoreSettings.SoundEngine == Enums.SoundEngine.Plugins)
                        VstPlayer.AsioOut?.ShowControlPanel();
                }
                ImGui.PopStyleColor();

                ImGui.Dummy(new(10));

                if (ImGui.BeginCombo("ASIO driver", SelectedAsioDriverName))
                {
                    foreach (var driver in AsioOut.GetDriverNames())
                    {
                        if (ImGui.Selectable(driver))
                        {
                            SetAsioDriverDevice(driver);

                            User32.MessageBox(IntPtr.Zero, "A restart of the application is required.\n" +
                                "The app will automatically close after closing this window.", "Info",
                                User32.MB_FLAGS.MB_TOPMOST | User32.MB_FLAGS.MB_ICONINFORMATION);

                            Application.AppInstance.Quit();
                        }
                    }
                    ImGui.EndCombo();
                }
            }

            if (AudioDriverType == AudioDriverTypes.WaveOut)
            {
                ImGui.Dummy(new(10));

                if (ImGui.SliderInt("SoundFont latency (WaveOut driver only)", ref WaveOutLatency, 15, 300, "%i", ImGuiSliderFlags.AlwaysClamp | ImGuiSliderFlags.NoInput))
                {
                    if (CoreSettings.SoundEngine == Enums.SoundEngine.SoundFonts)
                        MidiPlayer.SoundFontEngine?.ChangeLatency(WaveOutLatency);
                    else if (CoreSettings.SoundEngine == Enums.SoundEngine.Plugins)
                        VstPlayer.ChangeLatency(WaveOutLatency);
                }
                Drawings.Tooltip("Lower values reduce sound lag but can introduce audio artifacts, " +
                    "values under 100 are recommended for an optimal playback (default = 75)");
            }
        }

        ImGui.Dummy(new(10));

        // SOUNDFONTS
        ImGui.Text($"SOUND FONTS {FontAwesome6.FolderOpen}");
        ImGui.Spacing();

        ImGui.BeginTable("Sound fonts paths scan", 3, ImGuiTableFlags.PadOuterX | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
        ImGui.TableSetupColumn("Path");
        ImGui.TableSetupColumn("N° of sound fonts", ImGuiTableColumnFlags.WidthFixed, 220);
        ImGui.TableSetupColumn("##delete sound font path", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableHeadersRow();

        int index2 = 0;
        foreach (var path in SoundFontsPaths.ToList())
        {
            // disable built in path
            if (path == SoundFontsPaths[0])
                ImGui.BeginDisabled(true);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            ImGui.Text(path);

            int nSoundFont = 0;
            foreach (var soundFont in Directory.GetFiles(path))
            {
                if (Path.GetExtension(soundFont) == ".sf2")
                {
                    nSoundFont++;
                }
            }
            ImGui.TableSetColumnIndex(1);
            ImGui.Text(nSoundFont.ToString());
            ImGui.TableSetColumnIndex(2);
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = new Vector4(1, 0, 0.2f, 1);
            ImGui.PushFont(FontController.Font16_Icon12);
            ImGui.PushID(index2.ToString());
            if (ImGui.SmallButton($"{FontAwesome6.CircleXmark}##remove_soundfont_path"))
            {
                SoundFontsPaths.Remove(path);
            }
            ImGui.PopID();
            ImGui.PopFont();
            ImGuiTheme.Style.Colors[(int)ImGuiCol.Text] = new Vector4(1);
            index2++;

            if (path == SoundFontsPaths[0])
                ImGui.EndDisabled();
        }

        ImGui.EndTable();

        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - ImGuiUtils.FixedSize(new Vector2(100)).X);
        if (ImGui.Button($"{FontAwesome6.FolderPlus}##addSoundPath", ImGuiUtils.FixedSize(new Vector2(100, 50))))
        {
            var dlg = new FolderPicker();
            dlg.InputPath = "C:\\";
            if (dlg.ShowDialog(Program._window.Handle) == true)
            {
                if (SoundFontsPaths.Contains(dlg.ResultPath))
                {
                    User32.MessageBox(IntPtr.Zero, "Specified folder is already present", "Error", User32.MB_FLAGS.MB_ICONERROR | User32.MB_FLAGS.MB_TOPMOST);
                }
                else
                {
                    SoundFontsPaths.Add(dlg.ResultPath);
                }
            }
        }

        // PLUGINS
        ImGui.Text($"PLUGINS (VST) {FontAwesome6.Plug}");
        ImGui.Spacing();

        ImGui.Dummy(new(10));

        ImGui.Checkbox("Open plugins at startup", ref OpenPluginAtStart);
        ImGui.SetItemTooltip("When on, plugin windows are opened when the application is run.");

        ImGui.Dummy(new(10));

        ImGui.SeparatorText("Plugin Instrument");

        string instrumentName = VstPlayer.PluginsChain?.PluginInstrument == null
            ? "None" : VstPlayer.PluginsChain.PluginInstrument.PluginName;

        ImGui.BeginDisabled();
        ImGui.InputText("##instrument", ref instrumentName, 1000, ImGuiInputTextFlags.ReadOnly);
        ImGui.EndDisabled();

        if (VstPlayer.PluginsChain?.PluginInstrument is VstPlugin vst)
        {
            ImGui.SameLine();
            if (ImGui.Button(FontAwesome6.ScrewdriverWrench))
            {
                vst.OpenPluginWindow();
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(CoreSettings.SoundEngine != Enums.SoundEngine.Plugins);
        if (ImGui.Button($"Choose##plugin_instrument"))
        {
            var dialog = new OpenFileDialog()
            {
                Title = "Select a VST2 plugin instrument",
                Filter = "vst plugin (*.dll)|*.dll"
            };
            dialog.ShowOpenFileDialog();

            if (dialog.Success)
            {
                var file = new FileInfo(dialog.Files.First());
                var plugin = new VstPlugin(file.FullName);
                if (plugin.PluginType != PluginType.Instrument)
                {
                    plugin.Dispose();
                    User32.MessageBox(IntPtr.Zero, "Plugin is not an instrument.", "Error Loading Plugin",
                        User32.MB_FLAGS.MB_ICONERROR | User32.MB_FLAGS.MB_TOPMOST);
                }
                else
                {
                    VstPlayer.PluginsChain.AddPlugin(plugin);
                    PluginsPathManager.LoadValidInstrumentPath(file.FullName);
                }
            }
        }
        ImGui.EndDisabled();

        ImGui.Dummy(new(10));
        ImGui.SeparatorText("Plugin Effects");

        ImGui.BeginDisabled();
        if (CoreSettings.SoundEngine != Enums.SoundEngine.Plugins || VstPlayer.PluginsChain?.FxPlugins.Count == 0)
        {
            string effectName = "None";
            ImGui.InputText("##fx", ref effectName, 100, ImGuiInputTextFlags.ReadOnly);

            ImGui.SameLine();
            ImGui.ArrowButton("move_up", ImGuiDir.Up);
            ImGui.SameLine();
            ImGui.ArrowButton("move_down", ImGuiDir.Down);
        }
        else if (CoreSettings.SoundEngine == Enums.SoundEngine.Plugins && VstPlayer.PluginsChain != null)
        {
            foreach (var effect in VstPlayer.PluginsChain.FxPlugins.ToList())
            {
                string effectName = effect.PluginName;
                ImGui.InputText($"##{effect.PluginId}", ref effectName, 1000, ImGuiInputTextFlags.ReadOnly);

                bool enabled = effect.Enabled;

                ImGui.SameLine();
                ImGui.EndDisabled();
                if (ImGui.Checkbox($"##enabled{effect.PluginId}", ref enabled))
                {
                    effect.Enabled = !effect.Enabled;
                }
                ImGui.SameLine();
                if (effect is VstPlugin vstEffect)
                {
                    if (ImGui.Button($"{FontAwesome6.ScrewdriverWrench}##{effect.PluginId}"))
                    {
                        vstEffect.OpenPluginWindow();
                    }
                }
                ImGui.SameLine();
                if (ImGui.ArrowButton($"move_up{effect.PluginId}", ImGuiDir.Up))
                {
                    int pluginIndex = VstPlayer.PluginsChain.FxPlugins.IndexOf(effect);
                    int otherIndex = pluginIndex - 1;
                    if (otherIndex >= 0)
                    {
                        VstPlayer.PluginsChain.SwapFxPlugins(pluginIndex, otherIndex);
                    }
                }
                ImGui.SameLine();
                if (ImGui.ArrowButton($"move_down{effect.PluginId}", ImGuiDir.Down))
                {
                    int pluginIndex = VstPlayer.PluginsChain.FxPlugins.IndexOf(effect);
                    int otherIndex = pluginIndex + 1;
                    if (otherIndex < VstPlayer.PluginsChain.FxPlugins.Count)
                    {
                        VstPlayer.PluginsChain.SwapFxPlugins(pluginIndex, otherIndex);
                    }
                }
                ImGui.SameLine();
                ImGuiTheme.PushButton(new Vector4(0.8f, 0, 0.2f, 1), new Vector4(0.7f, 0, 0.2f, 1), new Vector4(1, 0, 0.2f, 1));
                if (ImGui.Button($"{FontAwesome6.PlugCircleXmark}##remove_plugin{effect.PluginId}"))
                {
                    VstPlayer.PluginsChain.RemovePlugin(effect);
                    if (effect is VstPlugin plug)
                    {
                        var path = plug.PluginContext.Find<string>("PluginPath");
                        PluginsPathManager.EffectsPath.Remove(path);
                    }
                }
                ImGuiTheme.PopButton();
                ImGui.BeginDisabled();
                ImGui.Spacing();
            }
        }
        ImGui.EndDisabled();

        ImGui.Spacing();

        ImGui.BeginDisabled(CoreSettings.SoundEngine != Enums.SoundEngine.Plugins);
        if (ImGui.Button($"Add Effect##plugin_effect"))
        {
            var dialog = new OpenFileDialog()
            {
                Title = "Select a VST2 plugin effect",
                Filter = "vst plugin (*.dll)|*.dll"
            };
            dialog.ShowOpenFileDialog();

            if (dialog.Success)
            {
                var file = new FileInfo(dialog.Files.First());
                var plugin = new VstPlugin(file.FullName);
                if (plugin.PluginType == PluginType.Instrument)
                {
                    plugin.Dispose();
                    User32.MessageBox(IntPtr.Zero, "Plugin is not an effect.", "Error Loading Plugin",
                        User32.MB_FLAGS.MB_ICONERROR | User32.MB_FLAGS.MB_TOPMOST);
                }
                else
                {
                    VstPlayer.PluginsChain.AddPlugin(plugin);
                    PluginsPathManager.EffectsPath.Add(file.FullName);
                }
            }
        }
        ImGui.EndDisabled();

        ImGui.Dummy(new(50));

        // INPUT
        ImGui.Text($"INPUT {FontAwesome6.Keyboard}");
        ImGui.Spacing();

        ImGui.Checkbox("Keyboard input", ref KeyboardInput);
        Drawings.Tooltip("When keyboard input is enabled, mouse input and shortcuts using letters are disabled");

        ImGui.Dummy(new(50));

        // VIDEO RECORDING
        ImGui.Text($"VIDEO RECORDING {FontAwesome6.Video}");
        ImGui.Spacing();

        ImGui.Dummy(new(10));

        ImGui.Checkbox("Auto Start Playback", ref VideoRecStartsPlayback);
        ImGui.SetItemTooltip("When turned on, midi playback will automatically start when a video record is fired.");
        ImGui.SameLine();
        ImGui.Checkbox("Open Destination Folder", ref VideoRecOpenDestFolder);
        ImGui.SetItemTooltip("When turned on, the destination folder of the recorded video clip will be opened on record stop.");
        ImGui.SameLine();
        ImGui.Checkbox("Auto Play", ref VideoRecAutoPlay);
        ImGui.SetItemTooltip("When turned on, the recorded video clip will be played using your default video player on record stop.");

        ImGui.Dummy(new(10));

        int[] framerates = { 30, 60, 120 };
        if (ImGui.BeginCombo("Recording Framerate", $"{VideoRecFramerate} FPS"))
        {
            foreach (var framerate in framerates)
            {
                if (ImGui.Selectable($"{framerate} FPS", framerate == VideoRecFramerate))
                {
                    SetVideoRecFramerate(framerate);
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SetItemTooltip("Framerate of the recording.\nUse 30 or 60 if you aim to share the video on platforms like YouTube.");

        ImGui.Dummy(new(10));

        ImGui.BeginDisabled();
        ImGui.InputText("Destination Folder", ref VideoRecDestFolder, 10000, ImGuiInputTextFlags.ReadOnly);
        ImGui.EndDisabled();
        ImGui.SetItemTooltip("Folder where video recordings will be saved.");

        ImGui.SameLine();
        if (ImGui.Button($"Change {FontAwesome6.FolderClosed}##video_rec_path"))
        {
            var dlg = new FolderPicker();
            dlg.InputPath = "C:\\";
            if (dlg.ShowDialog(Program._window.Handle) == true)
            {
                SetVideoRecDestFolder(dlg.ResultPath);
            }
        }

        ImGui.Dummy(new(50));

        // LOOK AND FEEL
        ImGui.Text($"LOOK AND FEEL {FontAwesome6.Paintbrush}");
        ImGui.Spacing();

        ImGui.SliderInt("Note block roundness", ref NoteRoundness, 0, 15);

        ImGui.Dummy(new(10));

        ImGui.ColorEdit4("Background color", ref MainBgCol, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoDragDrop);
        ImGui.SameLine();
        ImGui.ColorEdit4("Right Hand color", ref RightHandCol, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoDragDrop);
        ImGui.SameLine();
        ImGui.ColorEdit4("Left Hand color", ref LeftHandCol, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoDragDrop);
        ImGui.SameLine();
        ImGui.Checkbox("Notes glow FX", ref NeonFx);
        Drawings.Tooltip("Adds a subtle glowing effect around each note");

        ImGui.Dummy(new(10));

        ImGui.Checkbox("Colored keypresses", ref KeyPressColorMatch);
        Drawings.Tooltip("Pressed keys color matches notes color");
        ImGui.SameLine();
        ImGui.Checkbox("Velocity as note opacity", ref UseVelocityAsNoteOpacity);
        Drawings.Tooltip("If enabled, note blocks opacity will correspond to their velocity (Midi playback only)");
        ImGui.SameLine();
        ImGui.Checkbox("Animated background", ref AnimatedBackground);
        ImGui.SameLine();
        ImGui.Checkbox("Fps counter", ref FpsCounter);

        ImGui.Dummy(new(10));

        var effectsPreference = AccessibilityRuntime.Settings.VisualEffects;
        ImGuiAccessibility.ComboBox(
            "device-settings.visual-effects",
            "Visual effects",
            effectsPreference,
            Enum.GetValues<VisualEffectsPreference>().Select(preference => (
                $"device-settings.visual-effects.{preference.ToString().ToLowerInvariant()}",
                preference.ToString(),
                preference)),
            AccessibilityRuntime.SetVisualEffects,
            "System follows Windows preferences. Reduce removes decorative motion, glow, and transparency.");
        ImGui.TextWrapped("System follows Windows animation and contrast preferences. Reduce removes decorative motion, glow, and transparency.");
        if (AccessibilityRuntime.Warning is not null)
        {
            ImGui.TextWrapped(AccessibilityRuntime.Warning);
            ImGuiAccessibility.Text(
                "device-settings.accessibility-status",
                "Accessibility settings status",
                AccessibilityRuntime.Warning,
                liveSetting: AccessibilityLiveSetting.Polite);
        }

        ImGui.Dummy(new(10));

        ImGuiTheme.PushButton(ImGuiTheme.HtmlToVec4("#0284C7"), ImGuiTheme.HtmlToVec4("#0284C7"), ImGuiTheme.HtmlToVec4("#0284C7"));
        ImGuiAccessibility.ComboBox(
            "device-settings.theme",
            "Theme",
            Theme,
            Enum.GetValues<Themes>().Select(theme => (
                $"device-settings.theme.{theme.ToString().ToLowerInvariant()}",
                theme.ToString(),
                theme)),
            SetTheme,
            "Choose the application color theme.");
        ImGuiTheme.PopButton();

        ImGui.EndChild();
        ImGui.EndChild();

        ImGui.PopFont();
        ImGuiTheme.PushTheme();
    }

}
