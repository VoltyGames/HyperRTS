using HyperRTS.Presentation.Environment.Weather;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.LitTeam
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

        public void OnCreate(ref SystemState state)
        {
            _levels = new MeshPainter<GrimeLevels>(ref state);
            _bands = new MeshPainter<GrimeBand>(ref state);
            state.RequireForUpdate<GroundMaterials>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<UnitTag, Grime>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _levels.Update(ref state);
            _bands.Update(ref state);
            var transforms = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var materials = SystemAPI.GetSingleton<GroundMaterials>();
            var wetness = SystemAPI.TryGetSingleton(out GroundWetness ground) ? ground.Level : 0f;
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
                if (first && math.distancesq(transforms[root].Position, position) > 0.01f)
                {
                    continue;
                }

                var moved = grime.ValueRW.Advance(position, materials.At(position.xz), wetness,
                    health.ValueRO.Fraction, deltaTime);
                if (first || moved)
                {
                    Repaint(ref state, root, ref grime.ValueRW, first, parts, commands);
                }
            }

            commands.Playback(state.EntityManager);
        }

        private void Repaint(ref SystemState state, Entity root, ref Grime grime, bool first, NativeList<Entity> parts,
            EntityCommandBuffer commands)
        {
            _levels.Collect(root, parts);
            var levels = new GrimeLevels { Value = grime.Paint() };
            if (first)
            {
                _levels.Add(parts, levels, commands);
                AddBands(ref state, root, grime, parts, commands);
            }
            else
            {
                _levels.Set(parts, levels);
            }
        }

        // Each mesh gets the ground height and the band in its own object space, as the unit stands when first seen.
        private void AddBands(ref SystemState state, Entity root, in Grime grime, NativeList<Entity> parts, EntityCommandBuffer commands)
        {
            var transforms = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var ground = transforms[root].Position;
            var one = new NativeList<Entity>(1, Allocator.Temp);
            foreach (var part in parts)
            {
                if (!transforms.TryGetComponent(part, out var mesh))
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
