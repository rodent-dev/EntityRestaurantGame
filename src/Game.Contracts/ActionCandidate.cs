namespace Game.Contracts;

public class ActionCandidate
{
    public IAction Action { get; set; }
    public Entity Target { get; set; }
    public List<ActionWeight> Weights { get; set; } = new();

    public ActionCandidate(IAction action, Entity target)
    {
        Action = action;
        Target = target;
    }

    public int TotalWeight => Weights.Sum(w => w.Weight);
}

public class ActionWeight
{
    public int Weight { get; set; }
    public ActionWeightSource Source { get; set; }
}

public enum ActionWeightSource
{
    Base,
    Behaviors,
    Relationship,
    Status,
    Situation,
    PlayerInfluence
}
