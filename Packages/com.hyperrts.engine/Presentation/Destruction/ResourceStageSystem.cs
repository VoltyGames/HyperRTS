using HyperRTS.Core;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows the <see cref="ResourceStage"/> model matching how much of a resource node is left (others scaled to
    /// zero, leaving fog's toggles alone).
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
                var index = ResourceStage.Pick(stages.AsNativeArray(), node.ValueRO.Remaining);
                if (index == shown.ValueRO.Index)
                {
                    continue;
                }

                shown.ValueRW.Index = index;
                for (var i = 0; i < stages.Length; i++)
                {
                    if (transforms.TryGetComponent(stages[i].Visual, out var visual))
                    {
                        visual.Scale = i == index ? 1f : 0f;
                        transforms[stages[i].Visual] = visual;
                    }
                }
            }
        }
    }
}
