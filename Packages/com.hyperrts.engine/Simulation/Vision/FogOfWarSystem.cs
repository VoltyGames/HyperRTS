using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Creates the <see cref="FogOfWar"/> grid from <see cref="MapSettings"/> (again when the map's area changes) and
    /// restamps every team's vision (occluded by hills and <see cref="SightBlockers"/>) and detection a few times per
    /// second. With fog disabled the grid stays fully visible, but detection still runs.
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
        private MapSettings _builtMap;

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
                Dispose(fog.ValueRO);
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<MapSettings>();
            var rebuilt = EnsureGrid(ref state, settings);

            // Toggling fog restamps at once, so nothing reads a grid stamped under the old setting.
            var elapsed = SystemAPI.Time.ElapsedTime;
            if (!rebuilt && elapsed < _nextUpdate && settings.FogOfWar == _stampedWithFog)
            {
                return;
            }

            _nextUpdate = elapsed + UpdateInterval;
            _stampedWithFog = settings.FogOfWar;
            ref var fog = ref SystemAPI.GetSingletonRW<FogOfWar>().ValueRW;
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            fog.Version++;

            // Stealth applies with fog off too, so detection is restamped either way.
            state.Dependency = new ClearCellsJob { Cells = fog.Detected }.Schedule(state.Dependency);
            state.Dependency = new ClearCellsJob { Cells = fog.Close }.Schedule(state.Dependency);
            new DetectionStampJob { Fog = fog, Relations = relations }.Schedule();
            SystemAPI.TryGetSingleton<SightRules>(out var rules);
            new CloseStampJob
            {
                Fog = fog, Relations = relations, Radius = rules.RevealDistance,
                Agents = SystemAPI.GetComponentLookup<NavAgent>(true),
                Inside = SystemAPI.GetComponentLookup<Inside>(true),
            }.Schedule();
            if (settings.FogOfWar)
            {
                _revealed = false;
                StampVision(ref state, fog, relations, settings);
                return;
            }

            // Nothing else writes the grid while fog is off, so one fill lasts until fog comes back on.
            if (!_revealed)
            {
                _revealed = true;
                state.Dependency = new RevealAllJob
                {
                    Visible = fog.Visible, VisibleTall = fog.VisibleTall, Explored = fog.Explored,
                }.Schedule(fog.Visible.Length, 1024, state.Dependency);
            }
        }

        private void StampVision(ref SystemState state, in FogOfWar fog, in FactionRelations relations,
            in MapSettings settings)
        {
            state.Dependency = new ClearCellsJob { Cells = fog.Visible }.Schedule(state.Dependency);
            state.Dependency = new ClearCellsJob { Cells = fog.VisibleTall }.Schedule(state.Dependency);
            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            SystemAPI.TryGetSingleton<SightBlockers>(out var blockers);
            SystemAPI.TryGetSingleton<HighGround>(out var highGround);
            new SightStampJob
            {
                Fog = fog,
                Relations = relations,
                Terrain = terrain,
                Blockers = blockers,
                HighGround = highGround,
                WaterFloor = settings.FloodTerrain ? settings.WaterLevel : float.NegativeInfinity,
                Inside = SystemAPI.GetComponentLookup<Inside>(true),
                Blocks = SystemAPI.GetComponentLookup<SightBlock>(true),
                Eyes = SystemAPI.GetComponentLookup<PassengerEye>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            }.Schedule();
            state.Dependency = new ExploreJob { Visible = fog.VisibleTall, Explored = fog.Explored }
                .Schedule(fog.Explored.Length, 1024, state.Dependency);
        }

        /// <summary>Creates the grid, or replaces it when the map's area or fog cell size changed; true if it did.</summary>
        private bool EnsureGrid(ref SystemState state, in MapSettings settings)
        {
            // Read-only check: write access would complete every job reading the fog, every frame.
            var exists = SystemAPI.HasSingleton<FogOfWar>();
            if (exists && settings.SameFogGrid(_builtMap))
            {
                return false;
            }

            _builtMap = settings;
            _revealed = false;
            var grid = CreateGrid(settings);
            if (!exists)
            {
                state.EntityManager.CreateSingleton(grid);
                return true;
            }

            state.CompleteDependency();
            ref var fog = ref SystemAPI.GetSingletonRW<FogOfWar>().ValueRW;
            grid.Version = fog.Version + 1;
            Dispose(fog);
            fog = grid;
            return true;
        }

        private static void Dispose(in FogOfWar fog)
        {
            fog.Visible.Dispose();
            fog.VisibleTall.Dispose();
            fog.Explored.Dispose();
            fog.Detected.Dispose();
            fog.Close.Dispose();
        }

        private static FogOfWar CreateGrid(in MapSettings settings)
        {
            settings.FogLayout(out var size, out var cellSize);
            var count = size.x * size.y;

            return new FogOfWar
            {
                Visible = new NativeArray<ushort>(count, Allocator.Persistent),
                VisibleTall = new NativeArray<ushort>(count, Allocator.Persistent),
                Explored = new NativeArray<ushort>(count, Allocator.Persistent),
                Detected = new NativeArray<ushort>(count, Allocator.Persistent),
                Close = new NativeArray<ushort>(count, Allocator.Persistent),
                Size = size,
                Min = settings.Min,
                CellSize = cellSize,
                Version = 1,
            };
        }

        [BurstCompile]
        private struct RevealAllJob : IJobParallelFor
        {
            public NativeArray<ushort> Visible;
            public NativeArray<ushort> VisibleTall;
            public NativeArray<ushort> Explored;

            public void Execute(int index)
            {
                Visible[index] = ushort.MaxValue;
                VisibleTall[index] = ushort.MaxValue;
                Explored[index] = ushort.MaxValue;
            }
        }

        [BurstCompile]
        private struct ExploreJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ushort> Visible;
            public NativeArray<ushort> Explored;

            public void Execute(int index) => Explored[index] |= Visible[index];
        }
    }
}
