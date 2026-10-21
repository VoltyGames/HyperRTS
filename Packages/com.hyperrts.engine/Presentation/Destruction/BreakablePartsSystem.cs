using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>Breaks <see cref="BreakablePart"/>s to match health, by scale to leave fog's toggles alone.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BreakablePartsSystem : SystemBase
    {
        private EffectPool _pool;
        private DebrisSystem _debris;
        private ComponentLookup<LocalTransform> _transforms;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _debris = World.GetOrCreateSystemManaged<DebrisSystem>();
            RequireForUpdate<ShownBreakage>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            if (SystemAPI.TryGetSingletonBuffer<CueEvent>(out var cues, true) && !cues.IsEmpty)
            {
                RecordHits(cues);
            }

            _transforms = SystemAPI.GetComponentLookup<LocalTransform>();
            foreach (var (health, building, parts, shown, place, entity) in SystemAPI
                         .Query<RefRO<Health>, RefRO<BreakableParts>, DynamicBuffer<BreakablePart>,
                             RefRW<ShownBreakage>, RefRO<LocalToWorld>>().WithEntityAccess())
            {
                // The first frame only sets the look; a loaded or unseen building doesn't replay its damage.
                var quiet = !shown.ValueRO.Seen;
                if (quiet)
                {
                    foreach (var part in parts)
                    {
                        Show(part);
                    }

                    shown.ValueRW.Seen = true;
                }

                var target = building.ValueRO.TargetBroken(parts.Length, health.ValueRO.Fraction);
                while (shown.ValueRO.Broken < target)
                {
                    var part = Break(parts, ref shown.ValueRW);
                    if (!quiet)
                    {
                        Play(entity, building.ValueRO, part, place.ValueRO);
                    }
                }

                while (shown.ValueRO.Broken > target)
                {
                    Restore(parts, ref shown.ValueRW);
                }
            }
        }

        private void RecordHits(DynamicBuffer<CueEvent> cues)
        {
            if (!HasImpact(cues))
            {
                return;
            }

            foreach (var (building, shown, place) in SystemAPI
                         .Query<RefRO<BreakableParts>, RefRW<ShownBreakage>, RefRO<LocalToWorld>>())
            {
                var toLocal = math.inverse(place.ValueRO.Value);
                foreach (var cue in cues)
                {
                    if (cue.Slot != CueSlot.Impact)
                    {
                        continue;
                    }

                    var local = math.transform(toLocal, cue.Position);
                    if (building.ValueRO.IsNear(local))
                    {
                        shown.ValueRW.LastHit = local;
                        shown.ValueRW.HasHit = true;
                    }
                }
            }
        }

        private static bool HasImpact(DynamicBuffer<CueEvent> cues)
        {
            foreach (var cue in cues)
            {
                if (cue.Slot == CueSlot.Impact)
                {
                    return true;
                }
            }

            return false;
        }

        private BreakablePart Break(DynamicBuffer<BreakablePart> parts, ref ShownBreakage shown)
        {
            var next = shown.NextToBreak(parts.AsNativeArray());
            var part = parts[next];
            part.Order = ++shown.Broken;
            parts[next] = part;
            Show(part);
            return part;
        }

        private void Restore(DynamicBuffer<BreakablePart> parts, ref ShownBreakage shown)
        {
            var latest = shown.LatestBroken(parts.AsNativeArray());
            var part = parts[latest];
            part.Order = 0;
            parts[latest] = part;
            shown.Broken--;
            Show(part);
        }

        private void Show(in BreakablePart part)
        {
            var broken = part.Order > 0;
            SetScale(part.Intact, broken ? 0f : 1f);
            SetScale(part.Broken, broken ? 1f : 0f);
        }

        private void SetScale(Entity entity, float scale)
        {
            if (_transforms.TryGetComponent(entity, out var transform))
            {
                transform.Scale = scale;
                _transforms[entity] = transform;
            }
        }

        /// <summary>The building's break effect and debris, from the part that broke.</summary>
        private void Play(Entity entity, in BreakableParts building, in BreakablePart part, in LocalToWorld place)
        {
            var position = math.transform(place.Value, part.Centre);
            if (building.Effect.IsValid())
            {
                _pool.Play(building.Effect.Value, position, math.mul(place.Rotation, part.EffectFacing));
            }

            if (SystemAPI.HasComponent<Debris>(entity))
            {
                _debris.Throw(SystemAPI.GetComponent<Debris>(entity), position, math.rotate(place.Rotation, part.Outward));
            }
        }
    }
}
