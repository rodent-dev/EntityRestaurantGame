namespace EntityObjects.Effects;

public class RelationshipEffect : IActionEffect
{
    public void Apply(Entity user, Entity target, Dictionary<string, float> parameters)
    {
        int amount = (int)parameters.GetValueOrDefault("amount", 0f);
        user.EntityRelationships.TryAdd(target.ID, 0);
        user.EntityRelationships[target.ID] += amount;
    }
}
