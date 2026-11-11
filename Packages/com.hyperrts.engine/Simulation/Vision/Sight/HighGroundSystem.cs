using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>
    /// Builds <see cref="HighGround"/> on the fog grid from the terrain and the match's <see cref="SightRules"/>,
    /// again whenever the map, its terrain or the rules change. Without rules there is no high ground.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FogOfWarSystem))]
    public partial struct HighGroundSystem : ISystem
    {
        private static readonly ProfilerMarker RiseMarker = new("HighGroundSystem.Rise");

        private EntityQuery _ground;
        private MapSettings _builtMap;
        private TerrainHeight _builtTerrain;
        private SightRules _builtRules;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ground = SystemAPI.QueryBuilder().WithAllRW<HighGround>().Build();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<SightRules>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingleton<HighGround>(out var ground))
            {
                ground.Rise.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var map = SystemAPI.GetSingleton<MapSettings>();
            var rules = SystemAPI.GetSingleton<SightRules>();
            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            var exists = SystemAPI.HasSingleton<HighGround>();
            var same = map.SameFogGrid(_builtMap) && terrain.Blob.Equals(_builtTerrain.Blob) && Same(rules, _builtRules);
            if (exists && same)
            {
                return;
            }

            _builtMap = map;
            _builtTerrain = terrain;
            _builtRules = rules;
            var grid = map.FogGrid();
            var ground = new HighGround
            {
                Rise = new NativeArray<float>(grid.Count, Allocator.Persistent),
                Grid = grid,
                Rules = rules,
            };
            state.ReplaceSingleton(_ground, ref ground, "HighGround");
            var radius = (int)math.round(rules.Radius / grid.CellSize);
            state.Dependency = new RiseJob { Ground = ground, Terrain = terrain, Radius = radius }
                .Schedule(state.Dependency);
        }

        private static bool Same(in SightRules a, in SightRules b) =>
            a.Radius == b.Radius && a.SightPerMetre == b.SightPerMetre && a.MaxSight == b.MaxSight;

        /// <summary>Ground at every cell centre less the mean over the square of cells within the radius, by summed areas.</summary>
        [BurstCompile]
        private struct RiseJob : IJob
        {
            public HighGround Ground;
            public TerrainHeight Terrain;
            public int Radius;

            public void Execute()
            {
                using var marker = RiseMarker.Auto();
                var grid = Ground.Grid;
                var size = grid.Size;
                var sums = new NativeArray<double>((size.x + 1) * (size.y + 1), Allocator.Temp);
                foreach (var cell in grid.Cells(int2.zero, size - 1))
                {
                    var height = Terrain.Height(grid.Center(cell));
                    Ground.Rise[grid.Index(cell)] = height;
                    sums[Sum(cell + 1)] = height + sums[Sum(cell + new int2(0, 1))] + sums[Sum(cell + new int2(1, 0))] -
                                          sums[Sum(cell)];
                }

                foreach (var cell in grid.Cells(int2.zero, size - 1))
                {
                    var low = math.max(cell - Radius, 0);
                    var high = math.min(cell + Radius + 1, size);
                    var total = sums[Sum(high)] - sums[Sum(new int2(low.x, high.y))] -
                                sums[Sum(new int2(high.x, low.y))] + sums[Sum(low)];
                    var count = (high.x - low.x) * (high.y - low.y);
                    Ground.Rise[grid.Index(cell)] -= (float)(total / count);
                }
            }

            private readonly int Sum(int2 corner) => corner.y * (Ground.Grid.Size.x + 1) + corner.x;
        }
    }
}
