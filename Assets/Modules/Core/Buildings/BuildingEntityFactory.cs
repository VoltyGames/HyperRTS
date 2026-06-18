using HyperRTS.Core.Health;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Core.Buildings
{
    public class BuildingEntityFactory : IEntityFactory
    {
        public Entity CreateEntity(EntityManager entityManager)
        {
            var building = entityManager.CreateEntity();
            entityManager.AddComponentData(building, new BuildingTag());
            entityManager.AddComponentData(building, LocalTransform.Identity);
            entityManager.AddComponentData(building, new HealthComponent { CurrentHealth = 500, MaxHealth = 500 });
            entityManager.AddComponentData(building, new ConstructionProgress { Value = 0 });
            return building;
        }
    }
}
