using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>
    /// Gives every rendered part of a unit or wreck its body's half length as <see cref="SinkRadius"/>, so a hull
    /// longer than a crater is wide stands across it.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct SinkRadiusBakingSystem : ISystem
    {
        private EntityQuery _rendered;

        public void OnCreate(ref SystemState state)
        {
            _rendered = new EntityQueryBuilder(Allocator.Temp).WithAll<RenderFilterSettings>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var parents = SystemAPI.GetComponentLookup<Parent>(true);
            var agents = SystemAPI.GetComponentLookup<NavAgent>(true);
            var blockers = SystemAPI.GetComponentLookup<Blocker>(true);
            using var rendered = _rendered.ToEntityArray(Allocator.Temp);
            using var radii = new NativeList<float>(rendered.Length, Allocator.Temp);
            foreach (var entity in rendered)
            {
                radii.Add(BodyHalfLength(entity, parents, agents, blockers));
            }

            // Adding a component moves the entity, so measure first.
            var entityManager = state.EntityManager;
            for (var i = 0; i < rendered.Length; i++)
            {
                if (radii[i] > 0f)
                {
                    entityManager.AddComponentData(rendered[i], new SinkRadius { Value = radii[i] });
                }
            }
        }

        // The half length of the nearest unit or wreck at or above the entity; 0 when it is part of neither.
        private static float BodyHalfLength(Entity entity, ComponentLookup<Parent> parents,
            ComponentLookup<NavAgent> agents, ComponentLookup<Blocker> blockers)
        {
            while (true)
            {
                if (agents.TryGetComponent(entity, out var agent))
                {
                    return agent.HalfLength;
                }

                if (blockers.TryGetComponent(entity, out var blocker))
                {
                    return blocker.Radius + NavAgent.HalfSegmentFor(blocker.Radius, blocker.HullLength);
                }

                if (!parents.TryGetComponent(entity, out var parent))
                {
                    return 0f;
                }

                entity = parent.Value;
            }
        }
    }
}
