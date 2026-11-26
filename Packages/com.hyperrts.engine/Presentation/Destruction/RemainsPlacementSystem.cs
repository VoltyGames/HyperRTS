using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Death;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Places the parts of fresh remains the frame they appear, so a ruin or wreck is drawn where the piece it replaces
    /// stood instead of a frame later: the command buffer that spawns them plays back after the transform systems.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(HullTiltSystem))]
    public partial struct RemainsPlacementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Unplaced>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            // Jobs may still write the transforms the lookups below read.
            state.CompleteDependency();
            var groups = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);
            var places = SystemAPI.GetComponentLookup<LocalToWorld>();
            var parts = new RemainsParts
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Stretches = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
                Parents = SystemAPI.GetComponentLookup<Parent>(true),
            };
            foreach (var (unplaced, entity) in SystemAPI.Query<EnabledRefRW<Unplaced>>().WithEntityAccess())
            {
                unplaced.ValueRW = false;
                if (!groups.TryGetBuffer(entity, out var group))
                {
                    continue;
                }

                foreach (var part in group)
                {
                    if (places.HasComponent(part.Value) && parts.Transforms.HasComponent(part.Value))
                    {
                        places[part.Value] = new LocalToWorld { Value = parts.World(part.Value) };
                    }
                }
            }
        }

        private struct RemainsParts
        {
            public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<PostTransformMatrix> Stretches;
            public ComponentLookup<Parent> Parents;

            /// <summary>Where a part stands by its own and its parents' transforms, as the transform systems place it.</summary>
            public readonly float4x4 World(Entity part)
            {
                var local = Transforms[part].ToMatrix();
                if (Stretches.TryGetComponent(part, out var stretch))
                {
                    local = math.mul(local, stretch.Value);
                }

                return Parents.TryGetComponent(part, out var parent) ? math.mul(World(parent.Value), local) : local;
            }
        }
    }
}
