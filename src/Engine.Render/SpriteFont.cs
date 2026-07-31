using System.Numerics;
using Silk.NET.OpenGL;
using StbTrueTypeSharp;

namespace Engine.Render;

/// <summary>
/// A TTF baked to a glyph atlas at one fixed pixel size (ASCII 32–126).
/// Baked small and upscaled nearest with the rest of the canvas, a realistic
/// typeface comes out slightly pixely — the PS2-era text look.
/// </summary>
public sealed class SpriteFont : IDisposable
{
    /// <summary>Atlas UVs (0..1), pixel size, pen offsets from the baseline, and advance.</summary>
    public readonly record struct Glyph(
        float U, float V, float UWidth, float VHeight,
        float Width, float Height,
        float OffsetX, float OffsetY, float Advance);

    private readonly Dictionary<char, Glyph> _glyphs = [];

    public Texture2D Atlas { get; }
    public float PixelHeight { get; }
    /// <summary>Baseline distance from the top of a line.</summary>
    public float Ascent { get; }
    public float LineHeight { get; }

    private const int FirstChar = 32;
    private const int CharCount = 95; // ASCII 32..126

    public unsafe SpriteFont(GL gl, byte[] ttf, float pixelHeight)
    {
        PixelHeight = pixelHeight;
        int atlasSize = pixelHeight > 20 ? 512 : 256;
        var coverage = new byte[atlasSize * atlasSize];
        var baked = new StbTrueType.stbtt_bakedchar[CharCount];

        fixed (byte* ttfPtr = ttf)
        {
            fixed (byte* pixels = coverage)
            fixed (StbTrueType.stbtt_bakedchar* chars = baked)
            {
                int result = StbTrueType.stbtt_BakeFontBitmap(
                    ttfPtr, 0, pixelHeight, pixels, atlasSize, atlasSize, FirstChar, CharCount, chars);
                if (result == 0)
                    throw new InvalidOperationException("Font bake failed — not a valid TTF?");
            }

            var info = new StbTrueType.stbtt_fontinfo();
            if (StbTrueType.stbtt_InitFont(info, ttfPtr, 0) == 0)
                throw new InvalidOperationException("stbtt_InitFont rejected the font file.");
            float scale = StbTrueType.stbtt_ScaleForPixelHeight(info, pixelHeight);
            int ascent, descent, lineGap;
            StbTrueType.stbtt_GetFontVMetrics(info, &ascent, &descent, &lineGap);
            Ascent = ascent * scale;
            LineHeight = (ascent - descent + lineGap) * scale;
        }

        // Expand single-channel coverage to white-with-alpha so text renders
        // through the same shader as every other quad.
        var rgba = new byte[coverage.Length * 4];
        for (int i = 0; i < coverage.Length; i++)
        {
            rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = 255;
            rgba[i * 4 + 3] = coverage[i];
        }
        Atlas = new Texture2D(gl, rgba, atlasSize, atlasSize);

        for (int i = 0; i < CharCount; i++)
        {
            var b = baked[i];
            _glyphs[(char)(FirstChar + i)] = new Glyph(
                b.x0 / (float)atlasSize, b.y0 / (float)atlasSize,
                (b.x1 - b.x0) / (float)atlasSize, (b.y1 - b.y0) / (float)atlasSize,
                b.x1 - b.x0, b.y1 - b.y0,
                b.xoff, b.yoff, b.xadvance);
        }
    }

    /// <summary>Unknown characters fall back to a close ASCII stand-in, then '?'.</summary>
    public Glyph GetGlyph(char c)
    {
        if (_glyphs.TryGetValue(c, out var g)) return g;
        return _glyphs.TryGetValue(Approximate(c), out var near) ? near : _glyphs['?'];
    }

    /// <summary>The atlas bakes ASCII only; sim text may carry typographic
    /// characters (arrows, dashes, middots) that should still read.</summary>
    private static char Approximate(char c) => c switch
    {
        '→' => '>',
        '←' => '<',
        '—' or '–' => '-',
        '·' or '…' => '.',
        '‘' or '’' => '\'',
        '“' or '”' => '"',
        '×' => 'x',
        _ => '?',
    };

    /// <summary>Size of the text block; '\n' starts a new line.</summary>
    public Vector2 Measure(string text)
    {
        float widest = 0, lineWidth = 0;
        int lines = 1;
        foreach (char c in text)
        {
            if (c == '\n')
            {
                widest = MathF.Max(widest, lineWidth);
                lineWidth = 0;
                lines++;
                continue;
            }
            lineWidth += GetGlyph(c).Advance;
        }
        return new Vector2(MathF.Max(widest, lineWidth), lines * LineHeight);
    }

    public void Dispose() => Atlas.Dispose();
}
