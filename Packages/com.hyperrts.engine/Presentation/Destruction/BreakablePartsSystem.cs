using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>Breaks <see cref="BreakablePart"/>s to match health, by scale to leave fog's toggles alone.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BreakablePartsSystem : SystemBase
    {
        private EffectPool _pool;
        private ComponentLookup<LocalTransform> _transforms;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
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
            foreach (var (health, building, parts, shown, place) in SystemAPI
                         .Query<RefRO<Health>, RefRO<BreakableParts>, DynamicBuffer<BreakablePart>,
                             RefRW<ShownBreakage>, RefRO<LocalToWorld>>())
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
                    if (!quiet && building.ValueRO.Effect.IsValid())
                    {
                        Play(building.ValueRO.Effect.Value, part, place.ValueRO);
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
            foreach (var (building, shown, place) in SystemAPI
                         .Query<RefRO<BreakableParts>, RefRW<ShownBreakage>, RefRO<LocalToWorld>>())
            {
                var toLocal = math.inverse(place.ValueRO.Value);
                foreach (var cue in cues)
                {
                    var local = math.transform(toLocal, cue.Position);
                    if (cue.Slot == CueSlot.Impact && building.ValueRO.IsNear(local))
                    {
                        shown.ValueRW.LastHit = local;
                        shown.ValueRW.HasHit = true;
                    }
                }
            }
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

        private void Play(GameObject prefab, in BreakablePart part, in LocalToWorld place)
        {
            var rig = _pool.Get(prefab);
            if (rig != null)
            {
                var position = math.transform(place.Value, part.Centre);
                var rotation = math.mul(place.Rotation, part.EffectFacing);
                rig.Pick().Burst(position, rotation, 1f, EffectDetail.Density);
            }
        }
    }
}
