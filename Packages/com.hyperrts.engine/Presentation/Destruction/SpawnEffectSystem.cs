using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Combat;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>Plays each <see cref="SpawnEffect"/> once, the first frame its entity (a ruin, fallen tree) is drawn.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SpawnEffectSystem : SystemBase
    {
        private EffectPool _pool;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<SpawnEffect>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var locals = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var parents = SystemAPI.GetComponentLookup<Parent>(true);
            foreach (var (effect, pending, place, entity) in SystemAPI
                         .Query<RefRO<SpawnEffect>, EnabledRefRW<SpawnEffect>, RefRO<LocalToWorld>>().WithEntityAccess())
            {
                // Waits a frame for the spawn's real pose, or the effect plays at the world origin.
                if (!EffectStreams.PoseCurrent(entity, place.ValueRO, locals, parents))
                {
                    continue;
                }

                pending.ValueRW = false;
                var rig = effect.ValueRO.Prefab.IsValid() ? _pool.Get(effect.ValueRO.Prefab.Value) : null;
                if (rig != null)
                {
                    rig.Pick().Burst(place.ValueRO.Position, place.ValueRO.Rotation, 1f, EffectDetail.Density);
                }
            }
        }
    }
}
