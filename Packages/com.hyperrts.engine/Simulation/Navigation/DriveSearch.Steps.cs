using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct DriveSearch
    {
        private const float FineStep = 1.5f;
        private const float OpenStep = 3f;
        private const float SweepStep = 0.5f;
        private const float PivotStep = math.PI / 8f;
        private const float ReverseCost = 2f;
        private const float GearChangeHulls = 2f;
        private const float SteerCost = 0.25f;

        // Weight on the guide: a plan a little longer than the best, found in far fewer expansions.
        private const float Greed = 1.5f;

        // Room kept off walls, so following the plan a little wide never brushes one.
        private const float Clearance = 0.25f;

        // Plans steer no tighter than this share of full lock, leaving the follower room to correct.
        private const float LockShare = 0.85f;

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

        /// <summary>
        /// How far the hull, with some room for error round it, reaches into walls with its rear axle at
        /// <paramref name="pose"/>, counted only until past <paramref name="limit"/>.
        /// </summary>
        public readonly float Depth(in AxlePose pose, float limit) =>
            new NavCapsule(_access, float2.zero).Depth(_chassis.Center(pose), pose.Facing * _halfSegment, Clearance, limit);

        /// <summary>
        /// Whether the hull, swept along the first <paramref name="length"/> metres of the curve, never sinks into
        /// walls deeper than <paramref name="depth"/> or than it already has since.
        /// </summary>
        public readonly bool IsClear(in DriveCurve curve, float length, float depth) =>
            Sweeps(curve, length, depth, false);

        // Hull depths every half metre, from the search's window or else the field.
        private readonly bool Sweeps(in DriveCurve curve, float length, float depth, bool inWindow)
        {
            var allowed = math.max(depth, NavTolerances.WallOverlap);
            for (var distance = SweepStep; distance < length + SweepStep; distance += SweepStep)
            {
                var pose = curve.Sample(math.min(distance, length));
                var sunk = inWindow ? HullDepth(pose, allowed) : Depth(pose, allowed);
                if (sunk > allowed)
                {
                    return false;
                }

                allowed = math.max(sunk, NavTolerances.WallOverlap);
            }

            return true;
        }

        /// <summary>The radius of the curves it fits: a little over the turn radius, or one hull length for tracks.</summary>
        public readonly float CurveRadius => 1f / MostCurvature;

        private readonly float MostCurvature => _chassis.IsWheeled ? _chassis.MaxCurvature * LockShare : 1f / HullLength;

        private readonly float HullLength => (_access.Class.Radius + _halfSegment) * 2f;

        /// <summary>
        /// The forward curve to the goal, ending with the centre on a point goal, so the rear axle stops a rear axle
        /// short of it on the straight.
        /// </summary>
        public readonly bool TryFit(in AxlePose from, in DriveGoal goal, out DriveCurve curve, out float length)
        {
            if (goal.HasFacing)
            {
                var fits = DriveCurve.TryFit(from, _chassis.Pose(goal.Center, goal.Facing), CurveRadius, out curve);
                length = curve.Length;
                return fits;
            }

            var reaches = DriveCurve.TryFitPoint(from, goal.Center, CurveRadius, out curve);
            length = curve.Length - _chassis.RearAxle;
            return reaches && curve.Straight >= _chassis.RearAxle;
        }

        // The same depth over the search's window, which reads the field unchecked.
        private readonly float HullDepth(in AxlePose pose, float limit) =>
            _walls.Depth(_chassis.Center(pose), pose.Facing * _halfSegment, _access.Class.Radius + Clearance, limit);

        private readonly bool TryShortcut(in Node node, out DriveCurve curve, out float length) =>
            TryFit(node.Pose, _goal, out curve, out length) && Sweeps(curve, length, node.Depth, true);

        // Wheels steer at five shares of the lock, tracks at three, both ways; open ground, where a loop always fits,
        // takes longer steps and only forwards.
        private void AddMoves()
        {
            var shares = _chassis.IsWheeled ? 2 : 1;
            for (var open = 0; open <= 1; open++)
            {
                for (var share = -shares; share <= shares; share++)
                {
                    AddMove(MostCurvature * share / shares, 1, open == 1);
                    if (open == 0)
                    {
                        AddMove(MostCurvature * share / shares, -1, false);
                    }
                }
            }

            var origin = new AxlePose(float2.zero, new float2(1f, 0f));
            for (var side = -1f; side <= 1f && _chassis.Pivots; side += 2f)
            {
                _moves.Add(new Move
                {
                    Middle = _chassis.Rotate(origin, side * PivotStep * 0.5f), End = _chassis.Rotate(origin, side * PivotStep),
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
                var reverses = move.Gear < 0 && node.Run + move.Length > _chassis.ReverseRun;
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
            var depth = HullDepth(end, allowed);
            if (depth > allowed || HullDepth(node.Pose.Follow(move.Middle), allowed) > allowed)
            {
                return;
            }

            _visited[key] = _nodes.Length;
            var next = new Node { Pose = end, Parent = parent, Cost = cost, Depth = depth, Curvature = move.Curvature, Gear = gear };
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
            next.Run = next.Ahead >= HullLength ? 0f : node.Run;
        }

        // Length dearer near walls and twice in reverse, plus two hulls per gear change and a little for steering.
        private readonly float StepCost(in Node node, in AxlePose end, in Move move, int gear)
        {
            if (move.Pivot)
            {
                return PivotStep * HullLength * 0.5f;
            }

            var change = node.Gear != 0 && node.Gear != gear ? GearChangeHulls * HullLength : 0f;

            var wall = NavAccess.CellCost(_walls.At(_access.Grid.WorldToCell(_chassis.Center(end))), _access.WallMargin);
            var cost = move.Length * wall * (gear < 0 ? ReverseCost : 1f) + change;
            return cost + SteerCost * math.abs(move.Curvature - node.Curvature) / MostCurvature;
        }
    }
}
