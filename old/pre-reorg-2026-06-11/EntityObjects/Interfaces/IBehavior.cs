namespace EntityObjects.Interfaces;

public interface IBehavior
{
    string Name { get; }
    string BehaviorType { get; }
    int Scalar { get; set; }
    int Affect { get; set; }
    bool IsTargeted { get; set; }
}
