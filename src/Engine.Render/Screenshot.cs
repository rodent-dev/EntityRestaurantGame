using Silk.NET.OpenGL;

namespace Engine.Render;

/// <summary>
/// Debug helper: dump the current framebuffer to a .bmp file. Used by smoke
/// tests to verify rendering without a human looking at the window.
/// </summary>
public static class Screenshot
{
    public static unsafe void Capture(GL gl, int width, int height, string path)
    {
        int rowBytes = width * 3;
        int paddedRow = (rowBytes + 3) & ~3; // BMP rows pad to 4 bytes
        byte[] pixels = new byte[rowBytes * height];

        gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        fixed (byte* p = pixels)
        {
            // BGR matches BMP byte order; GL reads bottom-up, BMP stores bottom-up — no flip needed.
            gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Bgr, PixelType.UnsignedByte, p);
        }

        int dataSize = paddedRow * height;
        using var w = new BinaryWriter(File.Create(path));
        // BITMAPFILEHEADER
        w.Write((byte)'B'); w.Write((byte)'M');
        w.Write(54 + dataSize);
        w.Write(0);
        w.Write(54);
        // BITMAPINFOHEADER
        w.Write(40);
        w.Write(width);
        w.Write(height);
        w.Write((short)1);
        w.Write((short)24);
        w.Write(0);
        w.Write(dataSize);
        w.Write(2835); w.Write(2835); // 72 dpi
        w.Write(0); w.Write(0);

        byte[] pad = new byte[paddedRow - rowBytes];
        for (int y = 0; y < height; y++)
        {
            w.Write(pixels, y * rowBytes, rowBytes);
            if (pad.Length > 0) w.Write(pad);
        }
    }
}
