using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Engine.Render;

/// <summary>
/// Owns the OS window and the OpenGL context — the outer shell of the render
/// loop. Consumers subscribe to Load/Update/Render and never touch Silk's
/// window directly except through <see cref="Window"/> (e.g. to create an
/// input context).
/// </summary>
public sealed class GameWindow : IDisposable
{
    private readonly IWindow _window;
    private GL? _gl;

    public IWindow Window => _window;

    /// <summary>Valid from the Load event onward.</summary>
    public GL Gl => _gl ?? throw new InvalidOperationException("GL is not available before Load fires.");

    public event Action? Load;
    public event Action<double>? Update;
    public event Action<double>? Render;
    public event Action<Vector2D<int>>? Resize;

    public GameWindow(string title, int width, int height)
    {
        var options = WindowOptions.Default with
        {
            Title = title,
            Size = new Vector2D<int>(width, height),
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
            VSync = true,
        };

        _window = Silk.NET.Windowing.Window.Create(options);
        _window.Load += OnWindowLoad;
        _window.Update += dt => Update?.Invoke(dt);
        _window.Render += dt => Render?.Invoke(dt);
        _window.FramebufferResize += size =>
        {
            _gl?.Viewport(size);
            Resize?.Invoke(size);
        };
    }

    private void OnWindowLoad()
    {
        _gl = GL.GetApi(_window);
        Load?.Invoke();
    }

    /// <summary>Blocks until the window closes.</summary>
    public void Run() => _window.Run();

    public void Close() => _window.Close();

    public void Dispose()
    {
        _gl?.Dispose();
        _window.Dispose();
    }
}
