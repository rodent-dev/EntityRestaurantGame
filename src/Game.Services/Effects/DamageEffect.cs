using Game.Contracts;

namespace Game.Services.Effects;

public class DamageEffect : IActionEffect
{
    public void Apply(Entity user, Entity target, Dictionary<string, float> parameters)
    {
        float amount = parameters.GetValueOrDefault("amount", 0f);
        target.TakeDamage(amount);
    }
}
