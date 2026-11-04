using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Vision.Fog;
using System.Text;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Renders a window of a grid as ASCII, north up, for a failed assert: <see cref="Path"/> shows nav cells with a
    /// path on them and <see cref="Fog"/> a team's fog round a point. Pass the text in a failure message, or to
    /// <see cref="SimulationFixture.DumpOnFailure"/>.
    /// </summary>
    public static class GridDump
    {
        /// <summary>Widest window in cells; a longer path is cut to this round its start.</summary>
        private const int MaxSide = 100;

        /// <summary>Nav cells round the path: # blocked, ~ water, = deck, . land; S start, * waypoint, G goal.</summary>
        public static string Path(in NavGrid grid, float3 from, DynamicBuffer<PathWaypoint> path, int margin = 3)
        {
            var low = grid.Grid.Cell(from);
            var high = low;
            foreach (var waypoint in path)
            {
                var cell = grid.Grid.Cell(waypoint.Position);
                low = math.min(low, cell);
                high = math.max(high, cell);
            }

            var marks = new char[MaxSide * MaxSide];
            var window = Window(grid.Grid, low, high, margin, out var origin, out var size);
            Mark(grid.Grid, origin, size, marks, from, 'S');
            for (var i = 0; i < path.Length; i++)
            {
                Mark(grid.Grid, origin, size, marks, path[i].Position, i == path.Length - 1 ? 'G' : '*');
            }

            var text = new StringBuilder($"nav cells from {window.Min} to {window.Max}, # blocked, ~ water, = deck, S start, * waypoint, G goal\n");
            for (var y = size.y - 1; y >= 0; y--)
            {
                for (var x = 0; x < size.x; x++)
                {
                    var mark = marks[y * size.x + x];
                    text.Append(mark != 0 ? mark : Surface(grid.Surface(origin + new int2(x, y))));
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        /// <summary>A team's fog round a point: V sees the ground, T only tall targets, + explored, . unseen; X marks the centre.</summary>
        public static string Fog(in FogOfWar fog, byte team, float3 center, int radius = 10)
        {
            var middle = fog.Grid.Cell(center);
            var window = Window(fog.Grid, middle, middle, radius, out var origin, out var size);
            var text = new StringBuilder($"fog of team {team} from {window.Min} to {window.Max}, V ground, T tall only, + explored, . unseen, X centre\n");
            for (var y = size.y - 1; y >= 0; y--)
            {
                for (var x = 0; x < size.x; x++)
                {
                    var cell = origin + new int2(x, y);
                    text.Append(cell.Equals(middle) ? 'X' : Fog(fog, team, cell));
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        public static void Write(string dump) => TestContext.Out.WriteLine(dump);

        private static char Surface(NavSurface surface)
        {
            if ((surface & NavSurface.Deck) != 0)
            {
                return '=';
            }

            if (surface == NavSurface.Blocked)
            {
                return '#';
            }

            return surface == NavSurface.Water ? '~' : '.';
        }

        private static char Fog(in FogOfWar fog, byte team, int2 cell)
        {
            if (!fog.Grid.Contains(cell))
            {
                return ' ';
            }

            if (fog.Has(fog.Visible, cell, team))
            {
                return 'V';
            }

            if (fog.Has(fog.VisibleTall, cell, team))
            {
                return 'T';
            }

            return fog.Has(fog.Explored, cell, team) ? '+' : '.';
        }

        private static void Mark(in CellGrid grid, int2 origin, int2 size, char[] marks, float3 position, char mark)
        {
            var local = grid.Cell(position) - origin;
            if (math.all(local >= 0 & local < size))
            {
                marks[local.y * size.x + local.x] = mark;
            }
        }

        private static CellRange Window(in CellGrid grid, int2 low, int2 high, int margin, out int2 origin, out int2 size)
        {
            var window = grid.Cells(low - margin, math.min(high + margin, low + MaxSide - 1 - margin));
            origin = window.Min;
            size = window.Max - window.Min + 1;
            return window;
        }
    }
}
