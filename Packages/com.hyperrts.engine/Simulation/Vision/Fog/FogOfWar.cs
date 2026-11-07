using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision.Detection;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision.Fog
{
    /// <summary>
    /// Singleton fog-of-war grid over the map, as one bit plane per team for each layer: what is visible right now, at
    /// ground height and at the height of a <see cref="TallTarget"/>, what has ever been seen, what a detector covers
    /// and what a ground unit is close to. With fog disabled every cell is visible to everyone, but stealth and
    /// concealment still need detection.
    /// </summary>
    public struct FogOfWar : IComponentData
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
            var words = rowWords * grid.Size.y * FactionRelations.MaxTeams;
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

        /// <summary>Whether the team sees a target there, judged at a tall target's height when <paramref name="tall"/>.</summary>
        public readonly bool IsVisible(float3 position, byte team, bool tall) =>
            Test(tall ? VisibleTall : Visible, position, team);

        public readonly bool IsExplored(float3 position, byte team) => Test(Explored, position, team);

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
        public readonly bool IsHiddenFrom(in FactionRelations relations, byte viewerFaction, byte faction,
            float3 position, Concealment concealment, bool tall) =>
            relations.IsHostile(viewerFaction, faction) &&
            !CanSee(relations.GetTeam(viewerFaction), position, concealment, tall);

        /// <summary>Whether the team's bit is set for the cell in one of the layers.</summary>
        public readonly bool Has(NativeArray<ulong> plane, int2 cell, byte team) =>
            (plane[Word(cell, team)] & (1ul << (cell.x & 63))) != 0;

        public readonly void Set(NativeArray<ulong> plane, int2 cell, byte team)
        {
            var word = Word(cell, team);
            plane[word] |= 1ul << (cell.x & 63);
        }

        /// <summary>ORs 64 cells' bits starting at <paramref name="x"/> into a row, dropping those off the grid.</summary>
        public readonly void Or(NativeArray<ulong> plane, byte team, int row, int x, ulong bits)
        {
            if (x < 0)
            {
                bits = x <= -64 ? 0ul : bits >> -x;
                x = 0;
            }

            var room = Grid.Size.x - x;
            if (bits == 0ul || room <= 0)
            {
                return;
            }

            bits &= room >= 64 ? ulong.MaxValue : (1ul << room) - 1ul;
            var word = team * PlaneWords + row * RowWords + (x >> 6);
            var shift = x & 63;
            plane[word] |= bits << shift;
            if (shift != 0 && (x >> 6) + 1 < RowWords)
            {
                plane[word + 1] |= bits >> (64 - shift);
            }
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
        public static bool Stamps(byte team) => team != 0 && team < FactionRelations.MaxTeams;

        private readonly int Word(int2 cell, byte team) => team * PlaneWords + cell.y * RowWords + (cell.x >> 6);

        private readonly bool Test(NativeArray<ulong> plane, float3 position, byte team)
        {
            var cell = Grid.Cell(position);
            return Grid.Contains(cell) && Has(plane, cell, team);
        }
    }
}
