using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Stretches the parts of remains spawned from a stretched prop, once, so they match what stood there.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct StretchPartsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<StretchParts, PartStretch>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var manager = state.EntityManager;
            foreach (var (parts, stretch, entity) in
                     SystemAPI.Query<RefRO<StretchParts>, RefRO<PartStretch>>().WithEntityAccess())
            {
                Extend(manager, ecb, parts.ValueRO, stretch.ValueRO.Extension);
                ecb.RemoveComponent<StretchParts>(entity);
            }
        }

        /// <summary>The span's stretch goes in its <see cref="PostTransformMatrix"/>: <see cref="LocalTransform"/> scales uniformly.</summary>
        private static void Extend(EntityManager manager, EntityCommandBuffer ecb, in StretchParts parts, float extension)
        {
            var layout = parts.Layout;
            var start = manager.GetComponentData<LocalTransform>(parts.StartCap);
            ecb.SetComponent(parts.StartCap, start.WithPosition(layout.StartCapAt(extension)));
            var end = manager.GetComponentData<LocalTransform>(parts.EndCap);
            ecb.SetComponent(parts.EndCap, end.WithPosition(layout.EndCapAt(extension)));
            var span = manager.GetComponentData<LocalTransform>(parts.Span);
            ecb.SetComponent(parts.Span, span.WithPosition(layout.SpanPositionAt(extension)).WithScale(1f));
            ecb.AddComponent(parts.Span, new PostTransformMatrix { Value = float4x4.Scale(layout.SpanScaleAt(extension)) });
        }
    }
}
