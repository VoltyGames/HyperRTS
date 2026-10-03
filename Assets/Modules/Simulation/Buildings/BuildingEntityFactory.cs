using HyperRTS.Core;
using HyperRTS.Simulation.Health;
using HyperRTS.Simulation.Selection;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings
{
    public class BuildingEntityFactory : IEntityFactory
    {
        public Entity CreateEntity(EntityCommandBuffer ecb)
        {
            var building = ecb.CreateEntity();
            ecb.AddComponent<BuildingTag>(building);
            ecb.AddComponent(building, LocalTransform.Identity);
            ecb.AddComponent(building, new HealthComponent { CurrentHealth = 500, MaxHealth = 500 });
            ecb.AddComponent(building, new ConstructionProgress { Value = 0 });
            SelectionComponents.AddTo(ecb, building, SelectableKind.Building);
            return building;
        }
    }
}
