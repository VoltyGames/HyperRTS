using HyperRTS.Core;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Rendering.Visibility;
using HyperRTS.Simulation.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows each <see cref="Section"/> standing or broken to match the building's broken mask, with the break
    /// effect and debris for breaks this client saw happen.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class SectionViewSystem : SystemBase
    {
        private EffectPool _pool;
        private DebrisSystem _debris;
        private RenderSwitch _switch;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _debris = World.GetOrCreateSystemManaged<DebrisSystem>();
            _switch = new RenderSwitch(ref CheckedStateRef);
            RequireForUpdate<ShownSections>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _switch.Update(ref CheckedStateRef);
            foreach (var (building, sections, models, shown, place, entity) in SystemAPI
                         .Query<RefRO<Sections>, DynamicBuffer<Section>, DynamicBuffer<SectionModel>,
                             RefRW<ShownSections>, RefRO<LocalToWorld>>().WithEntityAccess())
            {
                var now = building.ValueRO.Broken;
                var changed = now ^ shown.ValueRO.Broken;
                if (shown.ValueRO.Seen && changed == 0)
                {
                    continue;
                }

                // The first frame only sets the look; a loaded or unseen building doesn't replay its damage.
                var quiet = !shown.ValueRO.Seen;
                for (var i = 0; i < models.Length; i++)
                {
                    var bit = 1ul << i;
                    if (!quiet && (changed & bit) == 0)
                    {
                        continue;
                    }

                    var broken = (now & bit) != 0;
                    Show(entity, models[i], broken);
                    if (broken && !quiet)
                    {
                        Play(entity, shown.ValueRO, sections[i], place.ValueRO);
                    }
                }

                shown.ValueRW.Broken = now;
                shown.ValueRW.Seen = true;
            }
        }

        private void Show(Entity building, in SectionModel model, bool broken)
        {
            var pieces = new NativeList<Entity>(Allocator.Temp);
            _switch.Collect(building, pieces);
            _switch.SetBranch(pieces, model.Intact, HideReason.Section, broken);
            _switch.SetBranch(pieces, model.Broken, HideReason.Section, !broken);
        }

        /// <summary>The building's break effect and debris, from the section that broke.</summary>
        private void Play(Entity entity, in ShownSections shown, in Section section, in LocalToWorld place)
        {
            var position = math.transform(place.Value, section.Center);
            var outward = section.Outward;
            if (shown.Effect.IsValid())
            {
                var up = math.abs(outward.y) > 0.5f ? new float3(0f, 0f, 1f) : math.up();
                _pool.Play(shown.Effect.Value, position, math.mul(place.Rotation, quaternion.LookRotation(outward, up)));
            }

            if (SystemAPI.HasComponent<Debris>(entity))
            {
                _debris.Throw(SystemAPI.GetComponent<Debris>(entity), position, math.rotate(place.Rotation, outward));
            }
        }
    }
}
