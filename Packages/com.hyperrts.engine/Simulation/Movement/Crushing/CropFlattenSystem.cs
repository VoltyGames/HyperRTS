using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Movement.Crushing
{
    /// <summary>
    /// Crushers flatten the crop cover in the <see cref="SightBlockers"/> cells under their hull, so a vehicle's lane
    /// through a sunflower field opens a line of sight. Every world runs it, so clients' fog matches the server's.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct CropFlattenSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SightBlockers>();
            state.RequireForUpdate<Crusher>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new FlattenJob { Blockers = SystemAPI.GetSingletonRW<SightBlockers>().ValueRO }.Schedule();
        }

        /// <summary>Single-threaded: hulls overlap the same cells.</summary>
        [BurstCompile]
        [WithAll(typeof(Crusher))]
        [WithNone(typeof(Inside), typeof(Dead))]
        private partial struct FlattenJob : IJobEntity
        {
            public SightBlockers Blockers;

            private void Execute(in NavAgent agent, in LocalTransform transform)
            {
                if (agent.Layer == NavLayer.Air || agent.Layer == NavLayer.Naval)
                {
                    return;
                }

                var hull = new float2(agent.Radius * 2f, agent.HalfLength * 2f);
                Blockers.Flatten(new NavBox(transform.Position, transform.Rotation, hull));
            }
        }
    }
}
