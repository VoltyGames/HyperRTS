using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Turns broken pieces, authored at scale 0 so editor views show the standing building, into full-size meshes
    /// hidden by <see cref="HideReason.Part"/>, which <see cref="BreakablePartsSystem"/> then flips.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateAfter(typeof(HiddenByBakingSystem))]
    public partial struct BreakablePartsBakingSystem : ISystem
    {
        private EntityQuery _buildings;
        private EntityQuery _meshes;

        public void OnCreate(ref SystemState state)
        {
            const EntityQueryOptions options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities |
                                               EntityQueryOptions.IgnoreComponentEnabledState;
            _buildings = new EntityQueryBuilder(Allocator.Temp).WithAll<BreakablePart>().WithOptions(options).Build(ref state);
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
                    hidden.Set(HideReason.Part, true);
                    em.SetComponentData(mesh, hidden);
                    em.SetComponentEnabled<MaterialMeshInfo>(mesh, false);
                }
            }
        }

        /// <summary>Sets every building's broken pieces back to full scale and returns them.</summary>
        private NativeHashSet<Entity> ScaleUpBrokenPieces(ref SystemState state)
        {
            var transforms = SystemAPI.GetComponentLookup<LocalTransform>();
            var pieces = new NativeHashSet<Entity>(64, Allocator.Temp);
            using var buildings = _buildings.ToEntityArray(Allocator.Temp);
            foreach (var building in buildings)
            {
                foreach (var part in state.EntityManager.GetBuffer<BreakablePart>(building, true))
                {
                    pieces.Add(part.Broken);
                    transforms.GetRefRW(part.Broken).ValueRW.Scale = 1f;
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
