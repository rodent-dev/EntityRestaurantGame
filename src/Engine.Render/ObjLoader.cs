using System.Globalization;
using System.Numerics;

namespace Engine.Render;

/// <summary>
/// Minimal Wavefront OBJ parser: positions, normals, faces (with fan
/// triangulation and negative-index support). Texcoords/materials are
/// ignored; unknown commands are skipped so real-world exports load.
/// </summary>
public static class ObjLoader
{
    public static MeshData Load(string path, Vector4 color) =>
        Parse(File.ReadAllText(path), color);

    public static MeshData Parse(string objText, Vector4 color)
    {
        var lines = objText.Split('\n');

        var positions = new List<float>();
        var normals = new List<float>();
        var outVertices = new List<float>();
        var outIndices = new List<uint>();
        // One output vertex per unique (position, normal) pair; -1 normal = "unspecified".
        var vertexCache = new Dictionary<(int Pos, int Norm), uint>();

        for (int lineNum = 1; lineNum <= lines.Length; lineNum++)
        {
            string line = lines[lineNum - 1].Trim(); // also strips \r
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            try
            {
                switch (parts[0])
                {
                    case "v":
                        ParseTriple(parts, positions);
                        break;
                    case "vn":
                        ParseTriple(parts, normals);
                        break;
                    case "f":
                        ParseFace(parts, positions, normals, vertexCache, outVertices, outIndices, color);
                        break;
                    // vt, o, g, s, usemtl, mtllib, l, … — ignored on purpose.
                }
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                throw new FormatException($"Malformed OBJ at line {lineNum}: '{line}' ({ex.Message})", ex);
            }
        }

        return new MeshData([.. outVertices], [.. outIndices], MeshTopology.Triangles);
    }

    private static void ParseTriple(string[] parts, List<float> target)
    {
        if (parts.Length < 4) throw new FormatException("expected 3 components");
        target.Add(float.Parse(parts[1], CultureInfo.InvariantCulture));
        target.Add(float.Parse(parts[2], CultureInfo.InvariantCulture));
        target.Add(float.Parse(parts[3], CultureInfo.InvariantCulture));
    }

    private static void ParseFace(
        string[] parts,
        List<float> positions,
        List<float> normals,
        Dictionary<(int, int), uint> vertexCache,
        List<float> outVertices,
        List<uint> outIndices,
        Vector4 color)
    {
        if (parts.Length < 4) throw new FormatException("face needs at least 3 vertices");

        // Resolve each "i", "i/t", "i//n", "i/t/n" reference to 0-based (pos, norm).
        var corners = new List<(int Pos, int Norm)>(parts.Length - 1);
        for (int i = 1; i < parts.Length; i++)
        {
            var comp = parts[i].Split('/');
            int pos = ResolveIndex(int.Parse(comp[0], CultureInfo.InvariantCulture), positions.Count / 3);

            int norm = -1;
            if (comp.Length >= 3 && comp[2].Length > 0)
            {
                int raw = int.Parse(comp[2], CultureInfo.InvariantCulture);
                if (raw != 0) norm = ResolveIndex(raw, normals.Count / 3);
            }
            corners.Add((pos, norm));
        }

        // Fan triangulation: (0,1,2), (0,2,3), …
        for (int i = 1; i < corners.Count - 1; i++)
        {
            outIndices.Add(EmitVertex(corners[0], positions, normals, vertexCache, outVertices, color));
            outIndices.Add(EmitVertex(corners[i], positions, normals, vertexCache, outVertices, color));
            outIndices.Add(EmitVertex(corners[i + 1], positions, normals, vertexCache, outVertices, color));
        }
    }

    /// <summary>OBJ indices are 1-based; negative counts back from the end (-1 = last).</summary>
    private static int ResolveIndex(int raw, int count) => raw switch
    {
        > 0 => raw - 1,
        < 0 => count + raw,
        _ => throw new FormatException("index 0 is invalid in OBJ"),
    };

    private static uint EmitVertex(
        (int Pos, int Norm) key,
        List<float> positions,
        List<float> normals,
        Dictionary<(int, int), uint> vertexCache,
        List<float> outVertices,
        Vector4 color)
    {
        if (vertexCache.TryGetValue(key, out uint existing))
            return existing;

        uint index = (uint)(outVertices.Count / MeshData.VertexFloats);
        vertexCache[key] = index;

        outVertices.Add(positions[key.Pos * 3]);
        outVertices.Add(positions[key.Pos * 3 + 1]);
        outVertices.Add(positions[key.Pos * 3 + 2]);

        if (key.Norm >= 0)
        {
            outVertices.Add(normals[key.Norm * 3]);
            outVertices.Add(normals[key.Norm * 3 + 1]);
            outVertices.Add(normals[key.Norm * 3 + 2]);
        }
        else
        {
            outVertices.Add(0f);
            outVertices.Add(1f);
            outVertices.Add(0f);
        }

        outVertices.Add(color.X);
        outVertices.Add(color.Y);
        outVertices.Add(color.Z);
        outVertices.Add(color.W);

        return index;
    }
}
