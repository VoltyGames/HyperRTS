using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Singleton fog-of-war grid over the map. Each cell holds one bit per team (bit n = team n) for what is
    /// visible right now, what has ever been seen and what a detector covers. With fog disabled every cell is visible
    /// to everyone, but stealth still needs detection.
    /// </summary>
    public struct FogOfWar : IComponentData
    {
        public NativeArray<ushort> Visible;
        public NativeArray<ushort> Explored;
        public NativeArray<ushort> Detected;
        public int2 Size;
        public float2 Min;
        public float CellSize;

        /// <summary>Bumped whenever the grid is restamped, so renderers upload only on change.</summary>
        public int Version;

        public readonly bool IsCreated => Visible.IsCreated;

        public readonly int2 WorldToCell(float3 position) => (int2)math.floor((position.xz - Min) / CellSize);

        public readonly bool InBounds(int2 cell) => math.all(cell >= 0 & cell < Size);

        public readonly int Index(int2 cell) => cell.y * Size.x + cell.x;

        public readonly int2 Cell(int index) => new(index % Size.x, index / Size.x);

        public readonly bool IsVisible(float3 position, byte team) => Test(Visible, position, team);

        public readonly bool IsExplored(float3 position, byte team) => Test(Explored, position, team);

        public readonly bool IsDetected(float3 position, byte team) => Test(Detected, position, team);

        /// <summary>Stealth hides an entity from a team none of whose detectors covers it, with or without fog.</summary>
        public readonly bool IsCloakedFrom(byte team, float3 position, bool stealthed) =>
            stealthed && !IsDetected(position, team);

        /// <summary>Whether a team sees an entity there: in its sight and, if stealthed, detected.</summary>
        public readonly bool CanSee(byte team, float3 position, bool stealthed) =>
            IsVisible(position, team) && !IsCloakedFrom(team, position, stealthed);

        /// <summary>Hostile entities the viewer team can't see are hidden; own, allied and neutral never are.</summary>
        public readonly bool IsHiddenFrom(in FactionRelations relations, byte viewerFaction, byte faction,
            float3 position, bool stealthed) =>
            relations.IsHostile(viewerFaction, faction) && !CanSee(relations.TeamOf(viewerFaction), position, stealthed);

        private readonly bool Test(NativeArray<ushort> cells, float3 position, byte team)
        {
            var cell = WorldToCell(position);
            return InBounds(cell) && HasTeam(cells[Index(cell)], team);
        }

        /// <summary>ORs <paramref name="bit"/> into every cell whose center lies within the flat circle.</summary>
        public readonly void StampCircle(NativeArray<ushort> cells, float3 center, float radius, ushort bit)
        {
            var radiusSq = radius * radius;
            var min = math.max(WorldToCell(center - radius), 0);
            var max = math.min(WorldToCell(center + radius), Size - 1);

            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cellCenter = Min + (new float2(x, y) + 0.5f) * CellSize;
                    if (math.distancesq(cellCenter, center.xz) <= radiusSq)
                    {
                        var index = Index(new int2(x, y));
                        cells[index] = (ushort)(cells[index] | bit);
                    }
                }
            }
        }

        /// <summary>Whether a <see cref="Visible"/> or <see cref="Explored"/> cell has the team's bit set.</summary>
        public static bool HasTeam(ushort cell, byte team) => (cell & (1 << team)) != 0;

        /// <summary>The team's cell bit; false for neutral and out-of-range teams, which never stamp.</summary>
        public static bool TryGetTeamBit(byte team, out ushort bit)
        {
            bit = 0;
            if (team == 0 || team >= FactionRelations.MaxTeams)
            {
                return false;
            }

            bit = (ushort)(1 << team);
            return true;
        }
    }
}
