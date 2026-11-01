using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    /// <summary>
    /// A <see cref="DriveSearch"/>'s heuristic: the cost from each cell of its window to the goal for a disc the hull's
    /// width.
    /// </summary>
    internal struct DriveGuide : IDisposable, IReverseGraph
    {
        // Turning where walls crowd the hull takes shuffles, so the guide charges the turn's arc many times over.
        private const float CrowdedTurnArcs = 16f;

        // A pose facing off its cell's way pays this share of that turn: enough to lead shuffles round, not to forbid them.
        private const float FacingShare = 0.25f;

        // A pose goal's front is closed this far ahead of its centre, up to this many hull lengths to each side.
        private const float FrontAhead = 2f;
        private const float FrontHulls = 2f;

        private ReverseDijkstra _search;

        // The way each cell's cheapest route leaves it towards the goal.
        private UnsafeList<float2> _ways;

        private NavAccess _access;
        private float _turnRadius;
        private float _hullLength;
        private FieldWindow _walls;
        private CellRect _window;
        private float2 _goal;

        public DriveGuide(int capacity, Allocator allocator)
        {
            _access = default;
            _turnRadius = 0f;
            _hullLength = 0f;
            _search = new ReverseDijkstra(capacity, allocator);
            _ways = new UnsafeList<float2>(capacity, allocator);
            _walls = default;
            _window = default;
            _goal = float2.zero;
        }

        public void Dispose()
        {
            _search.Dispose();
            _ways.Dispose();
        }

        /// <summary>
        /// Guides a hull <paramref name="hullLength"/> long that turns no tighter than <paramref name="turnRadius"/>.
        /// </summary>
        public void SetAgent(in NavAccess access, float turnRadius, float hullLength)
        {
            _access = access;
            _turnRadius = turnRadius;
            _hullLength = hullLength;
        }

        /// <summary>Fills the costs over the window's cells.</summary>
        public void Build(in FieldWindow walls, in CellRect window, in DriveGoal goal)
        {
            _walls = walls;
            _window = window;
            _goal = goal.Center;
            var count = window.Count;
            // Every step costs at least a cell.
            _search.Reset(count, _access.Grid.Frame.CellSize);
            _ways.Resize(count);
            for (var i = 0; i < count; i++)
            {
                _search.Settled[i] = !IsOpen(_window.Cell(i));
                _ways[i] = float2.zero;
            }

            if (goal.HasFacing)
            {
                CloseFront(goal);
            }

            Seed(goal.Facing);
            _search.Settle(ref this);
        }

        /// <summary>
        /// The cost to the goal from the cell under a point, facing <paramref name="facing"/>, with the turn onto the
        /// way the cell's route leaves; the straight distance where it doesn't reach.
        /// </summary>
        public readonly float Cost(float2 point, float2 facing)
        {
            var cell = _access.Grid.Frame.Cell(point);
            var index = _window.Index(cell);
            var cost = index < 0 ? float.PositiveInfinity : _search.Costs[index];
            if (cost == float.PositiveInfinity)
            {
                return math.distance(point, _goal);
            }

            return cost + Turn(facing, _ways[index]) * Crowding(cell) * FacingShare;
        }

        // Metres charged per radian turned at a cell.
        private readonly float Crowding(int2 cell) =>
            math.saturate(1f - _walls.At(cell) / _turnRadius) * _turnRadius * CrowdedTurnArcs;

        // Radians from one way to another, roughly; none from no way.
        private static float Turn(float2 from, float2 to) =>
            math.any(to != 0f) ? (1f - math.dot(from, to)) * math.PI * 0.5f : 0f;

        private readonly bool IsOpen(int2 cell) => _walls.At(cell) >= _access.OpenDistance;

        // Closing a pose goal's front leads the route in from behind, as the hull must arrive; only in a lane, walls on
        // both sides within reach, as in open ground it may turn.
        private void CloseFront(in DriveGoal goal)
        {
            var side = Planar.Left(goal.Facing);
            var middle = goal.Center + goal.Facing * FrontAhead;
            var halfCell = _access.Grid.Frame.CellSize * 0.5f;
            var reach = (int)math.ceil(_hullLength * FrontHulls / halfCell);
            var left = Wall(middle, side, reach, halfCell);
            var right = Wall(middle, -side, reach, halfCell);
            for (var i = -right; i <= left && left <= reach && right <= reach; i++)
            {
                var point = middle + side * (i * halfCell);
                Close(point);
                Close(point + goal.Facing * halfCell);
            }
        }

        // Half cells from a point to the first wall that way, or one past the reach.
        private readonly int Wall(float2 from, float2 way, int reach, float halfCell)
        {
            var length = reach * halfCell;
            var ray = new GridRay(_access.Grid.Frame, from, from + way * length);
            while (ray.MoveNext())
            {
                if (!IsOpen(ray.Cell))
                {
                    return (int)math.ceil(ray.Entry * length / halfCell);
                }
            }

            return reach + 1;
        }

        private void Close(float2 point)
        {
            var index = _window.Index(_access.Grid.Frame.Cell(point));
            if (index >= 0)
            {
                _search.Settled[index] = true;
            }
        }

        // The goal's cell and its open neighbours, leaving the way a pose goal faces.
        private void Seed(float2 facing)
        {
            var goal = _access.Grid.Frame.Cell(_goal);
            foreach (var cell in new CellRange(goal - 1, goal + 1))
            {
                var index = _window.Index(cell);
                var isGoal = cell.Equals(goal);
                if (index >= 0 && (isGoal || !_search.Settled[index]))
                {
                    _ways[index] = facing;
                    _search.Seed(index);
                }
            }
        }

        // Settles backwards: a neighbour pays its step onto this cell, and for the turn it makes there.
        public void Relax(int index, ref ReverseDijkstra search)
        {
            var cell = _window.Cell(index);
            var crowded = Crowding(cell);
            var leaving = _ways[index];
            foreach (var offset in new Neighbours())
            {
                var at = _window.Index(cell + offset);
                if (at < 0 || search.Settled[at] || !CanStep(cell, offset))
                {
                    continue;
                }

                var step = GridAStar.StepCost(int2.zero, offset);
                var way = -(float2)offset / step;
                var cost = search.Costs[index] + step * _access.Grid.Frame.CellSize + Turn(way, leaving) * crowded;
                if (search.Offer(at, cost))
                {
                    _ways[at] = way;
                }
            }
        }

        // Reads the window, not a CellBlock: this runs per relaxed cell. A diagonal never cuts a corner.
        private readonly bool CanStep(int2 cell, int2 offset) =>
            offset.x == 0 || offset.y == 0 ||
            (IsOpen(cell + new int2(offset.x, 0)) && IsOpen(cell + new int2(0, offset.y)));
    }
}
