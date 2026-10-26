using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Combat;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Plays each <see cref="SpawnEffect"/> once, the first frame its entity (a ruin) is drawn. A falling tree plays
    /// it where its canopy lands, as it lands.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SpawnEffectSystem : SystemBase
    {
        private EffectPool _pool;

        // The canopy's mass sits a little past the middle of the trunk.
        private const float CanopyShare = 0.6f;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<SpawnEffect>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            foreach (var (effect, pending, place, entity) in SystemAPI
                         .Query<RefRO<SpawnEffect>, EnabledRefRW<SpawnEffect>, RefRO<LocalToWorld>>().WithEntityAccess())
            {
                if (Falling(entity))
                {
                    continue;
                }

                pending.ValueRW = false;
                var prefab = effect.ValueRO.Prefab.IsValid() ? effect.ValueRO.Prefab.Value : null;
                var position = place.ValueRO.Position;
                var rotation = place.ValueRO.Rotation;
                if (SystemAPI.HasComponent<ToppleFall>(entity))
                {
                    var toppleFall = SystemAPI.GetComponent<ToppleFall>(entity);
                    position += toppleFall.CanopyDirection * toppleFall.Height * CanopyShare;
                    rotation = quaternion.LookRotationSafe(toppleFall.CanopyDirection, math.up());
                }

                _pool.Play(prefab, position, rotation);
            }
        }

        private bool Falling(Entity entity)
        {
            if (!SystemAPI.HasComponent<ToppleFall>(entity))
            {
                return false;
            }

            // ToppleSystem clocks the fall from the frame after the remains spawn.
            if (!SystemAPI.HasComponent<FallClock>(entity))
            {
                return true;
            }

            var clocked = SystemAPI.IsComponentEnabled<FallClock>(entity);
            return clocked && !SystemAPI.GetComponent<FallClock>(entity).Landed;
        }
    }
}
