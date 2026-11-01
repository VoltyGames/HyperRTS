using HyperRTS.Core;
using HyperRTS.Presentation.Environment.Weather;
using HyperRTS.Presentation.Rendering.Visibility;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>
    /// Advances each unit's <see cref="Grime"/> from the ground it drove over, the water it crossed and the damage it
    /// took, and writes it to <see cref="GrimeLevels"/> on every mesh under it, once a level has moved noticeably.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct GrimeSystem : ISystem
    {
        private MeshPainter<GrimeLevels> _levels;
        private MeshPainter<GrimeBand> _bands;
        private ComponentLookup<LocalToWorld> _transforms;

        public void OnCreate(ref SystemState state)
        {
            _levels = new MeshPainter<GrimeLevels>(ref state);
            _bands = new MeshPainter<GrimeBand>(ref state);
            _transforms = state.GetComponentLookup<LocalToWorld>(true);
            state.RequireForUpdate<GroundMaterials>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<UnitTag, Grime>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _levels.Update(ref state);
            _bands.Update(ref state);
            _transforms.Update(ref state);
            var ground = SystemAPI.GetSingleton<GroundMaterials>();
            var wetness = Wetness.Level;
            var deltaTime = SystemAPI.Time.DeltaTime;
            var parts = new NativeList<Entity>(Allocator.Temp);
            var commands = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (grime, health, transform, root) in SystemAPI
                         .Query<RefRW<Grime>, RefRO<Health>, RefRO<LocalTransform>>().WithAll<UnitTag>()
                         .WithEntityAccess())
            {
                var first = !grime.ValueRO.Placed;
                var position = transform.ValueRO.Position;
                // A unit spawned this frame has no world transforms yet, which the bands are measured from.
                if (first && math.distancesq(_transforms[root].Position, position) > 0.01f)
                {
                    continue;
                }

                var moved = grime.ValueRW.Advance(position, ground.At(position.xz), wetness,
                    health.ValueRO.Fraction, deltaTime);
                if (first)
                {
                    _levels.Collect(root, parts);
                    _levels.Add(parts, new GrimeLevels { Value = grime.ValueRW.Paint() }, commands);
                    AddBands(root, grime.ValueRO, parts, commands);
                }
                else if (moved)
                {
                    _levels.Collect(root, parts);
                    _levels.Set(parts, new GrimeLevels { Value = grime.ValueRW.Paint() });
                }
            }

            commands.Playback(state.EntityManager);
        }

        // Each mesh gets the ground height and the band in its own object space, as the unit stands when first seen.
        private void AddBands(Entity root, in Grime grime, NativeList<Entity> parts, EntityCommandBuffer commands)
        {
            var ground = _transforms[root].Position;
            var one = new NativeList<Entity>(1, Allocator.Temp);
            foreach (var part in parts)
            {
                if (!_transforms.TryGetComponent(part, out var mesh))
                {
                    continue;
                }

                var height = math.transform(math.inverse(mesh.Value), ground).y;
                var scale = math.length(mesh.Value.c1.xyz);
                one.Clear();
                one.Add(part);
                var band = new float4(height, grime.MudHeight / scale, grime.DustHeight / scale, 0f);
                _bands.Add(one, new GrimeBand { Value = band }, commands);
            }
        }
    }
}
