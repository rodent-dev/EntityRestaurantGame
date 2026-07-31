using Game.Contracts;

namespace Game.Services.Effects;

public class HealEffect : IActionEffect
{
    public void Apply(Entity user, Entity target, Dictionary<string, float> parameters)
    {
        float amount = parameters.GetValueOrDefault("amount", 0f);
        target.Stats.Health.Heal(amount);
    }
}
