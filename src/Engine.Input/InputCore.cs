using System.Numerics;
using System.Text.Json;

namespace Engine.Input;

public enum BindingKind
{
    Key,
    MouseButton,
}

/// <summary>A device-agnostic input source: a kind plus a code string like "W", "Escape", "Right".</summary>
public readonly record struct Binding(BindingKind Kind, string Code)
{
    public override string ToString() => $"{Kind}:{Code}";
}

/// <summary>
/// Maps game actions (any enum) to the device inputs that trigger them.
/// Serializes to data/input/*.json so controls are rebindable without a recompile.
/// </summary>
public sealed class BindingSet<TAction> where TAction : struct, Enum
{
    private readonly Dictionary<TAction, List<Binding>> _bindings = [];

    public void Bind(TAction action, Binding binding)
    {
        if (!_bindings.TryGetValue(action, out var list))
            _bindings[action] = list = [];
        if (!list.Contains(binding))
            list.Add(binding);
    }

    public IReadOnlyList<Binding> BindingsFor(TAction action) =>
        _bindings.GetValueOrDefault(action) ?? (IReadOnlyList<Binding>)[];

    public string ToJson()
    {
        var dict = new Dictionary<string, List<object>>();
        foreach (var (action, list) in _bindings)
            dict[action.ToString()] = [.. list.Select(b => (object)new { kind = b.Kind.ToString(), code = b.Code })];

        return JsonSerializer.Serialize(dict, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
    }

    public static BindingSet<TAction> FromJson(string json)
    {
        var result = new BindingSet<TAction>();
        using var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!Enum.TryParse(prop.Name, out TAction action))
                throw new ArgumentException($"Unknown action name: '{prop.Name}'", nameof(json));

            foreach (var item in prop.Value.EnumerateArray())
            {
                string? kindStr = item.GetProperty("kind").GetString();
                string? code = item.GetProperty("code").GetString();
                if (!Enum.TryParse<BindingKind>(kindStr, ignoreCase: true, out var kind))
                    throw new ArgumentException($"Unknown binding kind: '{kindStr}'", nameof(json));
                result.Bind(action, new Binding(kind, code ?? ""));
            }
        }

        return result;
    }

    public static BindingSet<TAction> FromFile(string path) => FromJson(File.ReadAllText(path));

    public void Save(string path) => File.WriteAllText(path, ToJson());
}

/// <summary>
/// Per-frame input snapshot. An adapter feeds raw device transitions; game code
/// only ever queries by action. Call <see cref="NextFrame"/> once per frame,
/// AFTER game logic has consumed the frame's input.
/// </summary>
public sealed class InputState<TAction> where TAction : struct, Enum
{
    private readonly Dictionary<(BindingKind, string), TAction[]> _reverseLookup = [];

    // An action is held while ANY of its bound device codes is down (ref-count),
    // so releasing W doesn't kill PanForward while ArrowUp is still pressed.
    private readonly HashSet<(BindingKind, string)> _heldCodes = [];
    private readonly Dictionary<TAction, int> _heldCount = [];
    private readonly HashSet<TAction> _pressedThisFrame = [];
    private readonly HashSet<TAction> _releasedThisFrame = [];

    private Vector2 _mouseDelta;
    private Vector2 _mousePosition;
    private float _scroll;

    public InputState(BindingSet<TAction> bindings)
    {
        foreach (var action in Enum.GetValues<TAction>())
            foreach (var b in bindings.BindingsFor(action))
            {
                var key = (b.Kind, b.Code);
                _reverseLookup[key] = _reverseLookup.TryGetValue(key, out var existing)
                    ? [.. existing, action]
                    : [action];
            }
    }

    public void DeviceDown(BindingKind kind, string code)
    {
        if (!_heldCodes.Add((kind, code))) return; // key repeat
        if (!_reverseLookup.TryGetValue((kind, code), out var actions)) return;

        foreach (var action in actions)
        {
            int count = _heldCount.GetValueOrDefault(action) + 1;
            _heldCount[action] = count;
            if (count == 1)
            {
                _pressedThisFrame.Add(action);
                _releasedThisFrame.Remove(action);
            }
        }
    }

    public void DeviceUp(BindingKind kind, string code)
    {
        if (!_heldCodes.Remove((kind, code))) return; // wasn't down
        if (!_reverseLookup.TryGetValue((kind, code), out var actions)) return;

        foreach (var action in actions)
        {
            int count = Math.Max(0, _heldCount.GetValueOrDefault(action) - 1);
            _heldCount[action] = count;
            if (count == 0)
            {
                _releasedThisFrame.Add(action);
                _pressedThisFrame.Remove(action);
            }
        }
    }

    public void FeedMouseDelta(Vector2 delta) => _mouseDelta += delta;
    public void FeedMousePosition(Vector2 position) => _mousePosition = position;
    public void FeedScroll(float delta) => _scroll += delta;

    /// <summary>Clears per-frame state; held actions persist.</summary>
    public void NextFrame()
    {
        _pressedThisFrame.Clear();
        _releasedThisFrame.Clear();
        _mouseDelta = Vector2.Zero;
        _scroll = 0f;
    }

    public bool IsHeld(TAction action) => _heldCount.GetValueOrDefault(action) > 0;
    public bool WasPressed(TAction action) => _pressedThisFrame.Contains(action);
    public bool WasReleased(TAction action) => _releasedThisFrame.Contains(action);
    public Vector2 MouseDelta => _mouseDelta;
    /// <summary>Absolute cursor position in window coordinates (persists across frames).</summary>
    public Vector2 MousePosition => _mousePosition;
    public float Scroll => _scroll;

    public float DigitalAxis(TAction negative, TAction positive) =>
        (IsHeld(positive) ? 1f : 0f) - (IsHeld(negative) ? 1f : 0f);

    public Vector2 DigitalAxis2(TAction left, TAction right, TAction down, TAction up) =>
        new(DigitalAxis(left, right), DigitalAxis(down, up));
}
