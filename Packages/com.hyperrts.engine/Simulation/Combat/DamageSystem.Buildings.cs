using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    public partial struct DamageSystem
    {
        private partial struct ResolveJob
        {
            /// <summary>Passes the share of a hit on a shelter that its walls, at their health before the hit, don't stop.</summary>
            private void PassToPassengers(Entity target, float amount, in Health health, in DamageEvent hit, float3 from)
            {
                if (hit.GarrisonFactor <= 0f || !Shelters.TryGetComponent(target, out var shelter))
                {
                    return;
                }

                var passing = hit;
                passing.Target = Entity.Null;
                passing.Origin = from;
                var intact = BuildingSections.TryGetBuffer(target, out var sections)
                    ? Sections.Intact(sections)
                    : health.Current / math.max(health.Max, 1e-4f);
                passing.Amount = shelter.Passing(hit, amount, intact);
                passing.Radius = 0f;
                if (passing.Amount > 0f)
                {
                    ShelterHits[target].Add(new ShelterHit { Hit = passing });
                }
            }

            /// <summary>The hit on a sectioned building's sections, in its space; returns the damage dealt.</summary>
            private float Route(Entity target, in DamageEvent hit, float3 from, float scale)
            {
                var transform = Transforms[target];
                var building = Buildings[target];
                var dealt = building.Route(BuildingSections[target], SectionFactors[target], hit,
                    transform.InverseTransformPoint(hit.Origin), transform.InverseTransformPoint(hit.Position), scale,
                    target == hit.Target);
                building.Side = ToppleMath.Quantize(from, transform.Position);
                Buildings[target] = building;
                return dealt;
            }

            /// <summary>Health restored by a heal of <paramref name="amount"/>, rebuilding broken sections first.</summary>
            private float Repair(Entity target, float amount)
            {
                if (!BuildingSections.TryGetBuffer(target, out var sections))
                {
                    return amount;
                }

                var building = Buildings[target];
                var restored = building.Repair(sections, amount);
                Buildings[target] = building;
                return restored;
            }

            /// <summary>A repairable building's health follows its sections and never drops to 0, so it is not destroyed.</summary>
            private float Resolved(Entity target, float health) =>
                Repairables.HasComponent(target) ? Repairable.Health(BuildingSections[target]) : health;
        }
    }
}
