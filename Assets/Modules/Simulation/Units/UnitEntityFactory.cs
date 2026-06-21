using HyperRTS.Core;
using HyperRTS.Simulation.Health;
using HyperRTS.Simulation.Selection;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    public class UnitEntityFactory : IEntityFactory
    {
        public Entity CreateEntity(EntityManager entityManager)
        {
            var unit = entityManager.CreateEntity();
            entityManager.AddComponentData(unit, new UnitTag());
            entityManager.AddComponentData(unit, LocalTransform.Identity);
            entityManager.AddComponentData(unit, new HealthComponent { CurrentHealth = 100, MaxHealth = 100 });
            entityManager.AddComponentData(unit, new MovementSpeed { Value = 5f });
            SelectionComponents.AddTo(entityManager, unit, SelectableKind.Unit);
            return unit;
        }
    }
}
