namespace Game.Contracts;

public class EntityHealth
{
    public double MaxHealth { get; private set; }
    public double CurrentHealth { get; private set; }

    public EntityHealth(int constitution = 10, double weight = 100, double height = 6)
    {
        MaxHealth = (constitution * 10) + (weight * 0.2) - (height * 0.1);
        CurrentHealth = MaxHealth;
    }

    public double TakeDamage(double amount)
    {
        CurrentHealth = Math.Max(0, CurrentHealth - amount);
        return CurrentHealth;
    }

    public double Heal(double amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        return CurrentHealth;
    }
}
