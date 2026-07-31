namespace Game.Contracts;

/// <summary>One entry in an entity's recent-decision history.</summary>
public record DecisionRecord(string ActionName, string TargetName, string PrimaryFactor);
