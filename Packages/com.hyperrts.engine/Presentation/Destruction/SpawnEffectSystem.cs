using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>Plays each <see cref="SpawnEffect"/> once, the first frame its entity (a ruin, fallen tree) is drawn.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SpawnEffectSystem : SystemBase
    {
        protected override void OnCreate() => RequireForUpdate<SpawnEffect>();

        protected override void OnUpdate()
        {
            CompleteDependency();
            foreach (var (effect, pending, place) in SystemAPI
                         .Query<RefRO<SpawnEffect>, EnabledRefRW<SpawnEffect>, RefRO<LocalToWorld>>())
            {
                pending.ValueRW = false;
                if (effect.ValueRO.Prefab.IsValid())
                {
                    Object.Instantiate(effect.ValueRO.Prefab.Value, place.ValueRO.Position, place.ValueRO.Rotation);
                }
            }
        }
    }
}
