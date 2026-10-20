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
            foreach (var (effect, pending, place) in SystemAPI
                         .Query<RefRO<SpawnEffect>, EnabledRefRW<SpawnEffect>, RefRO<LocalToWorld>>())
            {
                pending.ValueRW = false;
                var prefab = effect.ValueRO.Prefab.IsValid() ? effect.ValueRO.Prefab.Value : null;
                _pool.Play(prefab, place.ValueRO.Position, place.ValueRO.Rotation);
            }
        }
    }
}
