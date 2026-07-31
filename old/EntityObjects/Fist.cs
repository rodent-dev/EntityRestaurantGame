using EntityObjects;

namespace EntityObjects
{
    public class Fist : IItem
    {
        public string ItemId => "fist";
        public string ItemName => "Fist";
        public string Description => "A simple fist.";
        public int UsesRemaining { get; set; } = -1;
        public bool IsEquipped { get; set; } = true;
        public float Weight => 0;
        public ItemCategory Category => ItemCategory.Weapon;

        public void Apply(string actionName, Entity user, Entity target)
        {
            if (actionName == "Attack")
            {
                target.TakeDamage(5);
            }
        }

        public bool CanEnableAction(string actionName, Entity user, Entity target)
        {
            return actionName == "Attack";
        }

        public float GetEffectivenessScore(string actionName, Entity user, Entity target)
        {
            return 1;
        }

        public float GetScarcityPenalty(Entity user)
        {
            return 0;
        }
    }
}
