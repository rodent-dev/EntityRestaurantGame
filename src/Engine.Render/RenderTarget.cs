using Silk.NET.OpenGL;

namespace Engine.Render;

/// <summary>
/// Offscreen framebuffer with a nearest-filtered color texture and a depth
/// buffer. Render the scene into it at a low resolution, then blit upscaled
/// to the window — the PS2-era pixel look in two calls.
/// </summary>
public sealed class RenderTarget : IDisposable
{
    private readonly GL _gl;
    private readonly uint _fbo;
    private readonly uint _color;
    private readonly uint _depth;

    public int Width { get; }
    public int Height { get; }

    public unsafe RenderTarget(GL gl, int width, int height)
    {
        _gl = gl;
        Width = width;
        Height = height;

        _fbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

        _color = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _color);
        gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgb8,
            (uint)width, (uint)height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _color, 0);

        _depth = gl.GenRenderbuffer();
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depth);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)width, (uint)height);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer, _depth);

        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"Framebuffer incomplete: {status}");

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>Route subsequent draws into this target.</summary>
    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    /// <summary>Upscale-blit this target onto the window's framebuffer (nearest — crunchy pixels preserved).</summary>
    public void BlitToScreen(int windowWidth, int windowHeight)
    {
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, windowWidth, windowHeight,
            (uint)ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)windowWidth, (uint)windowHeight);
    }

    public void Dispose()
    {
        _gl.DeleteFramebuffer(_fbo);
        _gl.DeleteTexture(_color);
        _gl.DeleteRenderbuffer(_depth);
    }
}
