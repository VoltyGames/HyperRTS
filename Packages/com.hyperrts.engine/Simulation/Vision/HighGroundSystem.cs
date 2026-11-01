using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
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
        private MapSettings _builtMap;
        private TerrainHeight _builtTerrain;
        private SightRules _builtRules;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
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
            map.FogLayout(out var size, out var cellSize);
            var ground = new HighGround
            {
                Rise = new NativeArray<float>(size.x * size.y, Allocator.Persistent),
                Size = size,
                Min = map.Min,
                CellSize = cellSize,
                Rules = rules,
            };
            Replace(ref state, ground, exists);
            var radius = (int)math.round(rules.Radius / cellSize);
            state.Dependency = new RiseJob { Ground = ground, Terrain = terrain, Radius = radius }
                .Schedule(state.Dependency);
        }

        private void Replace(ref SystemState state, in HighGround ground, bool exists)
        {
            if (!exists)
            {
                state.EntityManager.CreateSingleton(ground, "HighGround");
                return;
            }

            state.CompleteDependency();
            ref var current = ref SystemAPI.GetSingletonRW<HighGround>().ValueRW;
            current.Rise.Dispose();
            current = ground;
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
                var size = Ground.Size;
                var sums = new NativeArray<double>((size.x + 1) * (size.y + 1), Allocator.Temp);
                for (var y = 0; y < size.y; y++)
                {
                    for (var x = 0; x < size.x; x++)
                    {
                        var height = Terrain.Height(Ground.Min + (new float2(x, y) + 0.5f) * Ground.CellSize);
                        Ground.Rise[y * size.x + x] = height;
                        sums[Sum(x + 1, y + 1)] = height + sums[Sum(x, y + 1)] + sums[Sum(x + 1, y)] - sums[Sum(x, y)];
                    }
                }

                for (var y = 0; y < size.y; y++)
                {
                    for (var x = 0; x < size.x; x++)
                    {
                        var low = math.max(new int2(x, y) - Radius, 0);
                        var high = math.min(new int2(x, y) + Radius + 1, size);
                        var total = sums[Sum(high.x, high.y)] - sums[Sum(low.x, high.y)] - sums[Sum(high.x, low.y)] +
                                    sums[Sum(low.x, low.y)];
                        var count = (high.x - low.x) * (high.y - low.y);
                        Ground.Rise[y * size.x + x] -= (float)(total / count);
                    }
                }
            }

            private readonly int Sum(int x, int y) => y * (Ground.Size.x + 1) + x;
        }
    }
}
