using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>Hits and heals on a sectioned building, which spread over its <see cref="Section"/>s.</summary>
    public struct SectionDamage
    {
        private ComponentLookup<SectionedBuilding> _buildings;
        private BufferLookup<Section> _sections;
        [ReadOnly] private BufferLookup<SectionFactor> _factors;
        [ReadOnly] private ComponentLookup<Repairable> _repairables;

        public SectionDamage(ref SystemState state)
        {
            _buildings = state.GetComponentLookup<SectionedBuilding>();
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
                ? SectionedBuilding.Intact(sections)
                : health.Current / math.max(health.Max, 1e-4f);

        /// <summary>
        /// Deals the hit to the sections it reaches, each times <paramref name="scale"/> and its material's factor,
        /// and returns the damage dealt. A direct hit lands in full on the first section the line from its origin to
        /// its point enters (else the nearest), and its splash on the rest; splash from a hit elsewhere reaches every
        /// section within its radius.
        /// </summary>
        public float Route(Entity target, in LocalTransform transform, in DamageEvent hit, float scale)
        {
            var building = _buildings[target];
            var sections = _sections[target];
            var factors = _factors[target];
            var position = transform.InverseTransformPoint(hit.Position);
            var entry = target == hit.Target
                ? SectionedBuilding.Entry(sections, transform.InverseTransformPoint(hit.Origin), position)
                : -1;
            var dealt = 0f;
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section.IsBroken)
                {
                    continue;
                }

                var gap = section.Gap(position);
                if (i != entry && (hit.Profile.Radius <= 0f || gap > hit.Profile.Radius))
                {
                    continue;
                }

                var share = i == entry ? 1f : hit.Profile.SplashFactor(gap);
                var amount = hit.Profile.Amount * share * scale * SectionFactor.Find(factors, section.Material, hit.Profile.Type);
                dealt += building.Damage(sections, i, amount);
            }

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
