using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows the <see cref="ResourceStage"/> model matching how much of a resource node is left.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ResourceStageSystem : SystemBase
    {
        protected override void OnCreate() => RequireForUpdate<ShownResourceStage>();

        protected override void OnUpdate()
        {
            CompleteDependency();
            var transforms = SystemAPI.GetComponentLookup<LocalTransform>();
            foreach (var (node, stages, shown) in SystemAPI
                         .Query<RefRO<ResourceNode>, DynamicBuffer<ResourceStage>, RefRW<ShownResourceStage>>()
                         .WithChangeFilter<ResourceNode>())
            {
                var index = IModelStage.Pick(stages.AsNativeArray(), node.ValueRO.Remaining);
                if (index == shown.ValueRO.Index)
                {
                    continue;
                }

                shown.ValueRW.Index = index;
                IModelStage.Show(stages.AsNativeArray(), index, transforms);
            }
        }
    }
}
