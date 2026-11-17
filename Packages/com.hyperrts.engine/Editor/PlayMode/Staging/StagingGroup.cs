using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>The units one spawn step made, under the label later steps use for them.</summary>
    internal sealed class StagingGroup
    {
        private readonly List<Entity> _units;

        public StagingGroup(string name, byte owner, List<Entity> units)
        {
            Name = name;
            Owner = owner;
            _units = units;
        }

        public string Name { get; }

        public byte Owner { get; }

        public int Total => _units.Count;

        public IReadOnlyList<Entity> Members => _units;

        public List<Entity> Alive(EntityManager entityManager) => _units.FindAll(unit => IsAlive(entityManager, unit));

        /// <summary>The middle of the live units on the ground plane; false when none is left.</summary>
        public bool TryGetCentre(EntityManager entityManager, out float2 centre)
        {
            var alive = Alive(entityManager);
            centre = default;
            if (alive.Count == 0)
            {
                return false;
            }

            foreach (var unit in alive)
            {
                centre += entityManager.GetComponentData<LocalTransform>(unit).Position.xz;
            }

            centre /= alive.Count;
            return true;
        }

        private static bool IsAlive(EntityManager entityManager, Entity unit)
        {
            if (!entityManager.Exists(unit) || !entityManager.HasComponent<Health>(unit))
            {
                return false;
            }

            var dead = entityManager.HasComponent<Dead>(unit) && entityManager.IsComponentEnabled<Dead>(unit);
            return !dead && entityManager.GetComponentData<Health>(unit).Current > 0f;
        }
    }
}
