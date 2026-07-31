namespace EntityObjects;

/// <summary>
/// A named behavioral trait that boosts actions matching its keywords.
/// Weight contribution = Affect * Scalar * (number of keyword matches).
/// More keyword overlap = stronger influence, creating natural gradients.
/// </summary>
public class BehaviorModule
{
    public string Name { get; set; }
    public HashSet<string> Keywords { get; set; }
    public int Affect { get; set; }
    public int Scalar { get; set; }

    public BehaviorModule(string name, HashSet<string> keywords, int affect, int scalar = 1)
    {
        Name = name;
        Keywords = keywords;
        Affect = affect;
        Scalar = scalar;
    }

    /// <summary>
    /// Returns how many of this behavior's keywords overlap with the given action's keywords.
    /// </summary>
    public int MatchCount(HashSet<string> actionKeywords)
    {
        int count = 0;
        foreach (var keyword in Keywords)
        {
            if (actionKeywords.Contains(keyword))
                count++;
        }
        return count;
    }
}
