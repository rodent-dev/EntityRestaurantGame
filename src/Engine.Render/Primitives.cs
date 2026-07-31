using System.Numerics;

namespace Engine.Render;

public enum MeshTopology
{
    Triangles,
    Lines,
}

/// <summary>
/// CPU-side mesh: interleaved vertices of 10 floats each —
/// position x,y,z · normal nx,ny,nz · color r,g,b,a — plus an index list.
/// </summary>
public readonly record struct MeshData(float[] Vertices, uint[] Indices, MeshTopology Topology)
{
    public const int VertexFloats = 10;
    public const int VertexBytes = VertexFloats * sizeof(float);
}

/// <summary>
/// Procedural mesh builders. All geometry is centered at the origin —
/// world placement is the model matrix's job at draw time.
/// Triangles wind counter-clockwise when viewed from the side their normal points at.
/// </summary>
public static class Primitives
{
    private static void AppendVertex(List<float> pool, Vector3 pos, Vector3 norm, Vector4 col)
    {
        pool.Add(pos.X); pool.Add(pos.Y); pool.Add(pos.Z);
        pool.Add(norm.X); pool.Add(norm.Y); pool.Add(norm.Z);
        pool.Add(col.X); pool.Add(col.Y); pool.Add(col.Z); pool.Add(col.W);
    }

    public static MeshData Triangle(Vector3 a, Vector3 b, Vector3 c, Vector4 color)
    {
        var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        var verts = new List<float>();
        AppendVertex(verts, a, normal, color);
        AppendVertex(verts, b, normal, color);
        AppendVertex(verts, c, normal, color);
        return new MeshData([.. verts], [0, 1, 2], MeshTopology.Triangles);
    }

    /// <summary>Flat quad in the XY plane facing +Z, centered at the origin.</summary>
    public static MeshData Pane(float width, float height, Vector4 color)
    {
        float hw = width / 2f, hh = height / 2f;
        var n = new Vector3(0, 0, 1);
        var verts = new List<float>();
        AppendVertex(verts, new Vector3(-hw, -hh, 0), n, color); // 0 bottom-left
        AppendVertex(verts, new Vector3( hw, -hh, 0), n, color); // 1 bottom-right
        AppendVertex(verts, new Vector3( hw,  hh, 0), n, color); // 2 top-right
        AppendVertex(verts, new Vector3(-hw,  hh, 0), n, color); // 3 top-left
        return new MeshData([.. verts], [0, 1, 2, 2, 3, 0], MeshTopology.Triangles);
    }

    /// <summary>Box centered at the origin. 24 vertices — 4 per face so each face carries its own outward normal.</summary>
    public static MeshData Cube(Vector3 size, Vector4 color)
    {
        var verts = new List<float>();
        var indices = new List<uint>();
        float x = size.X / 2f, y = size.Y / 2f, z = size.Z / 2f;

        // Face corners are listed CCW as seen from outside; the normal is
        // derived from the first three, so a wrong order here is self-revealing
        // (the face disappears under backface culling).
        void AddFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            uint b = (uint)(verts.Count / MeshData.VertexFloats);
            var normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v3 - v0));
            AppendVertex(verts, v0, normal, color);
            AppendVertex(verts, v1, normal, color);
            AppendVertex(verts, v2, normal, color);
            AppendVertex(verts, v3, normal, color);
            indices.AddRange([b, b + 1, b + 2, b, b + 2, b + 3]);
        }

        AddFace(new(-x, -y,  z), new( x, -y,  z), new( x,  y,  z), new(-x,  y,  z)); // +Z
        AddFace(new( x,  y, -z), new( x, -y, -z), new(-x, -y, -z), new(-x,  y, -z)); // -Z
        AddFace(new( x, -y, -z), new( x,  y, -z), new( x,  y,  z), new( x, -y,  z)); // +X
        AddFace(new(-x,  y, -z), new(-x, -y, -z), new(-x, -y,  z), new(-x,  y,  z)); // -X
        AddFace(new(-x,  y,  z), new( x,  y,  z), new( x,  y, -z), new(-x,  y, -z)); // +Y
        AddFace(new(-x, -y, -z), new( x, -y, -z), new( x, -y,  z), new(-x, -y,  z)); // -Y

        return new MeshData([.. verts], [.. indices], MeshTopology.Triangles);
    }

    /// <summary>Line grid on the XZ plane (y = 0), centered at the origin.</summary>
    public static MeshData Grid(int halfLines, float spacing, Vector4 color)
    {
        var verts = new List<float>();
        var indices = new List<uint>();
        var up = new Vector3(0, 1, 0);
        float extent = halfLines * spacing;
        uint idx = 0;

        for (int i = -halfLines; i <= halfLines; i++)
        {
            float p = i * spacing;
            AppendVertex(verts, new Vector3(-extent, 0, p), up, color); // parallel to X
            AppendVertex(verts, new Vector3( extent, 0, p), up, color);
            AppendVertex(verts, new Vector3(p, 0, -extent), up, color); // parallel to Z
            AppendVertex(verts, new Vector3(p, 0,  extent), up, color);
            indices.AddRange([idx, idx + 1, idx + 2, idx + 3]);
            idx += 4;
        }

        return new MeshData([.. verts], [.. indices], MeshTopology.Lines);
    }

    /// <summary>Debug gizmo: +X red, +Y green, +Z blue lines from the origin.</summary>
    public static MeshData Axes(float length)
    {
        var verts = new List<float>();
        var up = new Vector3(0, 1, 0);
        AppendVertex(verts, Vector3.Zero, up, new Vector4(1, 0, 0, 1));
        AppendVertex(verts, new Vector3(length, 0, 0), up, new Vector4(1, 0, 0, 1));
        AppendVertex(verts, Vector3.Zero, up, new Vector4(0, 1, 0, 1));
        AppendVertex(verts, new Vector3(0, length, 0), up, new Vector4(0, 1, 0, 1));
        AppendVertex(verts, Vector3.Zero, up, new Vector4(0, 0, 1, 1));
        AppendVertex(verts, new Vector3(0, 0, length), up, new Vector4(0, 0, 1, 1));
        return new MeshData([.. verts], [0, 1, 2, 3, 4, 5], MeshTopology.Lines);
    }
}
