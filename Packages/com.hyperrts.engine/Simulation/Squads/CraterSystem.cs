using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Digs a <see cref="Crater"/> cover entity where an impact cue lands on open ground with a splash radius of at least
    /// <see cref="CraterRules.MinRadius"/>, clear of other craters and cover spots, and removes the oldest past the cap.
    /// Only shooters that leave impact cues dig. The server alone spawns them: clients draw craters from the cues.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ImpactSurfaceSystem))]
    public partial struct CraterSystem : ISystem
    {
        private EntityQuery _craters;
        private uint _dug;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _craters = SystemAPI.QueryBuilder().WithAll<Crater, LocalTransform>().Build();
            state.RequireForUpdate<CraterRules>();
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<CoverSpots>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rules = SystemAPI.GetSingleton<CraterRules>();
            var points = new NativeList<float3>(Allocator.Temp);
            foreach (var cue in SystemAPI.GetSingletonBuffer<CueEvent>(true))
            {
                if (cue.IsGroundImpact && cue.Radius >= rules.MinRadius)
                {
                    points.Add(cue.Position);
                }
            }

            if (points.IsEmpty)
            {
                return;
            }

            var standing = Standing();
            var spots = SystemAPI.GetSingleton<CoverSpots>();
            var near = new NativeList<int>(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var point in points)
            {
                near.Clear();
                spots.FindNear(point.xz, rules.Spacing, near);
                if (near.IsEmpty && !NearCrater(standing, point.xz, rules.Spacing))
                {
                    Dig(ecb, rules, point, standing);
                }
            }

            ecb.Playback(state.EntityManager);
        }

        private NativeList<Dug> Standing()
        {
            var entities = _craters.ToEntityArray(Allocator.Temp);
            var craters = _craters.ToComponentDataArray<Crater>(Allocator.Temp);
            var places = _craters.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var standing = new NativeList<Dug>(entities.Length + 1, Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                standing.Add(new Dug { Entity = entities[i], Order = craters[i].Order, Position = places[i].Position.xz });
            }

            return standing;
        }

        private static bool NearCrater(NativeList<Dug> standing, float2 point, float spacing)
        {
            foreach (var crater in standing)
            {
                if (math.distancesq(crater.Position, point) <= spacing * spacing)
                {
                    return true;
                }
            }

            return false;
        }

        private void Dig(EntityCommandBuffer ecb, in CraterRules rules, float3 point, NativeList<Dug> standing)
        {
            if (standing.Length >= rules.MaxCount)
            {
                RemoveOldest(ecb, standing);
            }

            var crater = ecb.Instantiate(rules.Prefab);
            ecb.SetComponent(crater, LocalTransform.FromPosition(point));
            ecb.AddComponent(crater, new Crater { Order = ++_dug });
            standing.Add(new Dug { Entity = crater, Order = _dug, Position = point.xz });
        }

        private static void RemoveOldest(EntityCommandBuffer ecb, NativeList<Dug> standing)
        {
            var oldest = 0;
            for (var i = 1; i < standing.Length; i++)
            {
                if (standing[i].Order < standing[oldest].Order)
                {
                    oldest = i;
                }
            }

            ecb.DestroyEntity(standing[oldest].Entity);
            standing.RemoveAtSwapBack(oldest);
        }

        private struct Dug
        {
            public Entity Entity;
            public uint Order;
            public float2 Position;
        }
    }
}
