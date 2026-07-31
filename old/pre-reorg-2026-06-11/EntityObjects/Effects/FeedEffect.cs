namespace EntityObjects.Effects;

public class FeedEffect : IActionEffect
{
    public void Apply(Entity user, Entity target, Dictionary<string, float> parameters)
    {
        int amount = (int)parameters.GetValueOrDefault("amount", 0f);
        target.Needs.Feed(amount);
    }
}
