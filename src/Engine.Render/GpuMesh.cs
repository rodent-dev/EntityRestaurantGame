using Silk.NET.OpenGL;

namespace Engine.Render;

/// <summary>
/// A MeshData uploaded to the GPU: VAO describing the 10-float vertex layout,
/// VBO with the interleaved vertices, EBO with the indices.
/// </summary>
public sealed class GpuMesh : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao, _vbo, _ebo;
    private readonly uint _indexCount;
    private readonly PrimitiveType _mode;

    public unsafe GpuMesh(GL gl, MeshData data)
    {
        _gl = gl;
        _indexCount = (uint)data.Indices.Length;
        _mode = data.Topology == MeshTopology.Lines ? PrimitiveType.Lines : PrimitiveType.Triangles;

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);

        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.BufferData<float>(BufferTargetARB.ArrayBuffer, data.Vertices, BufferUsageARB.StaticDraw);

        _ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, data.Indices, BufferUsageARB.StaticDraw);

        // Layout: location 0 = position vec3, 1 = normal vec3, 2 = color vec4.
        uint stride = MeshData.VertexBytes;
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));

        gl.BindVertexArray(0);
    }

    public unsafe void Draw()
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(_mode, _indexCount, DrawElementsType.UnsignedInt, (void*)0);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
    }
}
