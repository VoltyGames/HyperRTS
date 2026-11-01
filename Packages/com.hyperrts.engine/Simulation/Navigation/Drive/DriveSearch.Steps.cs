using HyperRTS.Simulation.Navigation.Grid;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    public partial struct DriveSearch
    {
        private const float FineStep = 1.5f;
        private const float OpenStep = 3f;
        private const float PivotStep = math.PI / 8f;
        private const float ReverseCost = 2f;
        private const float GearChangeHulls = 2f;
        private const float SteerCost = 0.25f;

        // Weight on the guide: a plan a little longer than the best, found in far fewer expansions.
        private const float Greed = 1.5f;

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

        private readonly bool TryShortcut(in Node node, out DriveCurve curve, out float length) =>
            _hull.TryFit(node.Pose, _goal, out curve, out length) && _hull.IsClear(_walls, curve, length, node.Depth);

        // Wheels steer at five shares of the lock, tracks at three, both ways; open ground, where a loop always fits,
        // takes longer steps and only forwards.
        private void AddMoves()
        {
            var shares = _hull.Chassis.IsWheeled ? 2 : 1;
            for (var open = 0; open <= 1; open++)
            {
                for (var share = -shares; share <= shares; share++)
                {
                    AddMove(_hull.MostCurvature * share / shares, 1, open == 1);
                    if (open == 0)
                    {
                        AddMove(_hull.MostCurvature * share / shares, -1, false);
                    }
                }
            }

            var origin = new AxlePose(float2.zero, new float2(1f, 0f));
            var chassis = _hull.Chassis;
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

        private void Expand(int index, in Node node)
        {
            var open = IsOpen(node.Pose);
            foreach (var move in _moves)
            {
                var fits = move.Pivot || move.Open == open;
                var reverses = move.Gear < 0 && node.Run + move.Length > _hull.Chassis.ReverseRun;
                if (fits && !reverses)
                {
                    TryAdd(index, node, move);
                }
            }
        }

        // Each pose, and the middle of the step to it, may overlap walls only while the overlap shrinks, so a wedged start
        // can plan its way out.
        private void TryAdd(int parent, in Node node, in Move move)
        {
            var end = node.Pose.Follow(move.End);
            var key = Key(end);
            if (key < 0)
            {
                return;
            }

            // A pivot keeps the gear and moves no axle, so it adds no reverse run.
            var gear = move.Pivot ? node.Gear : move.Gear;
            var cost = node.Cost + StepCost(node, end, move, gear);
            if (_visited.TryGetValue(key, out var seen) && (_nodes[seen].Closed || _nodes[seen].Cost <= cost))
            {
                return;
            }

            var allowed = math.max(node.Depth, NavTolerances.WallOverlap);
            var depth = _hull.Depth(_walls, end, allowed);
            if (depth > allowed || _hull.Depth(_walls, node.Pose.Follow(move.Middle), allowed) > allowed)
            {
                return;
            }

            _visited[key] = _nodes.Length;
            var next = new Node
            {
                Pose = end, Key = key, Parent = parent, Cost = cost, Depth = depth, Curvature = move.Curvature,
                Gear = gear,
            };
            CountRuns(node, move, ref next);
            _nodes.Add(next);
            _open.Push(_nodes.Length - 1, cost + Greed * Guide(end));
        }

        // A reverse run ends only after a hull length forwards, so a nudge forwards never buys a longer reverse.
        private readonly void CountRuns(in Node node, in Move move, ref Node next)
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
            next.Run = next.Ahead >= _hull.HullLength ? 0f : node.Run;
        }

        // Length dearer near walls and twice in reverse, plus two hulls per gear change and a little for steering.
        private readonly float StepCost(in Node node, in AxlePose end, in Move move, int gear)
        {
            if (move.Pivot)
            {
                return PivotStep * _hull.HullLength * 0.5f;
            }

            var change = node.Gear != 0 && node.Gear != gear ? GearChangeHulls * _hull.HullLength : 0f;

            var access = _hull.Access;
            var cell = access.Grid.Frame.Cell(_hull.Chassis.Center(end));
            var wall = NavAccess.CellCost(_walls.At(cell), access.WallMargin);
            var cost = move.Length * wall * (gear < 0 ? ReverseCost : 1f) + change;
            return cost + SteerCost * math.abs(move.Curvature - node.Curvature) / _hull.MostCurvature;
        }
    }
}
