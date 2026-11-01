using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Turns the holed, pieces, stub, heap and broken section models, authored at scale 0 so editor views show the
    /// standing building, into full-size meshes hidden by <see cref="HideReason.Section"/>, which <see cref="SectionViewSystem"/> then flips.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateAfter(typeof(HiddenByBakingSystem))]
    public partial struct SectionsBakingSystem : ISystem
    {
        private EntityQuery _buildings;
        private EntityQuery _meshes;

        public void OnCreate(ref SystemState state)
        {
            const EntityQueryOptions options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities |
                                               EntityQueryOptions.IgnoreComponentEnabledState;
            _buildings = new EntityQueryBuilder(Allocator.Temp).WithAll<SectionModel>().WithOptions(options).Build(ref state);
            _meshes = new EntityQueryBuilder(Allocator.Temp).WithAll<MaterialMeshInfo, HiddenBy>().WithOptions(options)
                .Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var broken = ScaleUpBrokenPieces(ref state);
            if (broken.IsEmpty)
            {
                return;
            }

            var parents = SystemAPI.GetComponentLookup<Parent>(true);
            var em = state.EntityManager;
            using var meshes = _meshes.ToEntityArray(Allocator.Temp);
            foreach (var mesh in meshes)
            {
                if (IsUnder(mesh, broken, parents))
                {
                    var hidden = em.GetComponentData<HiddenBy>(mesh);
                    hidden.Set(HideReason.Section, true);
                    em.SetComponentData(mesh, hidden);
                    em.SetComponentEnabled<MaterialMeshInfo>(mesh, false);
                }
            }
        }

        /// <summary>Sets every building's hidden section models back to full scale and returns them.</summary>
        private NativeHashSet<Entity> ScaleUpBrokenPieces(ref SystemState state)
        {
            var transforms = SystemAPI.GetComponentLookup<LocalTransform>();
            var pieces = new NativeHashSet<Entity>(64, Allocator.Temp);
            using var buildings = _buildings.ToEntityArray(Allocator.Temp);
            foreach (var building in buildings)
            {
                foreach (var model in state.EntityManager.GetBuffer<SectionModel>(building, true))
                {
                    model.ForEachHidden(hidden =>
                    {
                        pieces.Add(hidden);
                        transforms.GetRefRW(hidden).ValueRW.Scale = 1f;
                    });
                }
            }

            return pieces;
        }

        private static bool IsUnder(Entity entity, NativeHashSet<Entity> branches, ComponentLookup<Parent> parents)
        {
            while (!branches.Contains(entity))
            {
                if (!parents.TryGetComponent(entity, out var parent))
                {
                    return false;
                }

                entity = parent.Value;
            }

            return true;
        }
    }
}
