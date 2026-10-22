using HyperRTS.Core;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Resources;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows the <see cref="ResourceStage"/> model matching how much of a resource node is left.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    [BurstCompile]
    public partial struct ResourceStageSystem : ISystem
    {
        private RenderSwitch _switch;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _switch = new RenderSwitch(ref state);
            state.RequireForUpdate<ShownResourceStage>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _switch.Update(ref state);
            foreach (var (node, stages, shown, entity) in SystemAPI
                         .Query<RefRO<ResourceNode>, DynamicBuffer<ResourceStage>, RefRW<ShownResourceStage>>()
                         .WithChangeFilter<ResourceNode>().WithEntityAccess())
            {
                var index = IModelStage.Pick(stages.AsNativeArray(), node.ValueRO.Remaining);
                if (index == shown.ValueRO.Index)
                {
                    continue;
                }

                shown.ValueRW.Index = index;
                _switch.ShowStage(entity, stages.AsNativeArray(), index);
            }
        }
    }
}
