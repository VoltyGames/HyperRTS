using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Creates the <see cref="FogOfWar"/> grid from <see cref="MapSettings"/> and restamps every team's vision
    /// (occluded by hills when there is a <see cref="TerrainHeight"/>) and detection a few times per second. With fog
    /// disabled the grid stays fully visible, but detection still runs.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    public partial struct FogOfWarSystem : ISystem
    {
        /// <summary>Seconds between restamps; vision needn't track movement every frame.</summary>
        public const float UpdateInterval = 0.1f;

        private double _nextUpdate;
        private bool _stampedWithFog;
        private bool _revealed;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<FactionRelations>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingletonRW<FogOfWar>(out var fog))
            {
                fog.ValueRW.Visible.Dispose();
                fog.ValueRW.Explored.Dispose();
                fog.ValueRW.Detected.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<MapSettings>();
            if (!SystemAPI.HasSingleton<FogOfWar>())
            {
                state.EntityManager.CreateSingleton(CreateGrid(settings));
            }

            // Toggling fog restamps at once, so nothing reads a grid stamped under the old setting.
            var elapsed = SystemAPI.Time.ElapsedTime;
            if (elapsed < _nextUpdate && settings.FogOfWar == _stampedWithFog)
            {
                return;
            }

            _nextUpdate = elapsed + UpdateInterval;
            _stampedWithFog = settings.FogOfWar;
            ref var fog = ref SystemAPI.GetSingletonRW<FogOfWar>().ValueRW;
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            fog.Version++;

            // Stealth applies with fog off too, so detection is restamped either way.
            state.Dependency = new ClearBytesJob { Cells = fog.Detected }.Schedule(state.Dependency);
            new DetectionStampJob { Fog = fog, Relations = relations }.Schedule();
            if (!settings.FogOfWar)
            {
                // Nothing else writes the grid while fog is off, so one fill lasts until fog comes back on.
                if (!_revealed)
                {
                    _revealed = true;
                    state.Dependency = new RevealAllJob { Visible = fog.Visible, Explored = fog.Explored }
                        .Schedule(fog.Visible.Length, 1024, state.Dependency);
                }

                return;
            }

            _revealed = false;
            state.Dependency = new ClearBytesJob { Cells = fog.Visible }.Schedule(state.Dependency);
            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            new StampJob
            {
                Fog = fog,
                Relations = relations,
                Terrain = terrain,
                WaterLevel = settings.WaterLevel,
            }.Schedule();
            state.Dependency = new ExploreJob { Visible = fog.Visible, Explored = fog.Explored }
                .Schedule(fog.Explored.Length, 1024, state.Dependency);
        }

        private static FogOfWar CreateGrid(in MapSettings settings)
        {
            var cellSize = math.max(settings.FogCellSize, 0.1f);
            var size = math.max((int2)math.ceil(settings.Size / cellSize), 1);
            var count = size.x * size.y;

            return new FogOfWar
            {
                Visible = new NativeArray<byte>(count, Allocator.Persistent),
                Explored = new NativeArray<byte>(count, Allocator.Persistent),
                Detected = new NativeArray<byte>(count, Allocator.Persistent),
                Size = size,
                Min = settings.Min,
                CellSize = cellSize,
                Version = 1,
            };
        }

        [BurstCompile]
        private struct RevealAllJob : IJobParallelFor
        {
            public NativeArray<byte> Visible;
            public NativeArray<byte> Explored;

            public void Execute(int index)
            {
                Visible[index] = byte.MaxValue;
                Explored[index] = byte.MaxValue;
            }
        }

        [BurstCompile]
        private struct ExploreJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Visible;
            public NativeArray<byte> Explored;

            public void Execute(int index) => Explored[index] |= Visible[index];
        }

        /// <summary>Single-threaded because overlapping sight circles OR into the same cells.</summary>
        [BurstCompile]
        private partial struct StampJob : IJobEntity
        {
            public FogOfWar Fog;
            public FactionRelations Relations;
            public TerrainHeight Terrain;
            public float WaterLevel;

            private void Execute(in LocalTransform transform, in VisionRange vision, in Faction faction)
            {
                if (vision.Value <= 0f || !FogOfWar.TryGetTeamBit(Relations.TeamOf(faction.Value), out var bit))
                {
                    return;
                }

                if (Terrain.IsCreated)
                {
                    var sight = new SightLines { Fog = Fog, Terrain = Terrain, WaterLevel = WaterLevel };
                    sight.Stamp(transform.Position, vision.Value, bit);
                    return;
                }

                Fog.StampCircle(Fog.Visible, transform.Position, vision.Value, bit);
            }
        }
    }
}
