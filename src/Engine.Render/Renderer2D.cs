using System.Numerics;
using Silk.NET.OpenGL;

namespace Engine.Render;

/// <summary>
/// Orthographic 2D pass over a virtual canvas (y-down, origin top-left).
/// Runs after the 3D pass into the same low-res render target, so all UI
/// inherits the chunky upscale. One unit quad + per-draw uniforms — immediate
/// mode, which is plenty at menu scale.
/// </summary>
public sealed class Renderer2D : IDisposable
{
    private const string VertexSrc = """
        #version 330 core
        layout(location = 0) in vec2 aPos;   // unit quad corner (0..1)
        uniform vec4 uDest;                  // x, y, w, h in canvas pixels
        uniform vec4 uSrc;                   // u, v, uw, vh in texture space
        uniform mat4 uProj;
        out vec2 vUv;
        void main()
        {
            vec2 pos = uDest.xy + aPos * uDest.zw;
            vUv = uSrc.xy + aPos * uSrc.zw;
            gl_Position = uProj * vec4(pos, 0.0, 1.0);
        }
        """;

    private const string FragmentSrc = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uTex;
        uniform vec4 uColor;
        out vec4 FragColor;
        void main() { FragColor = texture(uTex, vUv) * uColor; }
        """;

    private readonly GL _gl;
    private readonly Shader _shader;
    private readonly uint _vao, _vbo, _ebo;
    private readonly Texture2D _white;
    private bool _depthWasOn, _cullWasOn;

    public unsafe Renderer2D(GL gl)
    {
        _gl = gl;
        _shader = new Shader(gl, VertexSrc, FragmentSrc);
        _white = Texture2D.White(gl);

        float[] corners = [0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f];
        uint[] indices = [0, 1, 2, 2, 3, 0];

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);
        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferData<float>(BufferTargetARB.ArrayBuffer, corners, BufferUsageARB.StaticDraw);
        _ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        gl.BindVertexArray(0);
    }

    /// <summary>Enter 2D state. Pair with <see cref="End"/>; 3D state is restored there.</summary>
    public void Begin(int canvasWidth, int canvasHeight)
    {
        _depthWasOn = _gl.IsEnabled(EnableCap.DepthTest);
        _cullWasOn = _gl.IsEnabled(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _shader.Use();
        _shader.Set("uProj", Matrix4x4.CreateOrthographicOffCenter(0, canvasWidth, canvasHeight, 0, -1, 1));
        _gl.BindVertexArray(_vao);
    }

    public void End()
    {
        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.Blend);
        if (_depthWasOn) _gl.Enable(EnableCap.DepthTest);
        if (_cullWasOn) _gl.Enable(EnableCap.CullFace);
    }

    public void DrawRect(float x, float y, float w, float h, Vector4 color) =>
        Draw(_white, x, y, w, h, 0, 0, 1, 1, color);

    /// <summary>1px-thick rectangle outline.</summary>
    public void DrawRectOutline(float x, float y, float w, float h, Vector4 color)
    {
        DrawRect(x, y, w, 1, color);
        DrawRect(x, y + h - 1, w, 1, color);
        DrawRect(x, y + 1, 1, h - 2, color);
        DrawRect(x + w - 1, y + 1, 1, h - 2, color);
    }

    public void DrawTexture(Texture2D texture, float x, float y, float w, float h, Vector4? tint = null) =>
        Draw(texture, x, y, w, h, 0, 0, 1, 1, tint ?? Vector4.One);

    /// <summary>Draw a sub-rectangle of a texture (source in pixels) — for atlases and nine-patches.</summary>
    public void DrawTextureRegion(Texture2D texture, float x, float y, float w, float h,
        float srcX, float srcY, float srcW, float srcH, Vector4? tint = null) =>
        Draw(texture, x, y, w, h,
            srcX / texture.Width, srcY / texture.Height,
            srcW / texture.Width, srcH / texture.Height,
            tint ?? Vector4.One);

    public void DrawText(SpriteFont font, string text, float x, float y, Vector4 color)
    {
        float penX = x;
        float baseline = y + font.Ascent;
        foreach (char c in text)
        {
            if (c == '\n')
            {
                penX = x;
                baseline += font.LineHeight;
                continue;
            }
            var g = font.GetGlyph(c);
            if (g.Width > 0)
                Draw(font.Atlas, penX + g.OffsetX, baseline + g.OffsetY, g.Width, g.Height,
                    g.U, g.V, g.UWidth, g.VHeight, color);
            penX += g.Advance;
        }
    }

    /// <summary>Draw text with its horizontal center at <paramref name="centerX"/>.</summary>
    public void DrawTextCentered(SpriteFont font, string text, float centerX, float y, Vector4 color) =>
        DrawText(font, text, centerX - font.Measure(text).X / 2f, y, color);

    private unsafe void Draw(Texture2D texture, float x, float y, float w, float h,
        float u, float v, float uw, float vh, Vector4 color)
    {
        texture.Bind();
        _shader.Set("uDest", new Vector4(x, y, w, h));
        _shader.Set("uSrc", new Vector4(u, v, uw, vh));
        _shader.Set("uColor", color);
        _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
        _white.Dispose();
        _shader.Dispose();
    }
}
