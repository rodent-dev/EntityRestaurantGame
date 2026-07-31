using Silk.NET.OpenGL;
using StbImageSharp;

namespace Engine.Render;

/// <summary>
/// An RGBA image on the GPU. Nearest-filtered by default — UI and sprites
/// share the game's chunky upscale look. Load menu backgrounds from any
/// PNG/JPG/BMP/TGA via <see cref="FromFile"/>.
/// </summary>
public sealed class Texture2D : IDisposable
{
    private readonly GL _gl;

    public uint Handle { get; }
    public int Width { get; }
    public int Height { get; }

    public unsafe Texture2D(GL gl, byte[] rgba, int width, int height)
    {
        if (rgba.Length < width * height * 4)
            throw new ArgumentException($"Pixel buffer too small: {rgba.Length} bytes for {width}x{height} RGBA.", nameof(rgba));

        _gl = gl;
        Width = width;
        Height = height;

        Handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, Handle);
        fixed (byte* p = rgba)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public static Texture2D FromFile(GL gl, string path)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        return new Texture2D(gl, image.Data, image.Width, image.Height);
    }

    /// <summary>1×1 white texture — lets colored rects go through the same textured path.</summary>
    public static Texture2D White(GL gl) => new(gl, [255, 255, 255, 255], 1, 1);

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}
