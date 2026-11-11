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
            var squadOf = new NativeHashMap<Entity, FigurePart>(16, Allocator.Temp);
            using var squads = _squads.ToEntityArray(Allocator.Temp);
            foreach (var squad in squads)
            {
                em.GetBuffer<FigureRenderer>(squad).Clear();
                var figures = em.GetBuffer<SquadFigure>(squad);
                for (var i = 0; i < figures.Length; i++)
                {
                    var model = figures[i].Figure;
                    squadOf[model] = new FigurePart { Root = squad, Index = i, Model = model };
                    if (figures[i].HasOtherModel)
                    {
                        var other = figures[i].Other.Model.Figure;
                        squadOf[other] = new FigurePart { Root = squad, Index = i, Model = other };
                    }
                }
            }

            var drawn = new NativeHashSet<Entity>(16, Allocator.Temp);
            using var renderers = _renderers.ToEntityArray(Allocator.Temp);
            foreach (var renderer in renderers)
            {
                var link = renderer;
                while (link != Entity.Null)
                {
                    if (squadOf.TryGetValue(link, out var part))
                    {
                        em.GetBuffer<FigureRenderer>(part.Root).Add(new FigureRenderer { Model = link, Part = renderer });
                        em.AddComponentData(renderer, part);
                        drawn.Add(link);
                        break;
                    }

                    link = em.HasComponent<Parent>(link) ? em.GetComponentData<Parent>(link).Value : Entity.Null;
                }
            }

            foreach (var model in squadOf.GetKeyArray(Allocator.Temp))
            {
                if (!drawn.Contains(model))
                {
                    throw new System.InvalidOperationException(
                        $"[HyperRTS] Squad figure model '{em.GetName(model)}' has no renderer that plays clips, so it would stay frozen.");
                }
            }
        }
    }
}
