using EntityObjects.Interfaces;

namespace EntityObjects;

public class ActionModule
{
    private readonly Dictionary<string, IAction> _actions = new();

    public void AttachAction(IAction action) => _actions[action.ActionName] = action;
    public bool DetachAction(string actionName) => _actions.Remove(actionName);
    public IAction? GetAction(string actionName) => _actions.TryGetValue(actionName, out var a) ? a : null;
    public IEnumerable<IAction> GetAllActions() => _actions.Values;
    public bool HasAction(string actionName) => _actions.ContainsKey(actionName);
}
