using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common.Infantry;
using System;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Squads.Cover
{
    /// <summary>One <see cref="CoverSpot"/> in world space.</summary>
    public struct WorldCoverSpot
    {
        public float3 Position;

        /// <summary>Unit direction on the XZ plane a soldier in the spot faces.</summary>
        public float2 Facing;

        public CoverKind Kind;

        /// <summary>Metres the spot lies below its prop's origin, such as the floor of a trench.</summary>
        public float Drop;

        /// <summary>The prop it is on and its index in that prop's <see cref="CoverSpot"/> buffer.</summary>
        public Entity Entity;

        public byte Index;
    }

    /// <summary>Names one spot of one prop; it still means the same spot after the index is rebuilt.</summary>
    public readonly struct CoverSpotKey : IEquatable<CoverSpotKey>
    {
        public readonly Entity Entity;
        public readonly byte Index;

        public CoverSpotKey(Entity entity, byte index)
        {
            Entity = entity;
            Index = index;
        }

        public bool Equals(CoverSpotKey other) => Entity == other.Entity && Index == other.Index;

        public override bool Equals(object obj) => obj is CoverSpotKey other && Equals(other);

        public override int GetHashCode() => Entity.GetHashCode() * 31 + Index;
    }

    /// <summary>
    /// Singleton: every <see cref="CoverSpot"/> in a uniform grid over the map, with the <see cref="Reservation"/> of the member using it.
    /// <see cref="CoverSpotSystem"/> rebuilds it only when cover appears, vanishes or moves.
    /// </summary>
    public struct CoverSpots : IComponentData
    {
        public const float CellSize = 4f;

        /// <summary>Spots by cell, so each cell's spots sit together from <see cref="CellStart"/>.</summary>
        public NativeArray<WorldCoverSpot> Spots;

        public NativeArray<Reservation> Reservations;

        /// <summary>First spot of each cell; one more entry than cells, so a cell's spots end where the next begins.</summary>
        public NativeArray<int> CellStart;

        public NativeHashMap<CoverSpotKey, int> ByKey;

        public CellGrid Grid;

        /// <summary>Counts the builds, so a test can tell whether one ran.</summary>
        public int Version;

        public readonly bool IsCreated => Spots.IsCreated;

        public static CoverSpots Create(in MapSettings map, NativeArray<WorldCoverSpot> unsorted, int version)
        {
            var grid = new CellGrid(map.Min, CellSize, (int2)math.ceil(map.Size / CellSize));
            var start = new NativeArray<int>(grid.Count + 1, Allocator.Persistent);
            foreach (var spot in unsorted)
            {
                start[grid.Index(grid.Clamp(grid.Cell(spot.Position))) + 1]++;
            }

            for (var i = 0; i < grid.Count; i++)
            {
                start[i + 1] += start[i];
            }

            var next = new NativeArray<int>(start.GetSubArray(0, grid.Count), Allocator.Temp);
            var spots = new NativeArray<WorldCoverSpot>(unsorted.Length, Allocator.Persistent);
            var byKey = new NativeHashMap<CoverSpotKey, int>(math.max(unsorted.Length, 1), Allocator.Persistent);
            foreach (var spot in unsorted)
            {
                var cell = grid.Index(grid.Clamp(grid.Cell(spot.Position)));
                var slot = next[cell]++;
                spots[slot] = spot;
                byKey[new CoverSpotKey(spot.Entity, spot.Index)] = slot;
            }

            next.Dispose();
            return new CoverSpots
            {
                Spots = spots,
                CellStart = start,
                ByKey = byKey,
                Grid = grid,
                Version = version,
                Reservations = new NativeArray<Reservation>(unsorted.Length, Allocator.Persistent),
            };
        }

        public readonly void Dispose()
        {
            Spots.Dispose();
            Reservations.Dispose();
            CellStart.Dispose();
            ByKey.Dispose();
        }

        /// <summary>The number of the spot <paramref name="index"/> of <paramref name="entity"/>; false once the prop is gone.</summary>
        public readonly bool TryFind(Entity entity, byte index, out int spot) =>
            ByKey.TryGetValue(new CoverSpotKey(entity, index), out spot);

        /// <summary>Adds the numbers of the spots within <paramref name="radius"/> metres of <paramref name="point"/> to <paramref name="found"/>.</summary>
        public readonly void FindNear(float2 point, float radius, NativeList<int> found)
        {
            var min = Grid.Clamp(Grid.Cell(point - radius));
            var max = Grid.Clamp(Grid.Cell(point + radius));
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = Grid.Index(new int2(x, y));
                    for (var i = CellStart[cell]; i < CellStart[cell + 1]; i++)
                    {
                        if (math.distancesq(Spots[i].Position.xz, point) <= radius * radius)
                        {
                            found.Add(i);
                        }
                    }
                }
            }
        }
    }
}
