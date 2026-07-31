using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace Engine.Render;

/// <summary>Compiled + linked GLSL program with a cached uniform-location table.</summary>
public sealed class Shader : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    private readonly Dictionary<string, int> _uniformLocations = [];

    public Shader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        uint vs = Compile(ShaderType.VertexShader, vertexSource);
        uint fs = Compile(ShaderType.FragmentShader, fragmentSource);

        _handle = gl.CreateProgram();
        gl.AttachShader(_handle, vs);
        gl.AttachShader(_handle, fs);
        gl.LinkProgram(_handle);
        gl.GetProgram(_handle, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
            throw new InvalidOperationException($"Shader link failed: {gl.GetProgramInfoLog(_handle)}");

        gl.DetachShader(_handle, vs);
        gl.DetachShader(_handle, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
    }

    public static Shader FromFiles(GL gl, string vertexPath, string fragmentPath) =>
        new(gl, File.ReadAllText(vertexPath), File.ReadAllText(fragmentPath));

    private uint Compile(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException($"{type} compile failed: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }

    public void Use() => _gl.UseProgram(_handle);

    private int Loc(string name)
    {
        if (!_uniformLocations.TryGetValue(name, out int loc))
        {
            loc = _gl.GetUniformLocation(_handle, name);
            _uniformLocations[name] = loc;
        }
        return loc;
    }

    /// <summary>
    /// System.Numerics matrices are row-major with row-vector convention; GLSL
    /// expects column-major column-vector. Uploading the raw memory with
    /// transpose=false converts between the two conventions exactly, so
    /// shaders write the usual <c>uMvp * vec4(pos, 1)</c>.
    /// </summary>
    public void Set(string name, Matrix4x4 value)
    {
        var floats = MemoryMarshal.Cast<Matrix4x4, float>(MemoryMarshal.CreateReadOnlySpan(ref value, 1));
        _gl.UniformMatrix4(Loc(name), 1, false, floats);
    }

    public void Set(string name, Vector4 v) => _gl.Uniform4(Loc(name), v.X, v.Y, v.Z, v.W);
    public void Set(string name, Vector3 v) => _gl.Uniform3(Loc(name), v.X, v.Y, v.Z);
    public void Set(string name, float f) => _gl.Uniform1(Loc(name), f);

    public void Dispose() => _gl.DeleteProgram(_handle);
}
