using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// A <see cref="DriveSearch"/>'s heuristic: the cost from each cell of its window to the goal for a disc the hull's
    /// width, by Dijkstra with eight steps that never cut a corner. Turning where walls crowd the hull costs extra, and
    /// a pose goal's front is closed, so it leads in from behind as the hull must arrive.
    /// </summary>
    internal struct DriveGuide : IDisposable
    {
        // Turning where walls crowd the hull takes shuffles, so the guide charges the turn's arc many times over.
        private const float CrowdedTurnArcs = 16f;

        // A pose facing off its cell's way pays this share of that turn: enough to lead shuffles round, not to forbid them.
        private const float FacingShare = 0.25f;

        // A pose goal's front is closed this far ahead of its centre, up to this many hull lengths to each side.
        private const float FrontAhead = 2f;
        private const float FrontHulls = 2f;

        private UnsafeList<float> _costs;
        private UnsafeList<bool> _settled;

        // The way each cell's cheapest route leaves it towards the goal.
        private UnsafeList<float2> _ways;

        // The Dijkstra queue in buckets half a cell wide: each bucket's first entry, each entry's cell and the next.
        private UnsafeList<int> _buckets;
        private UnsafeList<int2> _entries;
        private FieldWindow _walls;
        private NavAccess _access;
        private int2 _first;
        private int2 _cells;
        private float2 _goal;
        private float _turnRadius;

        public DriveGuide(int capacity, Allocator allocator)
        {
            _costs = new UnsafeList<float>(capacity, allocator);
            _settled = new UnsafeList<bool>(capacity, allocator);
            _ways = new UnsafeList<float2>(capacity, allocator);
            _buckets = new UnsafeList<int>(1024, allocator);
            _entries = new UnsafeList<int2>(capacity, allocator);
            _walls = default;
            _access = default;
            _first = int2.zero;
            _cells = int2.zero;
            _goal = float2.zero;
            _turnRadius = 1f;
        }

        public void Dispose()
        {
            _costs.Dispose();
            _settled.Dispose();
            _ways.Dispose();
            _buckets.Dispose();
            _entries.Dispose();
        }

        /// <summary>Fills the costs over the cells from <paramref name="first"/>, <paramref name="cells"/> across.</summary>
        public void Build(in FieldWindow walls, in NavAccess access, int2 first, int2 cells, in DriveGoal goal,
            float turnRadius, float hullLength)
        {
            _walls = walls;
            _access = access;
            _first = first;
            _cells = cells;
            _goal = goal.Center;
            _turnRadius = turnRadius;
            var count = cells.x * cells.y;
            _costs.Resize(count);
            _settled.Resize(count);
            _ways.Resize(count);
            _buckets.Clear();
            _entries.Clear();
            for (var i = 0; i < count; i++)
            {
                _costs[i] = float.PositiveInfinity;
                _settled[i] = !IsOpen(Cell(i));
                _ways[i] = float2.zero;
            }

            if (goal.HasFacing)
            {
                CloseFront(goal, hullLength);
            }

            Seed(goal.Facing);
            Settle();
        }

        /// <summary>
        /// The cost to the goal from the cell under a point, facing <paramref name="facing"/>, with the turn onto the
        /// way the cell's route leaves; the straight distance where it doesn't reach.
        /// </summary>
        public readonly float Cost(float2 point, float2 facing)
        {
            var cell = _access.Grid.WorldToCell(point);
            var index = Index(cell);
            var cost = index < 0 ? float.PositiveInfinity : _costs[index];
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

        private readonly int2 Cell(int index) => _first + new int2(index % _cells.x, index / _cells.x);

        private readonly int Index(int2 cell)
        {
            var local = cell - _first;
            return math.any(local < 0 | local >= _cells) ? -1 : local.y * _cells.x + local.x;
        }

        private readonly bool IsOpen(int2 cell) => _walls.At(cell) >= _access.OpenDistance;

        // Only a goal in a lane, walls on both sides within reach, has its front closed: in open ground it may turn.
        private void CloseFront(in DriveGoal goal, float hullLength)
        {
            var side = new float2(-goal.Facing.y, goal.Facing.x);
            var middle = goal.Center + goal.Facing * FrontAhead;
            var halfCell = _access.Grid.CellSize * 0.5f;
            var reach = (int)math.ceil(hullLength * FrontHulls / halfCell);
            var left = Wall(middle, side, reach, halfCell);
            var right = Wall(middle, -side, reach, halfCell);
            for (var i = -right; i <= left && left <= reach && right <= reach; i++)
            {
                var point = middle + side * (i * halfCell);
                Close(point);
                Close(point + goal.Facing * halfCell);
            }
        }

        // Half cells from a point to the first wall that way, or past the reach.
        private readonly int Wall(float2 from, float2 way, int reach, float halfCell)
        {
            var steps = 0;
            while (steps <= reach && IsOpen(_access.Grid.WorldToCell(from + way * (steps * halfCell))))
            {
                steps++;
            }

            return steps;
        }

        private void Close(float2 point)
        {
            var index = Index(_access.Grid.WorldToCell(point));
            if (index >= 0)
            {
                _settled[index] = true;
            }
        }

        // The goal's cell and its open neighbours, leaving the way a pose goal faces.
        private void Seed(float2 facing)
        {
            var goal = _access.Grid.WorldToCell(_goal);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var index = Index(goal + new int2(dx, dy));
                    var isGoal = dx == 0 && dy == 0;
                    if (index >= 0 && (isGoal || !_settled[index]))
                    {
                        _costs[index] = 0f;
                        _ways[index] = facing;
                        Push(index, 0f);
                    }
                }
            }
        }

        // Every step costs at least a cell, so a bucket never grows while it is read.
        private void Settle()
        {
            for (var bucket = 0; bucket < _buckets.Length; bucket++)
            {
                for (var entry = _buckets[bucket]; entry >= 0; entry = _entries[entry].y)
                {
                    var index = _entries[entry].x;
                    if (!_settled[index])
                    {
                        _settled[index] = true;
                        Relax(index);
                    }
                }
            }
        }

        private void Push(int index, float cost)
        {
            var bucket = (int)(cost / (_access.Grid.CellSize * 0.5f));
            while (_buckets.Length <= bucket)
            {
                _buckets.Add(-1);
            }

            _entries.Add(new int2(index, _buckets[bucket]));
            _buckets[bucket] = _entries.Length - 1;
        }

        // Settles backwards: a neighbour pays its step onto this cell, and for the turn it makes there.
        private void Relax(int index)
        {
            var cell = Cell(index);
            var crowded = Crowding(cell);
            var leaving = _ways[index];
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var offset = new int2(dx, dy);
                    var at = Index(cell + offset);
                    if (at < 0 || _settled[at] || !CanStep(cell, offset))
                    {
                        continue;
                    }

                    var step = dx != 0 && dy != 0 ? math.SQRT2 : 1f;
                    var way = -(float2)offset / step;
                    var cost = _costs[index] + step * _access.Grid.CellSize + Turn(way, leaving) * crowded;
                    if (cost < _costs[at])
                    {
                        _costs[at] = cost;
                        _ways[at] = way;
                        Push(at, cost);
                    }
                }
            }
        }

        // A diagonal needs both cells beside it open, so it never cuts a corner.
        private readonly bool CanStep(int2 cell, int2 offset) =>
            offset.x == 0 || offset.y == 0 ||
            (IsOpen(cell + new int2(offset.x, 0)) && IsOpen(cell + new int2(0, offset.y)));
    }
}
