using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Hits and heals on a sectioned building, which spread over its <see cref="Section"/>s.</summary>
    public struct SectionDamage
    {
        private ComponentLookup<Sections> _buildings;
        private BufferLookup<Section> _sections;
        [ReadOnly] private BufferLookup<SectionFactor> _factors;
        [ReadOnly] private ComponentLookup<Repairable> _repairables;

        public SectionDamage(ref SystemState state)
        {
            _buildings = state.GetComponentLookup<Sections>();
            _sections = state.GetBufferLookup<Section>();
            _factors = state.GetBufferLookup<SectionFactor>(true);
            _repairables = state.GetComponentLookup<Repairable>(true);
        }

        public void Update(ref SystemState state)
        {
            _buildings.Update(ref state);
            _sections.Update(ref state);
            _factors.Update(ref state);
            _repairables.Update(ref state);
        }

        public bool IsSectioned(Entity target) => _sections.HasBuffer(target);

        /// <summary>The share of the target still standing: its sections', else its health's.</summary>
        public float Intact(Entity target, in Health health) =>
            _sections.TryGetBuffer(target, out var sections)
                ? Sections.Intact(sections)
                : health.Current / math.max(health.Max, 1e-4f);

        /// <summary>The hit on the building's sections, in its space; returns the damage dealt.</summary>
        public float Route(Entity target, in LocalTransform transform, in DamageEvent hit, float scale)
        {
            var building = _buildings[target];
            var dealt = building.Route(_sections[target], _factors[target], hit,
                transform.InverseTransformPoint(hit.Origin), transform.InverseTransformPoint(hit.Position), scale,
                target == hit.Target);
            _buildings[target] = building;
            return dealt;
        }

        /// <summary>Health restored by a heal of <paramref name="amount"/>, rebuilding broken sections first.</summary>
        public float Repair(Entity target, float amount)
        {
            if (!_sections.TryGetBuffer(target, out var sections))
            {
                return amount;
            }

            var building = _buildings[target];
            var restored = building.Repair(sections, amount);
            _buildings[target] = building;
            return restored;
        }

        /// <summary>A repairable building's health follows its sections and never drops to 0, so it is not destroyed.</summary>
        public float Resolved(Entity target, float health) =>
            _repairables.HasComponent(target) ? Repairable.Health(_sections[target]) : health;
    }
}
