using HyperRTS.Presentation.Animation.Bones;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Lists in each squad's <see cref="FigureRenderer"/> buffer the renderers at or under each of its figure models
    /// that play clips, after the extra material entities of multi-material renderers exist.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct SquadFigureRendererBakingSystem : ISystem
    {
        private const int MaxDepth = 8;

        private EntityQuery _squads;
        private EntityQuery _renderers;

        public void OnCreate(ref SystemState state)
        {
            const EntityQueryOptions options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities;
            _squads = new EntityQueryBuilder(Allocator.Temp).WithAll<SquadFigure, FigureRenderer>().WithOptions(options).Build(ref state);
            _renderers = new EntityQueryBuilder(Allocator.Temp).WithAll<Parent, LowerClip>().WithOptions(options).Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var squadOf = new NativeHashMap<Entity, Entity>(16, Allocator.Temp);
            using var squads = _squads.ToEntityArray(Allocator.Temp);
            foreach (var squad in squads)
            {
                em.GetBuffer<FigureRenderer>(squad).Clear();
                foreach (var figure in em.GetBuffer<SquadFigure>(squad))
                {
                    squadOf[figure.Figure] = squad;
                    if (figure.HasOtherModel)
                    {
                        squadOf[figure.Other.Model.Figure] = squad;
                    }
                }
            }

            using var renderers = _renderers.ToEntityArray(Allocator.Temp);
            foreach (var renderer in renderers)
            {
                var link = renderer;
                for (var step = 0; step < MaxDepth && link != Entity.Null; step++)
                {
                    if (squadOf.TryGetValue(link, out var squad))
                    {
                        em.GetBuffer<FigureRenderer>(squad).Add(new FigureRenderer { Model = link, Part = renderer });
                        break;
                    }

                    link = em.HasComponent<Parent>(link) ? em.GetComponentData<Parent>(link).Value : Entity.Null;
                }
            }
        }
    }
}
