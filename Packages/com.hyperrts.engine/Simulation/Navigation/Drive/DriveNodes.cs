using Unity.Collections.LowLevel.Unsafe;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    /// <summary>The poses a drive search has reached, the steps it tries from each and the order it expands them.</summary>
    internal struct DriveNodes
    {
        public const float FineStep = 1.5f;
        private const float OpenStep = 3f;
        private const float PivotStep = math.PI / 8f;
        private const float ReverseCost = 2f;
        private const float GearChangeHulls = 2f;
        private const float SteerCost = 0.25f;

        // Weight on the guide: a plan a little longer than the best, found in far fewer expansions.
        private const float Greed = 1.5f;

        // Unchecked, as they are the inner loop of every search.
        private UnsafeList<Node> _nodes;
        private UnsafeList<Move> _moves;
        private OpenSet _open;

        // The best node found so far for each cell and heading bin.
        private UnsafeHashMap<int, int> _visited;

        public DriveNodes(Allocator allocator)
        {
            _nodes = new UnsafeList<Node>(4096, allocator);
            _moves = new UnsafeList<Move>(24, allocator);
            _open = new OpenSet(4096, allocator);
            _visited = new UnsafeHashMap<int, int>(4096, allocator);
        }

        public readonly bool IsCreated => _nodes.IsCreated;

        public readonly Node this[int index] => _nodes[index];

        public void Dispose()
        {
            _nodes.Dispose();
            _moves.Dispose();
            _open.Dispose();
            _visited.Dispose();
        }

        public void ClearMoves() => _moves.Clear();

        public void Begin(in AxlePose start, int key, float depth, float guide)
        {
            _nodes.Clear();
            _open.Clear();
            _visited.Clear();
            _nodes.Add(new Node { Pose = start, Key = key, Parent = -1, Depth = depth });
            _visited[key] = 0;
            _open.Push(0, guide);
        }

        /// <summary>The next node to expand that no better one replaced, or false when none is left.</summary>
        public bool TryPop(out int index)
        {
            while (_open.TryPop(out index))
            {
                var node = _nodes[index];
                if (!node.Closed && _visited[node.Key] == index)
                {
                    return true;
                }
            }

            return false;
        }

        public Node Close(int index)
        {
            ref var node = ref _nodes.ElementAt(index);
            node.Closed = true;
            return node;
        }

        public void FillIfEmpty(in Chassis chassis, float mostCurvature)
        {
            if (_moves.IsEmpty)
            {
                FillMoves(chassis, mostCurvature);
            }
        }

        // One step from the origin facing +x; every node reuses it, so no step pays for its own trigonometry.
        private struct Move
        {
            public AxlePose Middle;
            public AxlePose End;
            public float Curvature;
            public float Length;
            public sbyte Gear;
            public bool Open;
            public bool Pivot;
        }

        // Wheels steer at five shares of the lock, tracks at three, both ways; open ground, where a loop always fits,
        // takes longer steps and only forwards.
        public void FillMoves(in Chassis chassis, float mostCurvature)
        {
            var shares = chassis.IsWheeled ? 2 : 1;
            for (var open = 0; open <= 1; open++)
            {
                for (var share = -shares; share <= shares; share++)
                {
                    AddMove(mostCurvature * share / shares, 1, open == 1);
                    if (open == 0)
                    {
                        AddMove(mostCurvature * share / shares, -1, false);
                    }
                }
            }

            var origin = new AxlePose(float2.zero, new float2(1f, 0f));
            for (var side = -1f; side <= 1f && chassis.Pivots; side += 2f)
            {
                _moves.Add(new Move
                {
                    Middle = chassis.Rotate(origin, side * PivotStep * 0.5f),
                    End = chassis.Rotate(origin, side * PivotStep),
                    Pivot = true,
                });
            }
        }

        private void AddMove(float curvature, int gear, bool open)
        {
            var origin = new AxlePose(float2.zero, new float2(1f, 0f));
            var length = open ? OpenStep : FineStep;
            _moves.Add(new Move
            {
                Middle = origin.Advance(curvature, gear * length * 0.5f), End = origin.Advance(curvature, gear * length),
                Curvature = curvature, Length = length, Gear = (sbyte)gear, Open = open,
            });
        }

        public void Expand(int index, in Node node, in DriveHull hull, in DriveWindow window)
        {
            var open = window.IsOpen(hull, node.Pose);
            foreach (var move in _moves)
            {
                var fits = move.Pivot || move.Open == open;
                var reverses = move.Gear < 0 && node.Run + move.Length > hull.Chassis.ReverseRun;
                if (fits && !reverses)
                {
                    TryAdd(index, node, move, hull, window);
                }
            }
        }

        // Each pose, and the middle of the step to it, may overlap walls only while the overlap shrinks, so a wedged start
        // can plan its way out.
        private void TryAdd(int parent, in Node node, in Move move, in DriveHull hull, in DriveWindow window)
        {
            var end = node.Pose.Follow(move.End);
            var key = window.Key(hull, end);
            if (key < 0)
            {
                return;
            }

            // A pivot keeps the gear and moves no axle, so it adds no reverse run.
            var gear = move.Pivot ? node.Gear : move.Gear;
            var cost = node.Cost + StepCost(node, end, move, gear, hull, window);
            if (_visited.TryGetValue(key, out var seen) && (_nodes[seen].Closed || _nodes[seen].Cost <= cost))
            {
                return;
            }

            var allowed = math.max(node.Depth, NavTolerances.WallOverlap);
            var depth = hull.Depth(window.Walls, end, allowed);
            if (depth > allowed || hull.Depth(window.Walls, node.Pose.Follow(move.Middle), allowed) > allowed)
            {
                return;
            }

            _visited[key] = _nodes.Length;
            var next = new Node
            {
                Pose = end, Key = key, Parent = parent, Cost = cost, Depth = depth, Curvature = move.Curvature,
                Gear = gear,
            };
            CountRuns(node, move, ref next, hull);
            _nodes.Add(next);
            _open.Push(_nodes.Length - 1, cost + Greed * window.Guide(hull, end));
        }

        // A reverse run ends only after a hull length forwards, so a nudge forwards never buys a longer reverse.
        private readonly void CountRuns(in Node node, in Move move, ref Node next, in DriveHull hull)
        {
            if (move.Pivot)
            {
                next.Run = node.Run;
                next.Ahead = node.Ahead;
                return;
            }

            if (move.Gear < 0)
            {
                next.Run = node.Run + move.Length;
                return;
            }

            next.Ahead = (node.Gear > 0 ? node.Ahead : 0f) + move.Length;
            next.Run = next.Ahead >= hull.HullLength ? 0f : node.Run;
        }

        // Length dearer near walls and twice in reverse, plus two hulls per gear change and a little for steering.
        private readonly float StepCost(in Node node, in AxlePose end, in Move move, int gear, in DriveHull hull,
            in DriveWindow window)
        {
            if (move.Pivot)
            {
                return PivotStep * hull.HullLength * 0.5f;
            }

            var change = node.Gear != 0 && node.Gear != gear ? GearChangeHulls * hull.HullLength : 0f;

            var access = hull.Access;
            var cell = access.Grid.Grid.Cell(hull.Chassis.Center(end));
            var wall = NavAccess.CellCost(window.Walls.At(cell), access.WallMargin);
            var cost = move.Length * wall * (gear < 0 ? ReverseCost : 1f) + change;
            return cost + SteerCost * math.abs(move.Curvature - node.Curvature) / hull.MostCurvature;
        }

        public struct Node
        {
            public AxlePose Pose;

            // The pose's cell and heading bin, kept so each pop needn't work it out again.
            public int Key;

            public int Parent;
            public float Cost;
            public float Depth;

            /// <summary>Metres reversed since the last forward run of a hull length.</summary>
            public float Run;

            /// <summary>Metres driven forwards since the last reverse step.</summary>
            public float Ahead;

            public float Curvature;

            /// <summary>1 forwards, -1 in reverse, 0 at the start.</summary>
            public sbyte Gear;

            public bool Closed;
        }
    }
}
