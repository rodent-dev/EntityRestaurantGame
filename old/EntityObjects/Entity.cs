using EntityObjects.EntityObjects;
using EntityObjects.Interfaces;

namespace EntityObjects
{
    public class Entity
    {
        /// <summary>
        /// Simple DND like stats for the entity, allow some degree of scaling alongside the physique
        /// </summary>
        CombatStats Stats { get; set; }
        /// <summary>
        /// Behavior module set that defines the entity's personality and action tendencies
        /// </summary>
        public HashSet<BehaviorModule> Behaviors { get; set; }
        /// <summary>
        /// Gets or sets the physical attributes associated with the entity.
        /// </summary>
        EntityPhysique Physique { get; set; }
        /// <summary>
        /// Gets or sets the actions associated with the entity. See EntityType.
        /// </summary>
        ActionModule Actions { get; set; }

        /// <summary>
        /// All discovered relationships with other entities, Key is the Entity ID, Value is the relationship score
        /// </summary>
        public Dictionary<string, int> EntityRelationships { get; set; }

        public List<IItem> Inventory { get; set; }

        /// <summary>
        /// String Based entitiy type identifier, When an entity is created, use this to map out what specific action modules to attach.
        /// </summary>
        public string EntityType { get; set; }
        public string DisplayName { get; set; }
        public string ID { get; set; }

        /// <summary>
        /// Last time this was updated - prevent current tick double updates
        /// </summary>
        public long LastScoreTick { get; set; }
        /// <summary>
        /// Represents whether ANY associated values have been updated for this entity during the current tick.
        /// </summary>
        public bool HasUpdatedValueThisTick { get; set; }
        public Entity()
        {
            Actions = new ActionModule();
            Behaviors = new HashSet<BehaviorModule>();
            Stats = new CombatStats();
            Physique = new EntityPhysique();
            EntityRelationships = new Dictionary<string, int>();
            Inventory = new List<IItem>();
            EntityType = string.Empty;
            DisplayName = string.Empty;
            ID = string.Empty;
            ActionCandidates = new List<ActionCandidate>();
            BehaviorProfile = new Dictionary<string, int>();
        }


        /// <summary>
        /// Holds the action and the 
        /// Resets after an entire interaction
        /// </summary>
        public List<ActionCandidate> ActionCandidates { get; set; }

        public List<IItem> Invetory { get; set; } = new List<IItem>();

        /// <summary>
        /// Get the action module for this entity
        /// </summary>
        public ActionModule GetActionModule() => Actions;

        /// <summary>
        /// Only need to calculate this once per entity creation or behavior update
        /// </summary>
        public Dictionary<string, int> BehaviorProfile { get; set; }

        public double TakeDamage(double damageAmount) => Stats.Health.TakeDamage(damageAmount);
        public double GetCurrentHealth() => Stats.Health.CurrentHealth;


        /* 
         * TODO - This should be more dynamic
         * We want to allow this to be a way where fights are OPEN, entities can choose to join combat or flee based on their behaviors and relationships
         * Additionally, that should allow for specific item candidates to be considered. In a situation where we notice 'Hey, lot of bad guys
         * we can opt for certain items with additional usage weights. instead of just all actions + all entities.' 
         * 
         */
        public List<ActionCandidate> BuildCandidates(
            Entity actor,
            List<Entity> activeEntities)
        {
            var candidates = new List<ActionCandidate>();

            foreach (var action in Actions.GetAllActions())
            {
                //if (!action.CanActorUse(actor))
                //    continue;

                foreach (var target in activeEntities)
                {
                    //if (!action.CanTarget(actor, target))
                    //    continue;

                    // item handled later
                    candidates.Add(new ActionCandidate(action, target));
                }
            }

            return candidates;
        }

        public void ClearActionCandidates()
        {
            ActionCandidates = new List<ActionCandidate>();
        }


        /////////// ActionCandidate Weight Calculations ///////////

        /// <summary>
        /// Calculate the behavior weight for each action candidate based on this entity's behaviors
        /// </summary>
        public void CalculateBehaviorWeight()
        {
            // These are cheap calculations, and since the objects they are derived from are dynamic, I justify recalculating them each time.

            // 1. Build behavior weight dictionary
            var behaviorWeights = new Dictionary<string, int>();
            foreach (var behavior in Behaviors)
            {
                if (!behaviorWeights.ContainsKey(behavior.BehaviorType))
                {
                    behaviorWeights[behavior.BehaviorType] = 0;
                }
                behaviorWeights[behavior.BehaviorType] += behavior.Affect;
            }

            // 2. Build Relationship weight dictionary (check UNIQUE active candidates)
            var uniqueTargets = ActionCandidates.Select(c => c.Target.ID).Distinct().ToList();
            var relationshipWeights = new Dictionary<string, int>();
            foreach (var targetId in uniqueTargets)
            {
                if (EntityRelationships.TryGetValue(targetId, out var relationshipScore))
                {
                    relationshipWeights[targetId] = relationshipScore;
                }
            }

            // 3. Status dictionary (TODO: implement when status effects are available)
            var statusWeights = new Dictionary<string, int>();

            // Apply weights to candidates
            foreach (var candidate in ActionCandidates)
            {
                // Behavior weight - check if action name matches a behavior type
                if (behaviorWeights.TryGetValue(candidate.Action.ActionName, out var bWeight))
                {
                    candidate.Weights.Add(new ActionWeight
                    {
                        Weight = bWeight,
                        Source = ActionWeightSource.Behaviors
                    });
                }

                // Relationship weight - apply to targeted actions
                if (candidate.Target != null && relationshipWeights.TryGetValue(candidate.Target.ID, out var rWeight))
                {
                    candidate.Weights.Add(new ActionWeight
                    {
                        Weight = rWeight,
                        Source = ActionWeightSource.Relationship
                    });
                }

                // Status weight - when available
                //if (statusWeights.TryGetValue(candidate.Target?.ID ?? string.Empty, out var sWeight))
                //{
                //    candidate.Weights.Add(new ActionWeight
                //    {
                //        Weight = sWeight,
                //        Source = ActionWeightSource.Status
                //    });
                //}
            }
        }
    }
    // -- Core Stats -- 
    public class CombatStats
    {
        //defaults
        const int DEFAULT_STAT = 10;
        const double DEFAULT_HEALTH = 100.0;
        const double NEGATION_THRESHOLD = 0.33;


        int Strength { get; set; }
        int Agility { get; set; }
        int Intelligence { get; set; }
        int Consitution { get; set; }
        int Wisdom { get; set; }
        int Charisma { get; set; }
        public EntityHealth Health { get; set; }


        // ------- Constructors

        // Use All arguments
        public CombatStats(int str, int agi, int intel, int con, int wis, int cha)
        {
            Strength = str;
            Agility = agi;
            Intelligence = intel;
            Wisdom = wis;
            Consitution = con;
            Charisma = cha;
            Health = new EntityHealth();
            Health.MaxHealth = Health.CalculateMaxHealthFromStats(con, 100, 6);
            Health.CurrentHealth = Health.MaxHealth;
        }

        // No Arguments, use Default
        public CombatStats(bool isRandom = false)
        {
            Strength = CalculateCharacterStat(isRandom);
            Agility = CalculateCharacterStat(isRandom);
            Intelligence = CalculateCharacterStat(isRandom);
            Wisdom = CalculateCharacterStat(isRandom);
            Consitution = CalculateCharacterStat(isRandom);
            Charisma = CalculateCharacterStat(isRandom);
            Health = new EntityHealth();
            Health.MaxHealth = Health.CalculateMaxHealthFromStats(Consitution, 100, 6);
            Health.CurrentHealth = Health.MaxHealth;
        }

        /// <summary>
        /// Get a random stat set for this entity
        /// </summary>
        public int CalculateCharacterStat(bool isRandom)
        {
            if (!isRandom)
            {
                return DEFAULT_STAT;
            }
            var random = new Random();
            bool isNegative = random.NextDouble() <= NEGATION_THRESHOLD;

            var statValue = isNegative ? random.Next(0, 10) * -1 : random.Next(0, 10);
            return DEFAULT_STAT + statValue;
        }
    }

    public class EntityHealth
    {
        public double MaxHealth { get; set; }
        public double CurrentHealth { get; set; }

        //Function - figure out health from entity stats
        public double CalculateMaxHealthFromStats(int con, double weight, double height)
        {
            // TODO - tinker but this is fine for now   
            return (con * 10) + (weight * 0.2) - (height * 0.1);
        }

        public double TakeDamage(double damageAmount)
        {
            CurrentHealth -= damageAmount;
            if (CurrentHealth < 0)
            {
                CurrentHealth = 0;
            }
            return CurrentHealth;
        }

        /// <summary>
        /// Update the entities health after a stat change, This returns teh CURRENT health, not maximum health.
        /// </summary>
        /// <returns> Current Health of Entity </returns>
        public double UpdateHealthAfterStatChange(int con, double weight, double height)
        {
            double newMaxHealth = CalculateMaxHealthFromStats(con, weight, height);
            double ratio = CurrentHealth / MaxHealth;
            MaxHealth = newMaxHealth;
            CurrentHealth = newMaxHealth * ratio;
            return CurrentHealth;
        }
    }

    //todo
    public class EntityPhysique
    {
        double Weight { get; set; }
        double Height { get; set; }
    }
}
