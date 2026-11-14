using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision.Fog
{
    /// <summary>
    /// Singleton fog-of-war grid, one bit plane per team for each layer: visible now (at ground height and at
    /// <see cref="TallTarget"/> height), ever seen, detected and close to a ground unit. With fog disabled every cell
    /// is visible, but stealth still needs detection.
    /// </summary>
    public struct FogOfWar : IComponentData, IReplaceableSingleton<FogOfWar>
    {
        /// <summary>Cells whose ground is in sight, so infantry there are seen.</summary>
        public NativeArray<ulong> Visible;

        /// <summary>Cells where a tall target is in sight; every visible cell is in it too.</summary>
        public NativeArray<ulong> VisibleTall;

        public NativeArray<ulong> Explored;
        public NativeArray<ulong> Detected;

        /// <summary>Cells within the reveal distance of a team's ground units, where its concealed enemies show.</summary>
        public NativeArray<ulong> Close;

        public CellGrid Grid;

        /// <summary>64-bit words in one row of a team's plane.</summary>
        public int RowWords;

        /// <summary>Bumped whenever the grid is restamped, so renderers upload only on change.</summary>
        public int Version;

        /// <summary>Bumped whenever the grid is built anew, so every viewer's <see cref="SightArea"/> is drawn again.</summary>
        public int Generation;

        public readonly bool IsCreated => Visible.IsCreated;

        public readonly int PlaneWords => RowWords * Grid.Size.y;

        /// <summary>An empty grid with a plane for every team in each layer.</summary>
        public static FogOfWar Create(in CellGrid grid, Allocator allocator)
        {
            var rowWords = (grid.Size.x + 63) >> 6;
            var words = rowWords * grid.Size.y * OwnerRelations.MaxTeams;
            return new FogOfWar
            {
                Visible = new NativeArray<ulong>(words, allocator),
                VisibleTall = new NativeArray<ulong>(words, allocator),
                Explored = new NativeArray<ulong>(words, allocator),
                Detected = new NativeArray<ulong>(words, allocator),
                Close = new NativeArray<ulong>(words, allocator),
                Grid = grid,
                RowWords = rowWords,
                Version = 1,
            };
        }

        public readonly void Dispose()
        {
            Visible.Dispose();
            VisibleTall.Dispose();
            Explored.Dispose();
            Detected.Dispose();
            Close.Dispose();
        }

        public void Inherit(ref FogOfWar previous)
        {
            Version = previous.Version + 1;
            Generation = previous.Generation + 1;
        }

        /// <summary>Whether the team sees a target there, judged at a tall target's height when <paramref name="tall"/>.</summary>
        public readonly bool IsVisible(float3 position, byte team, bool tall) =>
            Test(tall ? VisibleTall : Visible, position, team);

        public readonly bool IsExplored(float3 position, byte team) => Test(Explored, position, team);

        /// <summary>What an AI may act on: units must be in sight, buildings stay known once explored, and a match without fog (not created) shows everything.</summary>
        public readonly bool IsKnown(float3 position, byte team, bool building, bool tall)
        {
            if (!IsCreated)
            {
                return true;
            }

            return building ? IsExplored(position, team) : IsVisible(position, team, tall);
        }

        public readonly bool IsDetected(float3 position, byte team) => Test(Detected, position, team);

        public readonly bool IsClose(float3 position, byte team) => Test(Close, position, team);

        /// <summary>
        /// Stealth hides an entity from a team none of whose detectors covers it, and concealment also from one with no
        /// ground unit close by, with or without fog.
        /// </summary>
        public readonly bool IsHiddenByConcealment(byte team, float3 position, Concealment concealment)
        {
            if (concealment == Concealment.None || IsDetected(position, team))
            {
                return false;
            }

            return concealment == Concealment.Stealthed || !IsClose(position, team);
        }

        /// <summary>Whether a team sees an entity there: in its sight at the entity's height, and not hidden by concealment.</summary>
        public readonly bool CanSee(byte team, float3 position, Concealment concealment, bool tall) =>
            IsVisible(position, team, tall) && !IsHiddenByConcealment(team, position, concealment);

        /// <summary>Hostile entities the viewer team can't see are hidden; own, allied and neutral never are.</summary>
        public readonly bool IsHiddenFrom(in OwnerRelations relations, byte viewerOwner, byte owner,
            float3 position, Concealment concealment, bool tall) =>
            relations.IsHostile(viewerOwner, owner) &&
            !CanSee(relations.GetTeam(viewerOwner), position, concealment, tall);

        /// <summary>Whether the team's bit is set for the cell in one of the layers.</summary>
        public readonly bool Has(NativeArray<ulong> plane, int2 cell, byte team) =>
            (plane[Word(cell, team)] & (1ul << (cell.x & 63))) != 0;

        public readonly void Set(NativeArray<ulong> plane, int2 cell, byte team)
        {
            var word = Word(cell, team);
            plane[word] |= 1ul << (cell.x & 63);
        }

        /// <summary>Sets the team's bit in every cell whose center lies within the flat circle.</summary>
        public readonly void StampCircle(NativeArray<ulong> plane, float3 center, float radius, byte team)
        {
            foreach (var cell in Grid.CellsInCircle(center.xz, radius))
            {
                Set(plane, cell, team);
            }
        }

        /// <summary>Whether a team stamps the grid: neutral never does. Baking and match setup reject teams past the planes.</summary>
        public static bool Stamps(byte team) => team != 0 && team < OwnerRelations.MaxTeams;

        private readonly int Word(int2 cell, byte team) => team * PlaneWords + cell.y * RowWords + (cell.x >> 6);

        private readonly bool Test(NativeArray<ulong> plane, float3 position, byte team)
        {
            var cell = Grid.Cell(position);
            return Grid.Contains(cell) && Has(plane, cell, team);
        }
    }
}
