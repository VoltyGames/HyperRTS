using HyperRTS.Core.SharedComponents;
using Unity.Entities;

namespace HyperRTS.Core.Buildings
{
    public class BuildingFactory : IEntityFactory
    {
        public Entity CreateEntity(EntityManager entityManager)
        {
            var building = entityManager.CreateEntity();
            entityManager.AddComponentData(building, new BuildingTag());
            entityManager.AddComponentData(building, new Health { CurrentHealth = 500, MaxHealth = 500 });
            entityManager.AddComponentData(building, new ConstructionProgress { Value = 0 });
            return building;
        }
    }
}
