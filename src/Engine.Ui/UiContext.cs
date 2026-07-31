using Engine.Render;
using Silk.NET.OpenGL;

namespace Engine.Ui;

/// <summary>
/// Everything a scene needs to draw itself: the 2D renderer, the game fonts,
/// the canvas size, and a texture cache rooted at the UI asset folder
/// (data/ui). A missing image returns null — scenes fall back to plain rects,
/// they never crash over art.
/// </summary>
public sealed class UiContext
{
    public GL Gl { get; }
    public Renderer2D Renderer { get; }
    public SpriteFont Font { get; }
    public SpriteFont HeadingFont { get; }
    /// <summary>Virtual canvas size (the low-res render target, not the window).</summary>
    public int Width { get; }
    public int Height { get; }

    private readonly string _assetRoot;
    private readonly Dictionary<string, Texture2D?> _textures = [];

    public UiContext(GL gl, Renderer2D renderer, SpriteFont font, SpriteFont headingFont,
        int width, int height, string assetRoot)
    {
        Gl = gl;
        Renderer = renderer;
        Font = font;
        HeadingFont = headingFont;
        Width = width;
        Height = height;
        _assetRoot = assetRoot;
    }

    /// <summary>Load (and cache) an image from the UI asset folder; null if the file doesn't exist.</summary>
    public Texture2D? TryLoadTexture(string fileName)
    {
        if (_textures.TryGetValue(fileName, out var cached))
            return cached;

        string path = Path.Combine(_assetRoot, fileName);
        var texture = File.Exists(path) ? Texture2D.FromFile(Gl, path) : null;
        _textures[fileName] = texture;
        return texture;
    }
}
