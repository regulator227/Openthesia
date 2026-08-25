using Veldrid.Sdl2;
using Veldrid;
using System.Diagnostics;
using Veldrid.StartupUtilities;
using System.Numerics;
using ImGuiNET;
using Openthesia.Core;
using Openthesia.Core.Plugins;
using Openthesia.Settings;

namespace Openthesia;

class Program
{
    public static bool IsRunning = true;
    public static Sdl2Window _window;
    private static GraphicsDevice _gd;
    private static CommandList _cl;
    private static ImGuiController _controller;
    private static Vector3 _clearColor = new(0.45f, 0.55f, 0.6f);

    [STAThread]
    static void Main(string[] args)
    {
        WindowsAccessibilityAdapter.EnablePerMonitorV2();

        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, 1280, 720, WindowState.Maximized, $"Openthesia {ProgramData.ProgramVersion}"),
            new GraphicsDeviceOptions(false, null, true, ResourceBindingModel.Improved, true, true),
            out _window,
            out _gd);

        _cl = _gd.ResourceFactory.CreateCommandList();
        _controller = new ImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);

        _window.Resized += () =>
        {
            _gd.MainSwapchain.Resize(
                (uint)Math.Max(1, _window.Width),
                (uint)Math.Max(1, _window.Height));
            _controller.WindowResized(_window.Width, _window.Height);
        };

        var stopwatch = Stopwatch.StartNew();
        float deltaTime = 0f;

        ImGuiController.LoadImages(_gd, _controller);
        ProgramData.Initialize();
        AccessibilityRuntime.Update(_window.Handle);
        _controller.SetAccessibilityScale(AccessibilityRuntime.Presentation.UiScale);
        ImGuiTheme.PushTheme();

        Application app = new();

        while (_window.Exists)
        {
            deltaTime = stopwatch.ElapsedTicks / (float)Stopwatch.Frequency;
            stopwatch.Restart();
            InputSnapshot snapshot = _window.PumpEvents();
            if (!_window.Exists) { break; }
            var accessibilityChanged = AccessibilityRuntime.Update(_window.Handle);
            _controller.SetAccessibilityScale(AccessibilityRuntime.Presentation.UiScale);
            _controller.Update(deltaTime, snapshot);
            if (accessibilityChanged)
                ImGuiTheme.PushTheme();

            if (ImGui.IsKeyPressed(ImGuiKey.F11, false))
            {
                var windowsState = _window.WindowState == WindowState.BorderlessFullScreen 
                    ? WindowState.Normal 
                    : WindowState.BorderlessFullScreen;
                _window.WindowState = windowsState;
            }

            if (CoreSettings.SoundEngine == Enums.SoundEngine.Plugins)
            {
                if (VstPlayer.PluginsChain?.PluginInstrument is VstPlugin instrument)
                {
                    instrument.PluginWindow.PumpEvents();
                }
                foreach (var plugin in VstPlayer.PluginsChain.FxPlugins)
                {
                    if (plugin is VstPlugin plug)
                    {
                        plug.PluginWindow.PumpEvents();
                    }
                }
            }

            app.OnUpdate();
            if (!app.IsRunning())
            {
                break;
            }

            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
            _controller.Render(_gd, _cl);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }

        ProgramData.SaveSettings();

        _gd.WaitForIdle();
        _controller.Dispose();
        _cl.Dispose();
        _gd.Dispose();
        Process.GetCurrentProcess().Kill(); // temporary solution since process doesn't close when using ASIO4ALL
    }
}
