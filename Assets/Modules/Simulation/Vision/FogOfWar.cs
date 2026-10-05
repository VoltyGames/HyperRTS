using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Singleton fog-of-war grid over the map. Each cell holds one bit per team (bit n = team n) for what is
    /// visible right now and what has ever been seen. With fog disabled every cell is visible to everyone.
    /// </summary>
    public struct FogOfWar : IComponentData
    {
        public NativeArray<byte> Visible;
        public NativeArray<byte> Explored;
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

        /// <summary>Hostile entities outside the viewer team's sight are hidden; own, allied and neutral never are.</summary>
        public readonly bool IsHiddenFrom(in FactionRelations relations, byte viewerFaction, byte faction,
            float3 position) =>
            relations.IsHostile(viewerFaction, faction) && !IsVisible(position, relations.TeamOf(viewerFaction));

        private readonly bool Test(NativeArray<byte> cells, float3 position, byte team)
        {
            var cell = WorldToCell(position);
            return InBounds(cell) && HasTeam(cells[Index(cell)], team);
        }

        /// <summary>Whether a <see cref="Visible"/> or <see cref="Explored"/> cell has the team's bit set.</summary>
        public static bool HasTeam(byte cell, byte team) => (cell & (1 << team)) != 0;
    }
}
