namespace Game.Contracts;

/// <summary>
/// The verbs an entity has. Ordered by name, deliberately: BuildCandidates
/// walks this to build the candidate list, and SelectWeighted then walks THAT
/// list accumulating until it passes the roll — so candidate order decides who
/// wins a given roll. A plain Dictionary iterates in string-hash order, and
/// .NET randomizes string hashing per process, which silently made every run
/// of the same seeded world diverge. Ordinal sort costs nothing at ~10 verbs
/// and buys reproducibility.
/// </summary>
public class ActionModule
{
    private readonly SortedDictionary<string, IAction> _actions = new(StringComparer.Ordinal);

    public void AttachAction(IAction action) => _actions[action.ActionName] = action;
    public bool DetachAction(string actionName) => _actions.Remove(actionName);
    public IAction? GetAction(string actionName) => _actions.TryGetValue(actionName, out var a) ? a : null;
    public IEnumerable<IAction> GetAllActions() => _actions.Values;
    public bool HasAction(string actionName) => _actions.ContainsKey(actionName);
}
