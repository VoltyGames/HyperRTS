using HyperRTS.Core;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Construction
{
    /// <summary>
    /// Copies a building's <see cref="ConstructionProgress"/> to <see cref="BuildProgress"/> on every mesh under it
    /// while it is under construction, and takes it off again once complete, so finished buildings draw as authored.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct BuildRevealSystem : ISystem
    {
        private EntityQuery _finished;
        private MeshPainter<BuildProgress> _painter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _finished = SystemAPI.QueryBuilder().WithAll<BuildRevealing>().WithDisabled<ConstructionProgress>().Build();
            _painter = new MeshPainter<BuildProgress>(ref state);
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithPresent<ConstructionProgress>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _painter.Update(ref state);
            var parts = new NativeList<Entity>(Allocator.Temp);
            var commands = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (progress, root) in SystemAPI.Query<RefRO<ConstructionProgress>>().WithNone<BuildRevealing>()
                         .WithEntityAccess())
            {
                _painter.Collect(root, parts);
                _painter.Add(parts, new BuildProgress { Value = progress.ValueRO.Value }, commands);
                commands.AddComponent<BuildRevealing>(root);
            }

            foreach (var root in _finished.ToEntityArray(Allocator.Temp))
            {
                _painter.Collect(root, parts);
                _painter.Remove(parts, commands);
                commands.RemoveComponent<BuildRevealing>(root);
            }

            commands.Playback(state.EntityManager);
            // After playback, so the painter sees the meshes that just got the property.
            _painter.Update(ref state);
            foreach (var (progress, root) in SystemAPI.Query<RefRO<ConstructionProgress>>().WithAll<BuildRevealing>()
                         .WithChangeFilter<ConstructionProgress>().WithEntityAccess())
            {
                _painter.Collect(root, parts);
                _painter.Set(parts, new BuildProgress { Value = progress.ValueRO.Value });
            }
        }
    }
}
