using IconFonts;
using ImGuiNET;
using Openthesia.Core;
using Openthesia.Core.Accessibility;
using Openthesia.Core.Midi;
using Openthesia.Settings;
using Openthesia.Ui.Helpers;
using Openthesia.Ui.Accessibility;
using System.Numerics;

namespace Openthesia.Ui.Windows;

public class MidiBrowserWindow : ImGuiWindow
{
    private string _searchBuffer = string.Empty;
    private bool _alphabeticOrder = true;

    public MidiBrowserWindow()
    {
        _id = Enums.Windows.MidiBrowser.ToString();
        _active = false;
    }

    private void RenderSearchBar()
    {
        var searchHeight = ImGui.GetFrameHeightWithSpacing() + ImGuiUtils.FixedSize(new Vector2(12)).Y;
        if (ImGui.BeginChild("Searchbar container", new(ImGui.GetContentRegionAvail().X, searchHeight)))
        {
            string orderIcon = _alphabeticOrder ? FontAwesome6.ArrowDownAZ : FontAwesome6.ArrowUpAZ;
            var orderLabel = _alphabeticOrder ? "Sort A to Z" : "Sort Z to A";
            void ToggleSort() => _alphabeticOrder = !_alphabeticOrder;
            if (ImGui.Button($"{orderIcon} {orderLabel}"))
                ToggleSort();
            ImGuiAccessibility.Toggle(
                "song-library.sort",
                "Sort ascending",
                _alphabeticOrder,
                ToggleSort,
                "Order Songs and Charts alphabetically by name.");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(Math.Max(100f, ImGui.GetContentRegionAvail().X));
            ImGui.InputTextWithHint($"Search {FontAwesome6.MagnifyingGlass}", "Search midi file...", ref _searchBuffer, 1000);
            ImGuiAccessibility.Edit(
                "song-library.search",
                "Search Songs and Charts",
                _searchBuffer,
                value => _searchBuffer = value.Length <= 1000 ? value : value[..1000]);
        }
        ImGui.EndChild();
    }

    private void RenderBrowser()
    {
        if (CoreSettings.AnimatedBackground && AccessibilityRuntime.Presentation.AllowDecorativeMotion)
            Drawings.RenderMatrixBackground();

        // browser theme
        ImGui.PushStyleColor(
            ImGuiCol.ChildBg,
            AccessibilityRuntime.Presentation.UseSystemContrast
                ? AccessibilityRuntime.ContrastPalette.Window
                : new Vector4(ThemeManager.MainBgCol.X, ThemeManager.MainBgCol.Y, ThemeManager.MainBgCol.Z, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, ImGuiUtils.FixedSize(new Vector2(10)));

        using (AutoFont font22 = new(FontController.GetFontOfSize(22)))
        {
            var margin = Math.Max(8f, Math.Min(ImGuiUtils.FixedSize(new Vector2(24)).X, _io.DisplaySize.X * 0.04f));
            var top = Math.Min(ImGuiUtils.FixedSize(new Vector2(115)).Y, _io.DisplaySize.Y * 0.22f);
            var containerSize = new Vector2(
                Math.Max(200f, _io.DisplaySize.X - margin * 2),
                Math.Max(200f, _io.DisplaySize.Y - top - margin));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, Math.Max(2f, 2f * FontController.DSF));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 10f * FontController.DSF);
            ImGui.SetCursorScreenPos(new Vector2(margin, top));
            var visible = ImGui.BeginChild(
                "Midi browser container",
                containerSize,
                ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.Border);
            ImGui.PopStyleVar(2);
            if (visible)
            {
                ImGui.Text($"{FontAwesome6.Folder} MIDI File Browser");
                ImGui.Spacing();
                RenderSearchBar();
                ImGui.Separator();

                var sourceListPosition = ImGui.GetCursorScreenPos();
                var sourceListSize = ImGui.GetContentRegionAvail();
                UiAutomationRuntime.Coordinator.Register(
                    new AccessibilityNode(
                        "song-library.sources",
                        "song-library",
                        AccessibilityRole.List,
                        "MIDI Sources")
                    {
                        Bounds = new AccessibilityBounds(
                            sourceListPosition.X,
                            sourceListPosition.Y,
                            sourceListSize.X,
                            sourceListSize.Y)
                    });
                if (ImGui.BeginChild("Midi file list", sourceListSize))
                {
                    if (ImGui.BeginTable("File Table", 1, ImGuiTableFlags.PadOuterX))
                    {
                        ImGui.TableSetupColumn("Name");

                        List<string> midiFiles = new();
                        foreach (var midiPath in MidiPathsManager.MidiPaths)
                        {
                            var files = Directory.GetFiles(midiPath, "*.mid");
                            midiFiles.AddRange(files);
                        }
                        var sortedFiles = SortFiles(midiFiles);
                        foreach (var file in sortedFiles)
                        {
                            if (!Path.GetFileName(file).ToLower().Contains(_searchBuffer.ToLower()) && _searchBuffer != string.Empty)
                                continue;

                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            void SelectChart()
                            {
                                MidiFileHandler.LoadMidiFile(file);
                                // we start and stop the playback so we can change the time before playing the song,
                                // else falling notes and keypresses are mismatched
                                MidiPlayer.Playback.Start();
                                MidiPlayer.Playback.Stop();
                                WindowsManager.SetWindow(Enums.Windows.ModeSelection);
                            }
                            var sourceId = ImGuiAccessibility.StableId(
                                "song-library.midi-source",
                                file);
                            if (ImGui.Selectable(Path.GetFileName(file)))
                            {
                                SelectChart();
                                UiAutomationRuntime.NotifyActionCompleted(
                                    sourceId,
                                    AccessibilityAction.Invoke);
                            }
                            ImGuiAccessibility.RegisterLastItem(
                                new AccessibilityNode(
                                    sourceId,
                                    "song-library.sources",
                                    AccessibilityRole.ListItem,
                                    Path.GetFileNameWithoutExtension(file))
                                {
                                    Description = "Import this MIDI Source and open its Chart's Practice setup.",
                                    IsFocusable = true,
                                    SupportedActions = AccessibilityAction.Invoke |
                                                       AccessibilityAction.Focus
                                },
                                _ => SelectChart());
                        }

                        ImGui.EndTable();
                    }
                    ImGui.EndChild();
                }
            }
            ImGui.EndChild();

            ImGui.PopStyleColor(); // child bg
            ImGui.PopStyleVar(); // window padding
        }
    }

    private List<string> SortFiles(List<string> midiFiles)
    {
        return _alphabeticOrder ? midiFiles.OrderBy(path => Path.GetFileName(path)).ToList() : midiFiles.OrderByDescending(path => Path.GetFileName(path)).ToList();
    }

    protected override void OnImGui()
    {
        using (AutoFont font16_icon16 = new(FontController.Font16_Icon16))
        {
            var margin = Math.Max(8f, Math.Min(ImGuiUtils.FixedSize(new Vector2(22)).X, _io.DisplaySize.X * 0.04f));
            var gap = ImGuiUtils.FixedSize(new Vector2(10)).X;
            var buttonWidth = Math.Min(
                ImGuiUtils.FixedSize(new Vector2(150)).X,
                Math.Max(100f, (_io.DisplaySize.X - margin * 2 - gap) / 2));
            var buttonHeight = ImGuiUtils.FixedSize(new Vector2(50)).Y;
            ImGui.SetCursorScreenPos(new Vector2(margin, Math.Min(buttonHeight, _io.DisplaySize.Y * 0.08f)));
            void GoBack() => WindowsManager.SetWindow(Enums.Windows.Home);
            var backInvoked = ImGui.Button(
                                  $"{FontAwesome6.ArrowLeftLong} Back",
                                  new Vector2(buttonWidth, buttonHeight)) ||
                              EscapeReturns();
            if (backInvoked)
                GoBack();
            ImGuiAccessibility.Button(
                "song-library.back",
                "Back",
                GoBack,
                "Return to Home.",
                invoked: backInvoked);

            ImGuiTheme.PushButton(ImGuiTheme.HtmlToVec4("#0EA5E9"), ImGuiTheme.HtmlToVec4("#096E9B"), ImGuiTheme.HtmlToVec4("#0EA5E9"));
            ImGui.SameLine();
            void OpenFile()
            {
                if (!MidiFileHandler.OpenMidiDialog())
                    return;

                MidiPlayer.Playback.Start();
                MidiPlayer.Playback.Stop();
                WindowsManager.SetWindow(Enums.Windows.ModeSelection);
            }
            var openFileInvoked = ImGui.Button(
                $"Open file {FontAwesome6.FileImport}",
                new Vector2(buttonWidth, buttonHeight));
            if (openFileInvoked)
                OpenFile();
            ImGuiAccessibility.Button(
                "song-library.open-file",
                "Open MIDI Source",
                OpenFile,
                "Choose a MIDI Source from the Windows file dialog.",
                invoked: openFileInvoked);
            ImGuiTheme.PopButton();

            RenderBrowser();
        }
    }

    private static bool EscapeReturns()
    {
        return !ImGui.GetIO().WantTextInput &&
               !ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId) &&
               ImGui.IsKeyPressed(ImGuiKey.Escape, false);
    }
}
