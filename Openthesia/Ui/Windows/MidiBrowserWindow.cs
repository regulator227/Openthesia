using IconFonts;
using ImGuiNET;
using Openthesia.Core;
using Openthesia.Core.Accessibility;
using Openthesia.Core.Midi;
using Openthesia.Settings;
using Openthesia.Ui.Accessibility;
using Openthesia.Ui.Helpers;
using System.Numerics;
using Vanara.PInvoke;

namespace Openthesia.Ui.Windows;

public class MidiBrowserWindow : ImGuiWindow
{
    private const string BrowserId = "midi-source-browser";
    private const string SourcesListId = $"{BrowserId}.sources";

    private readonly MidiBrowserNavigation _navigation = new();
    private string _searchBuffer = string.Empty;
    private bool _alphabeticOrder = true;
    private string? _selectedEntryKey;
    private string? _pendingFocusKey;
    private bool _focusFirstEntry;
    private string? _statusMessage;
    private CancellationTokenSource? _scanCancellation;
    private Task<MidiDiscoveryResult>? _scanTask;
    private string? _scanKey;
    private MidiDiscoveryResult _scanResult = MidiDiscoveryResult.Empty;
    private string? _scanResultKey;
    private MidiDiscoveryProgress _scanProgress = new();

    public MidiBrowserWindow()
    {
        _id = Enums.Windows.MidiBrowser.ToString();
        _active = false;
    }

    protected override void OnActivated()
    {
        if (WindowsManager.Window == Enums.Windows.Home)
        {
            _navigation.Reset();
            _searchBuffer = string.Empty;
            _selectedEntryKey = null;
            _pendingFocusKey = MidiBrowserWindowKeys.AllMidiFiles;
        }
        RequestRefresh(clearExistingResults: true);
    }

    protected override void OnDeactivated()
    {
        _scanCancellation?.Cancel();
    }

    private void RenderSearchBar()
    {
        var compact = ImGui.GetContentRegionAvail().X < ImGuiUtils.FixedSize(new Vector2(650)).X;
        var searchRows = compact ? 2 : 1;
        var searchHeight = ImGui.GetFrameHeightWithSpacing() * searchRows +
                           ImGuiUtils.FixedSize(new Vector2(12)).Y;
        if (ImGui.BeginChild("Searchbar container", new(ImGui.GetContentRegionAvail().X, searchHeight)))
        {
            var sortingEnabled = _navigation.View != MidiBrowserView.SearchPaths;
            ImGui.BeginDisabled(!sortingEnabled);
            var orderIcon = _alphabeticOrder ? FontAwesome6.ArrowDownAZ : FontAwesome6.ArrowUpAZ;
            var orderLabel = _alphabeticOrder ? "Sort A to Z" : "Sort Z to A";
            void ToggleSort() => _alphabeticOrder = !_alphabeticOrder;
            var sortInvoked = ImGui.Button($"{orderIcon} {orderLabel}");
            if (sortInvoked)
                ToggleSort();
            ImGuiAccessibility.Toggle(
                $"{BrowserId}.sort",
                "Sort ascending",
                _alphabeticOrder,
                ToggleSort,
                "Order folders and MIDI Sources alphabetically by name.",
                enabled: sortingEnabled);
            ImGui.EndDisabled();

            ImGui.SameLine();
            void Refresh() => RequestRefresh(clearExistingResults: false);
            var refreshInvoked = ImGui.Button($"{FontAwesome6.ArrowsRotate} Refresh");
            if (refreshInvoked)
                Refresh();
            ImGuiAccessibility.Button(
                $"{BrowserId}.refresh",
                "Refresh",
                Refresh,
                "Rescan the current MIDI location.",
                invoked: refreshInvoked);

            if (compact)
                ImGui.NewLine();
            else
                ImGui.SameLine();
            ImGui.SetNextItemWidth(Math.Max(100f, ImGui.GetContentRegionAvail().X));
            var hint = _navigation.View == MidiBrowserView.AllMidiFiles
                ? "Search all MIDI files..."
                : "Search this page...";
            ImGui.InputTextWithHint(
                $"Search {FontAwesome6.MagnifyingGlass}",
                hint,
                ref _searchBuffer,
                1000);
            ImGuiAccessibility.Edit(
                $"{BrowserId}.search",
                "Search MIDI Sources",
                _searchBuffer,
                value => _searchBuffer = value.Length <= 1000 ? value : value[..1000],
                _navigation.View == MidiBrowserView.AllMidiFiles
                    ? "Search every discovered MIDI Source by filename or folder."
                    : "Filter the entries on the current page.");
        }
        ImGui.EndChild();
    }

    private void RenderBrowser()
    {
        if (CoreSettings.AnimatedBackground && AccessibilityRuntime.Presentation.AllowDecorativeMotion)
            Drawings.RenderMatrixBackground();

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
                ImGui.Text($"{FontAwesome6.Folder} Play MIDI File");
                ImGui.Spacing();
                RenderBreadcrumbs();
                RenderSearchBar();
                ImGui.Separator();
                RenderScanStatus();
                RenderEntryList();
            }
            ImGui.EndChild();
        }

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private void RenderBreadcrumbs()
    {
        if (_navigation.View == MidiBrowserView.SearchPaths)
        {
            ImGui.TextUnformatted("MIDI Paths");
            ImGuiAccessibility.Text(
                $"{BrowserId}.location",
                "Current MIDI location",
                "MIDI Paths");
            return;
        }

        RenderBreadcrumbButton("MIDI Paths", "paths", NavigateToSearchPaths);
        ImGui.SameLine();
        ImGui.TextUnformatted(">");
        ImGui.SameLine();

        if (_navigation.View == MidiBrowserView.AllMidiFiles)
        {
            ImGui.TextUnformatted("All MIDI Files");
            ImGuiAccessibility.Text(
                $"{BrowserId}.location",
                "Current MIDI location",
                "MIDI Paths, All MIDI Files");
            return;
        }

        var searchPath = _navigation.SearchPath!;
        var currentDirectory = _navigation.CurrentDirectory!;
        var searchPathName = DisplayFolderName(searchPath);
        var relative = Path.GetRelativePath(searchPath, currentDirectory);
        var locationParts = new List<string> { "MIDI Paths", searchPathName };
        if (relative == ".")
        {
            ImGui.TextUnformatted(searchPathName);
        }
        else
        {
            RenderBreadcrumbButton(searchPathName, searchPath, () => NavigateToDirectory(searchPath));
            var accumulated = searchPath;
            foreach (var segment in relative.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                accumulated = Path.Combine(accumulated, segment);
                var target = accumulated;
                locationParts.Add(segment);
                ImGui.SameLine();
                ImGui.TextUnformatted(">");
                ImGui.SameLine();
                if (string.Equals(target, currentDirectory, StringComparison.OrdinalIgnoreCase))
                    ImGui.TextUnformatted(segment);
                else
                    RenderBreadcrumbButton(segment, target, () => NavigateToDirectory(target));
            }
        }

        ImGuiAccessibility.Text(
            $"{BrowserId}.location",
            "Current MIDI location",
            string.Join(", ", locationParts));
    }

    private void RenderBreadcrumbButton(string label, string key, Action action)
    {
        ImGui.PushID(key);
        var invoked = ImGui.SmallButton(label);
        if (invoked)
            action();
        ImGuiAccessibility.Button(
            $"{BrowserId}.breadcrumb.{ImGuiAccessibility.StableId("path", key)}",
            label,
            action,
            "Open this MIDI folder level.",
            invoked: invoked);
        ImGui.PopID();
    }

    private void RenderScanStatus()
    {
        EnsureScan();
        var desiredKey = GetScanRequest().Key;
        var scanning = _scanTask is not null && _scanKey == desiredKey;
        if (scanning)
        {
            var progress = $"Scanning… {_scanProgress.SourcesFound} MIDI files found";
            if (_scanProgress.LocationsSkipped > 0)
                progress += $" · {_scanProgress.LocationsSkipped} locations skipped";
            ImGui.TextDisabled(progress);
            ImGuiAccessibility.Text(
                $"{BrowserId}.scan-status",
                "MIDI scan status",
                progress,
                liveSetting: AccessibilityLiveSetting.Polite);
        }

        if (_scanResultKey == desiredKey && _scanResult.Issues.Count > 0)
        {
            var skipped = $"Skipped {_scanResult.Issues.Count} unavailable or inaccessible location" +
                          (_scanResult.Issues.Count == 1 ? "." : "s.");
            ImGui.TextDisabled(skipped);
            ImGuiAccessibility.Text(
                $"{BrowserId}.scan-warning",
                "MIDI scan warning",
                skipped,
                liveSetting: AccessibilityLiveSetting.Polite);
        }

        if (_statusMessage is not null)
        {
            ImGuiUtils.TextWrappedUnformatted(_statusMessage);
            ImGuiAccessibility.Text(
                $"{BrowserId}.status",
                "MIDI Source status",
                _statusMessage,
                liveSetting: AccessibilityLiveSetting.Assertive);
        }
    }

    private void RenderEntryList()
    {
        var sourceListPosition = ImGui.GetCursorScreenPos();
        var sourceListSize = ImGui.GetContentRegionAvail();
        UiAutomationRuntime.Coordinator.Register(
            new AccessibilityNode(
                SourcesListId,
                BrowserId,
                AccessibilityRole.List,
                EntryListName())
            {
                Bounds = new AccessibilityBounds(
                    sourceListPosition.X,
                    sourceListPosition.Y,
                    sourceListSize.X,
                    sourceListSize.Y)
            });

        if (!ImGui.BeginChild("MIDI source entries", sourceListSize))
        {
            ImGui.EndChild();
            return;
        }

        var entries = CurrentEntries();
        for (var index = 0; index < entries.Count; index++)
            RenderEntry(entries[index], focusFirst: index == 0 && _focusFirstEntry);

        var desiredKey = GetScanRequest().Key;
        var waitingForFirstScan = _scanResultKey != desiredKey;
        if (entries.Count == 0 && !waitingForFirstScan)
        {
            var emptyMessage = string.IsNullOrWhiteSpace(_searchBuffer)
                ? "No MIDI files found here."
                : "No matching MIDI files or folders.";
            ImGui.TextDisabled(emptyMessage);
            ImGuiAccessibility.Text(
                $"{BrowserId}.empty",
                "MIDI Source results",
                emptyMessage);
        }

        ImGui.EndChild();
    }

    private IReadOnlyList<MidiBrowserEntry> CurrentEntries()
    {
        if (_navigation.View == MidiBrowserView.SearchPaths)
            return SearchPathEntries();

        var desiredKey = GetScanRequest().Key;
        if (_scanResultKey != desiredKey)
            return Array.Empty<MidiBrowserEntry>();

        return _navigation.View == MidiBrowserView.AllMidiFiles
            ? MidiBrowserEntries.BuildAllMidiFiles(_scanResult, _searchBuffer, _alphabeticOrder)
            : MidiBrowserEntries.BuildDirectory(
                _scanResult,
                _navigation.CurrentDirectory!,
                _searchBuffer,
                _alphabeticOrder);
    }

    private IReadOnlyList<MidiBrowserEntry> SearchPathEntries()
    {
        var entries = new List<MidiBrowserEntry>();
        var allEntry = new MidiBrowserEntry(
            MidiBrowserEntryKind.AllMidiFiles,
            MidiBrowserWindowKeys.AllMidiFiles,
            "All MIDI Files",
            "Every MIDI file in every configured path",
            null);
        if (MatchesSearch(allEntry))
            entries.Add(allEntry);

        var desiredKey = GetScanRequest().Key;
        var hasScanResult = _scanResultKey == desiredKey;
        foreach (var path in MidiPathsManager.GetDistinctPaths())
        {
            var unavailable = hasScanResult && _scanResult.IsSearchPathUnavailable(path);
            var entry = new MidiBrowserEntry(
                MidiBrowserEntryKind.SearchPath,
                path,
                DisplayFolderName(path),
                unavailable ? $"{path} · Unavailable" : path,
                path,
                IsEnabled: !unavailable);
            if (MatchesSearch(entry))
                entries.Add(entry);
        }
        return entries;
    }

    private bool MatchesSearch(MidiBrowserEntry entry)
    {
        return string.IsNullOrWhiteSpace(_searchBuffer) ||
               entry.Name.Contains(_searchBuffer, StringComparison.OrdinalIgnoreCase) ||
               entry.Subtext?.Contains(_searchBuffer, StringComparison.OrdinalIgnoreCase) == true;
    }

    private void RenderEntry(MidiBrowserEntry entry, bool focusFirst)
    {
        var sourceId = ImGuiAccessibility.StableId(
            $"{BrowserId}.{entry.Kind.ToString().ToLowerInvariant()}",
            entry.Key);
        if (focusFirst || string.Equals(_pendingFocusKey, entry.Key, StringComparison.OrdinalIgnoreCase))
        {
            ImGui.SetKeyboardFocusHere();
            UiAutomationRuntime.Coordinator.RequestFocus(sourceId);
            _pendingFocusKey = null;
            _focusFirstEntry = false;
        }

        var hasSubtext = !string.IsNullOrWhiteSpace(entry.Subtext);
        var rowHeight = hasSubtext
            ? ImGuiUtils.FixedSize(new Vector2(58)).Y
            : ImGuiUtils.FixedSize(new Vector2(40)).Y;
        var rowStart = ImGui.GetCursorScreenPos();
        ImGui.PushID(entry.Key);
        ImGui.BeginDisabled(!entry.IsEnabled);
        var selected = string.Equals(_selectedEntryKey, entry.Key, StringComparison.OrdinalIgnoreCase);
        var pressed = ImGui.Selectable(
            "##entry",
            selected,
            ImGuiSelectableFlags.AllowDoubleClick,
            new Vector2(Math.Max(1f, ImGui.GetContentRegionAvail().X), rowHeight));
        var hovered = ImGui.IsItemHovered();
        var mouseClicked = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left, false);
        if (mouseClicked)
            _selectedEntryKey = entry.Key;
        var opened = pressed && (!mouseClicked || ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left));

        var icon = entry.Kind switch
        {
            MidiBrowserEntryKind.MidiSource => FontAwesome6.FileAudio,
            MidiBrowserEntryKind.AllMidiFiles => FontAwesome6.CompactDisc,
            _ => FontAwesome6.Folder
        };
        var nameColor = ImGui.GetColorU32(entry.IsEnabled ? ImGuiCol.Text : ImGuiCol.TextDisabled);
        var padding = ImGuiUtils.FixedSize(new Vector2(10)).X;
        ImGui.GetWindowDrawList().AddText(
            rowStart + new Vector2(padding, ImGuiUtils.FixedSize(new Vector2(7)).Y),
            nameColor,
            $"{icon} {entry.Name}");
        if (hasSubtext)
        {
            using AutoFont subtextFont = new(FontController.GetFontOfSize(16));
            ImGui.GetWindowDrawList().AddText(
                rowStart + new Vector2(padding, ImGuiUtils.FixedSize(new Vector2(33)).Y),
                ImGui.GetColorU32(ImGuiCol.TextDisabled),
                entry.Subtext!);
        }

        ImGuiAccessibility.RegisterLastItem(
            new AccessibilityNode(
                sourceId,
                SourcesListId,
                AccessibilityRole.ListItem,
                entry.Name)
            {
                Description = EntryDescription(entry),
                Value = entry.Subtext,
                IsEnabled = entry.IsEnabled,
                IsFocusable = entry.IsEnabled,
                IsSelected = selected,
                SupportedActions = entry.IsEnabled
                    ? AccessibilityAction.Invoke | AccessibilityAction.Focus
                    : AccessibilityAction.None
            },
            entry.IsEnabled ? _ => OpenEntry(entry) : null);
        ImGui.EndDisabled();
        ImGui.PopID();

        if (opened && entry.IsEnabled)
        {
            OpenEntry(entry);
            UiAutomationRuntime.NotifyActionCompleted(sourceId, AccessibilityAction.Invoke);
        }
    }

    private void OpenEntry(MidiBrowserEntry entry)
    {
        _statusMessage = null;
        _searchBuffer = string.Empty;
        switch (entry.Kind)
        {
            case MidiBrowserEntryKind.AllMidiFiles:
                _navigation.OpenAllMidiFiles();
                _pendingFocusKey = null;
                _focusFirstEntry = true;
                RequestRefresh(clearExistingResults: true);
                break;
            case MidiBrowserEntryKind.SearchPath:
                _navigation.OpenSearchPath(entry.FullPath!);
                _pendingFocusKey = null;
                _focusFirstEntry = true;
                RequestRefresh(clearExistingResults: true);
                break;
            case MidiBrowserEntryKind.Folder:
                _navigation.OpenDirectory(entry.FullPath!);
                _pendingFocusKey = null;
                _focusFirstEntry = true;
                RequestRefresh(clearExistingResults: true);
                break;
            case MidiBrowserEntryKind.MidiSource:
                if (_navigation.View == MidiBrowserView.AllMidiFiles)
                    _navigation.ReturnToSourceDirectory(entry.Source!);
                _pendingFocusKey = entry.FullPath;
                _focusFirstEntry = false;
                OpenMidiSource(entry.FullPath!);
                break;
        }
    }

    private void OpenMidiSource(string file)
    {
        try
        {
            MidiFileHandler.LoadMidiFile(file);
            MidiPlayer.Playback.Start();
            MidiPlayer.Playback.Stop();
            WindowsManager.SetWindow(Enums.Windows.ModeSelection);
        }
        catch (Exception exception)
        {
            _statusMessage = $"Couldn't open {Path.GetFileName(file)}: {exception.Message}";
            User32.MessageBox(
                IntPtr.Zero,
                _statusMessage,
                "Couldn't open MIDI file",
                User32.MB_FLAGS.MB_ICONERROR | User32.MB_FLAGS.MB_TOPMOST);
        }
    }

    private void RenderTopControls()
    {
        var margin = Math.Max(8f, Math.Min(ImGuiUtils.FixedSize(new Vector2(22)).X, _io.DisplaySize.X * 0.04f));
        var gap = ImGuiUtils.FixedSize(new Vector2(10)).X;
        var buttonWidth = Math.Min(
            ImGuiUtils.FixedSize(new Vector2(150)).X,
            Math.Max(100f, (_io.DisplaySize.X - margin * 2 - gap) / 2));
        var buttonHeight = ImGuiUtils.FixedSize(new Vector2(50)).Y;
        ImGui.SetCursorScreenPos(new Vector2(margin, Math.Min(buttonHeight, _io.DisplaySize.Y * 0.08f)));

        var backInvoked = ImGui.Button(
                              $"{FontAwesome6.ArrowLeftLong} Back",
                              new Vector2(buttonWidth, buttonHeight)) ||
                          KeyboardBackRequested();
        if (backInvoked)
            GoBack();
        ImGuiAccessibility.Button(
            $"{BrowserId}.back",
            "Back",
            GoBack,
            BackDescription(),
            invoked: backInvoked);

        ImGuiTheme.PushButton(
            ImGuiTheme.HtmlToVec4("#0EA5E9"),
            ImGuiTheme.HtmlToVec4("#096E9B"),
            ImGuiTheme.HtmlToVec4("#0EA5E9"));
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
            $"{BrowserId}.open-file",
            "Open MIDI Source",
            OpenFile,
            "Choose a MIDI Source from the Windows file dialog.",
            invoked: openFileInvoked);
        ImGuiTheme.PopButton();
    }

    private void GoBack()
    {
        var result = _navigation.Back();
        _searchBuffer = string.Empty;
        _selectedEntryKey = null;
        if (result.ReturnHome)
        {
            WindowsManager.SetWindow(Enums.Windows.Home);
            return;
        }

        _pendingFocusKey = result.FocusPath;
        RequestRefresh(clearExistingResults: true);
    }

    private void NavigateToSearchPaths()
    {
        _pendingFocusKey = _navigation.View == MidiBrowserView.AllMidiFiles
            ? MidiBrowserWindowKeys.AllMidiFiles
            : _navigation.SearchPath;
        _navigation.Reset();
        _searchBuffer = string.Empty;
        RequestRefresh(clearExistingResults: true);
    }

    private void NavigateToDirectory(string path)
    {
        var previousDirectory = _navigation.CurrentDirectory;
        _navigation.OpenDirectory(path);
        _pendingFocusKey = ImmediateChild(path, previousDirectory);
        _searchBuffer = string.Empty;
        RequestRefresh(clearExistingResults: true);
    }

    private bool KeyboardBackRequested()
    {
        if (ImGui.GetIO().WantTextInput ||
            ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId))
        {
            return false;
        }

        return ImGui.IsKeyPressed(ImGuiKey.Escape, false) ||
               ImGui.IsKeyPressed(ImGuiKey.Backspace, false) ||
               (ImGui.GetIO().KeyAlt && ImGui.IsKeyPressed(ImGuiKey.LeftArrow, false));
    }

    private void EnsureScan()
    {
        PollScan();
        var request = GetScanRequest();
        if (_scanResultKey == request.Key || (_scanTask is not null && _scanKey == request.Key))
            return;
        StartScan(request, clearExistingResults: true);
    }

    private void RequestRefresh(bool clearExistingResults)
    {
        var request = GetScanRequest();
        StartScan(request, clearExistingResults);
    }

    private void StartScan(ScanRequest request, bool clearExistingResults)
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        _scanProgress = new MidiDiscoveryProgress();
        _scanKey = request.Key;
        if (clearExistingResults)
            _scanResultKey = null;
        _scanTask = MidiSourceDiscovery.DiscoverAsync(
            request.Paths,
            _scanProgress,
            _scanCancellation.Token);
    }

    private void PollScan()
    {
        if (_scanTask is null || !_scanTask.IsCompleted)
            return;

        if (_scanTask.IsCompletedSuccessfully)
        {
            _scanResult = _scanTask.Result;
            _scanResultKey = _scanKey;
        }
        else if (_scanTask.IsFaulted)
        {
            var message = _scanTask.Exception?.GetBaseException().Message ?? "The MIDI scan failed.";
            _scanResult = new MidiDiscoveryResult(
                Array.Empty<MidiSourceEntry>(),
                new[] { new MidiDiscoveryIssue(string.Empty, message, IsSearchPath: false) });
            _scanResultKey = _scanKey;
        }

        _scanTask = null;
    }

    private ScanRequest GetScanRequest()
    {
        var paths = _navigation.View == MidiBrowserView.Directory
            ? new[] { _navigation.CurrentDirectory! }
            : MidiPathsManager.GetDistinctPaths();
        var pathKey = string.Join('\u001f', paths);
        var keyPrefix = _navigation.View == MidiBrowserView.Directory ? "directory" : "all";
        return new ScanRequest($"{keyPrefix}|{pathKey}", paths);
    }

    private string EntryListName()
    {
        return _navigation.View switch
        {
            MidiBrowserView.SearchPaths => "MIDI Search Paths",
            MidiBrowserView.AllMidiFiles => "All MIDI Files",
            _ => $"MIDI folders and files in {DisplayFolderName(_navigation.CurrentDirectory!)}"
        };
    }

    private static string EntryDescription(MidiBrowserEntry entry)
    {
        return entry.Kind switch
        {
            MidiBrowserEntryKind.AllMidiFiles =>
                "Open every distinct MIDI file discovered beneath all configured MIDI Search Paths.",
            MidiBrowserEntryKind.SearchPath => entry.IsEnabled
                ? "Open this configured MIDI Search Path."
                : "This configured MIDI Search Path is unavailable.",
            MidiBrowserEntryKind.Folder => "Open this folder.",
            _ => "Open this MIDI Source and continue to its Chart setup."
        };
    }

    private string BackDescription()
    {
        return _navigation.View switch
        {
            MidiBrowserView.SearchPaths => "Return to Home.",
            MidiBrowserView.AllMidiFiles => "Return to MIDI Paths.",
            _ when string.Equals(
                _navigation.CurrentDirectory,
                _navigation.SearchPath,
                StringComparison.OrdinalIgnoreCase) => "Return to MIDI Paths.",
            _ => "Return to the previous MIDI folder."
        };
    }

    private static string DisplayFolderName(string path)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    private static string? ImmediateChild(string parent, string? descendant)
    {
        if (descendant is null)
            return null;
        var relative = Path.GetRelativePath(parent, descendant);
        if (relative == "." || Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
            return null;
        var firstSegment = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstSegment is null
            ? null
            : MidiPathsManager.NormalizePath(Path.Combine(parent, firstSegment));
    }

    protected override void OnImGui()
    {
        using (AutoFont font16Icon16 = new(FontController.Font16_Icon16))
            RenderTopControls();
        RenderBrowser();
    }

    private sealed record ScanRequest(string Key, IReadOnlyList<string> Paths);
}
