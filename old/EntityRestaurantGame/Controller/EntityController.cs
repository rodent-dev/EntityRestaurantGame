using EntityObjects;

namespace EntityRestaurantGame.Controller
{
    public class EntityController
    {
        const int DEFAULT_RELATIONSHIP_SCORE = 0;
        /// <summary>
        /// Entities currently being managed
        /// </summary>
        public List<Entity> ActiveEntities  { get; set; }

        /// <summary>
        /// Entities that will be processed in bulk later. These are NOT directly managed until moved to ActiveEntities
        /// </summary>
        public List<Entity> InactiveEntities { get; set; }


        // Eventually entities will live in regions (thats specific for the POC demo, when we move to 3d space
        // concern yourself with that

        public EntityController()
        {
            ActiveEntities = new List<Entity>();
            InactiveEntities = new List<Entity>();
        }

        public Entity MakeEntityActive(Entity entity) {
            var currentEntity = InactiveEntities.IndexOf(entity);
            
            if(currentEntity == -1)
            {
                throw new Exception("Entity not found in InactiveEntities");
            }

            InactiveEntities.Remove(entity);
            ActiveEntities.Add(entity);

            return entity;
        }

        public Entity MakeEntityInactive(Entity entity)
        {
            var currentEntity = ActiveEntities.IndexOf(entity);

            if (currentEntity == -1)
            {
                throw new Exception("Entity not found in InactiveEntities");
            }

            ActiveEntities.Remove(entity);
            InactiveEntities.Add(entity);

            return entity;
        }

        /// <summary>
        /// Runner method - Perform All actions necessary on active entities    
        /// </summary>
        public void ProcessEntities()
        {
            foreach (var entity in ActiveEntities) {
                //With the new candidate system, this should just need to perform the action that was attached
                //during calculation phase
            }
        }

        /// ------------- TESTING METHODS ------------

        public Entity[] CreateDummyEntities(int count)
        {
            Entity[] entities = new Entity[count];
            for (int i = 0; i < count; i++)
            {
                entities[i] = new Entity();
            }
            return entities;
        }
    }
}
