using EntityObjects;

namespace EntityObjects
{
    public class Gift : IItem
    {
        public string ItemId => "gift";
        public string ItemName => "A Small Gift";
        public string Description => "A small, nicely wrapped gift.";
        public int UsesRemaining { get; set; } = 1;
        public bool IsEquipped { get; set; }
        public float Weight => 1;
        public ItemCategory Category => ItemCategory.Misc;

        public void Apply(string actionName, Entity user, Entity target)
        {
            // The logic is in SpeakAction, this can be empty.
        }

        public bool CanEnableAction(string actionName, Entity user, Entity target)
        {
            return actionName == "Speak";
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
