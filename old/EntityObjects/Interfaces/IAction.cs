using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using EntityObjects;

namespace EntityObjects.Interfaces
{
    public interface IAction
    {
        string ActionName { get; }

        // What items can be used to perform this action?
        List<IItem> GetViableItems(Entity user, Entity target);

        // Execute with a specific item
        void Execute(Entity user, Entity target, IItem item);

        bool CanActorUse(Entity user);
        bool CanTarget(Entity user, Entity target);
    }
    public class AttackAction : IAction
    {
        public string ActionName => "Attack";

        public List<IItem> GetViableItems(Entity user, Entity target)
        {
            // Get all items that can attack
            return user.Inventory
                .Where(item => item.CanEnableAction("Attack", user, target))
                .ToList();
        }

        public void Execute(Entity user, Entity target, IItem item)
        {
            item.Apply("Attack", user, target);

            // Handle usage consumption
            if (item.UsesRemaining > 0)
                item.UsesRemaining--;
        }

        public bool CanActorUse(Entity user)
        {
            // For now, any entity can attack
            return true;
        }

        public bool CanTarget(Entity user, Entity target)
        {
            // For now, an entity can't attack itself
            return user.ID != target.ID;
        }
    }

    public class DefendAction : IAction
    {
        public string ActionName => "Defend";

        public List<IItem> GetViableItems(Entity user, Entity target)
        {
            // Shields, armor, defensive items
            return user.Inventory
                .Where(item => item.CanEnableAction("Defend", user, target))
                .ToList();
        }

        public void Execute(Entity user, Entity target, IItem item)
        {
            item.Apply("Defend", user, target);
        }

        public bool CanActorUse(Entity user)
        {
            // For now, any entity can defend
            return true;
        }

        public bool CanTarget(Entity user, Entity target)
        {
            // Defending is a self-targeted action
            return user.ID == target.ID;
        }
    }

    public class SpeakAction : IAction
    {
        public string ActionName => "Speak";

        public bool CanActorUse(Entity user)
        {
            // Can speak if the user has a giftable item.
            return user.Inventory.Any(i => i is Gift);
        }

        public bool CanTarget(Entity user, Entity target)
        {
            // Can't speak to yourself.
            return user.ID != target.ID;
        }

        public void Execute(Entity user, Entity target, IItem item)
        {
            if (item is Gift gift)
            {
                // Remove the gift from the user's inventory
                user.Inventory.Remove(gift);
                // Add the gift to the target's inventory
                target.Inventory.Add(gift);

                // Improve relationship
                if (!user.EntityRelationships.ContainsKey(target.ID))
                {
                    user.EntityRelationships[target.ID] = 0;
                }
                user.EntityRelationships[target.ID] += 10;
            }
        }

        public List<IItem> GetViableItems(Entity user, Entity target)
        {
            return user.Inventory.Where(i => i is Gift).ToList();
        }
    }
}
