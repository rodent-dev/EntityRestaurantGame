namespace Engine.Items;

/// <summary>
/// Maps effect keyword strings to IActionEffect implementations.
/// Must be populated before ItemFactory is called.
/// </summary>
public class EffectRegistry<TActor>
{
    private readonly Dictionary<string, IActionEffect<TActor>> _effects = new();

    public void Register(string keyword, IActionEffect<TActor> effect)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("Effect keyword must not be empty.", nameof(keyword));
        _effects[keyword] = effect;
    }

    /// <summary>Resolves a keyword to its implementation. Throws if not registered.</summary>
    public IActionEffect<TActor> Resolve(string keyword)
    {
        if (_effects.TryGetValue(keyword, out var effect)) return effect;
        throw new InvalidOperationException(
            $"No effect registered for keyword '{keyword}'. Register it before loading items.");
    }

    public bool IsRegistered(string keyword) => _effects.ContainsKey(keyword);
}
