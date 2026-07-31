using EntityObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace EntityObjects
{
    public interface IItem
    {
        // ===== IDENTITY =====
        string ItemId { get; } // Unique identifier for the item
        string ItemName { get; }
        string Description { get; }

        // ===== TYPE =====
        ItemType Type { get; } // New enum for item categorization
        float SpeakEffectModifier { get; } // How this item affects the 'speak' action effectiveness

        // ===== ACTION ENABLEMENT =====
        // Can this item be used to perform this action on this target?
        bool CanEnableAction(string actionName, Entity user, Entity target);

        // How effective is this item for this action-target pair?
        // This is where ALL the intelligence lives - scarcity, AOE, context, everything
        float GetEffectivenessScore(string actionName, Entity user, Entity target);

        // ===== EXECUTION =====
        // Apply the item's effect for the given action
        void Apply(string actionName, Entity user, Entity target);

        // ===== RESOURCE MANAGEMENT =====
        int UsesRemaining { get; set; } // -1 = infinite, 0 = depleted, >0 = limited
        bool IsDepleted => UsesRemaining == 0;

        // How reluctant should the entity be to use this? (0.0 = not scarce, 1.0 = very scarce)
        // This could be dynamic based on inventory count, rarity, etc.
        float GetScarcityPenalty(Entity user);

        // ===== OPTIONAL: METADATA =====
        bool IsEquipped { get; set; } // For quick-access items
        float Weight { get; } // For encumbrance systems
    }

    // New enum for item types as requested by the user
    public enum ItemType
    {
        Standard,
        Weapon,
        Consumable,
        Merit
    }

    // The old ItemCategory enum is no longer used and will be removed or replaced in concrete implementations.
    // Kept for reference, but ideally removed from final code.
    // public enum ItemCategory
    // {
    //     Weapon,
    //     Consumable,
    //     Tool,
    //     Armor,
    //     Misc
    // }
}
