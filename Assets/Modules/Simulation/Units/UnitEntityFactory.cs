using HyperRTS.Core;
using HyperRTS.Simulation.Health;
using HyperRTS.Simulation.Selection;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    public class UnitEntityFactory : IEntityFactory
    {
        public Entity CreateEntity(EntityCommandBuffer ecb)
        {
            var unit = ecb.CreateEntity();
            ecb.AddComponent<UnitTag>(unit);
            ecb.AddComponent(unit, LocalTransform.Identity);
            ecb.AddComponent(unit, new HealthComponent { CurrentHealth = 100, MaxHealth = 100 });
            ecb.AddComponent(unit, new MovementSpeed { Value = 5f });
            ecb.AddComponent<MoveDestination>(unit);
            ecb.SetComponentEnabled<MoveDestination>(unit, false); // idle until ordered
            SelectionComponents.AddTo(ecb, unit, SelectableKind.Unit);
            return unit;
        }
    }
}
