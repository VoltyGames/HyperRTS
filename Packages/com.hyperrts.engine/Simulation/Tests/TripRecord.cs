using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Steps a <see cref="TestWorld"/> and records how named units drove: metres, turning, loops, tight legs, stops,
    /// give-ups and sideways speed, so movement quality is a number a later change can beat.
    /// </summary>
    public sealed class TripRecord
    {
        private readonly TestWorld _world;
        private readonly List<Tally> _units = new();

        public TripRecord(TestWorld world) => _world = world;

        public IReadOnlyList<Tally> Units => _units;
        public int Steps { get; private set; }
        public float Seconds => Steps * TestWorld.FrameTime;

        /// <summary>
        /// The most any unit turned one way within 40 m driven forwards: a truck's forward ring reads 360 degrees, a
        /// turnaround it backs round under 180.
        /// </summary>
        public float MaxLoop => Max(unit => unit.MaxLoop);

        /// <summary>The unit that turned most one way, where and when.</summary>
        public Tally WorstLoop => _units.OrderByDescending(unit => unit.MaxLoop).First();

        public float MaxSideways => Max(unit => unit.Sideways);
        public int GiveUps => Sum(unit => unit.GiveUps);
        public int Stops => Sum(unit => unit.Stops);
        public int Trips => Sum(unit => unit.Trips);
        public float TightSeconds => Sum(unit => unit.TightSeconds);

        public float MeanTripSeconds => Trips == 0 ? 0f : Sum(unit => unit.TripSeconds) / Trips;

        public Tally Track(string name, Entity unit)
        {
            var tally = new Tally(name, unit, _world);
            _units.Add(tally);
            return tally;
        }

        /// <summary>One simulation step, then a sample of every tracked unit.</summary>
        public void Tick()
        {
            _world.Tick();
            Steps++;
            foreach (var unit in _units)
            {
                unit.Sample(_world);
            }
        }

        public void CountTrip(Tally unit) => unit.CountTrip(Seconds);

        /// <summary>One line per unit, then a summary line, to the Editor log, which test runs from the command line keep.</summary>
        public void Log(string title)
        {
            UnityEngine.Debug.Log($"[{title}] {Steps} steps, {Seconds:F0} s");
            foreach (var unit in _units)
            {
                UnityEngine.Debug.Log($"  {unit}");
            }

            UnityEngine.Debug.Log($"[{title}] summary: {Trips} trips, mean {MeanTripSeconds:F1} s, " +
                             $"max loop {MaxLoop:F0} deg, tight {TightSeconds:F1} s, stops {Stops}, " +
                             $"give-ups {GiveUps}, max sideways {MaxSideways:F2} m/s");
        }

        private float Max(System.Func<Tally, float> read)
        {
            var most = 0f;
            foreach (var unit in _units)
            {
                most = math.max(most, read(unit));
            }

            return most;
        }

        private int Sum(System.Func<Tally, int> read)
        {
            var total = 0;
            foreach (var unit in _units)
            {
                total += read(unit);
            }

            return total;
        }

        private float Sum(System.Func<Tally, float> read)
        {
            var total = 0f;
            foreach (var unit in _units)
            {
                total += read(unit);
            }

            return total;
        }

        /// <summary>What one unit has done since it was tracked.</summary>
        public sealed class Tally
        {
            // A full circle at the truck's 6 m turn radius, so a ring driven forwards reads 360 degrees.
            private const float LoopMetres = 40f;
            private const float StopSpeed = 0.2f;
            private const float Step = TestWorld.FrameTime;

            // A window of recent forward steps: metres driven and signed radians turned in each.
            private readonly Queue<float2> _window = new();
            private float _windowMetres;
            private float _windowTurn;
            private float2 _position;
            private float _yaw;
            private bool _rolling;
            private bool _arrived;
            private float _lastTripAt;

            public readonly string Name;
            public readonly Entity Unit;
            public float Metres;
            public float Turned;
            public float MaxLoop;

            /// <summary>Where the unit stood and the seconds since it was tracked when <see cref="MaxLoop"/> was reached.</summary>
            public float3 MaxLoopAt;

            private float _seconds;
            public float TightSeconds;
            public float Sideways;
            public int Stops;
            public int GiveUps;
            public int Trips;
            public float TripSeconds;

            public Tally(string name, Entity unit, TestWorld world)
            {
                Name = name;
                Unit = unit;
                var transform = world.Get<LocalTransform>(unit);
                _position = transform.Position.xz;
                _yaw = Yaw(transform);
            }

            public void CountTrip(float now)
            {
                Trips++;
                TripSeconds += now - _lastTripAt;
                _lastTripAt = now;
            }

            public void Sample(TestWorld world)
            {
                var transform = world.Get<LocalTransform>(Unit);
                var yaw = Yaw(transform);
                var moved = transform.Position.xz - _position;
                var turn = math.atan2(math.sin(yaw - _yaw), math.cos(yaw - _yaw));
                var distance = math.length(moved);
                Metres += distance;
                Turned += math.degrees(math.abs(turn));
                MeasureSideways(moved, _yaw + turn * 0.5f);
                _seconds += Step;
                MeasureLoop(distance, turn, transform);
                _position = transform.Position.xz;
                _yaw = yaw;

                var ordered = world.IsEnabled<MoveDestination>(Unit);
                CountStops(ordered, math.length(world.Get<Velocity>(Unit).Value));
                if (ordered && PathState.IsPlannedManoeuvre(world.Buffer<PathWaypoint>(Unit)))
                {
                    TightSeconds += Step;
                }

                CountGiveUps(world);
            }

            public override string ToString() =>
                $"{Name}: {Metres:F0} m, turned {Turned:F0} deg, max loop {MaxLoop:F0} deg at {MaxLoopAt.xy} after {MaxLoopAt.z:F0} s, " +
                $"tight {TightSeconds:F1} s, stops {Stops}, give-ups {GiveUps}, sideways {Sideways:F2} m/s, " +
                $"trips {Trips} (mean {(Trips == 0 ? 0f : TripSeconds / Trips):F1} s)";

            private static float Yaw(in LocalTransform transform)
            {
                var forward = transform.Forward();
                return math.atan2(forward.x, forward.z);
            }

            // The hull's right vector at the middle of the step, so a turning hull isn't read as sliding.
            private void MeasureSideways(float2 moved, float yaw)
            {
                var right = new float2(math.cos(yaw), -math.sin(yaw));
                Sideways = math.max(Sideways, math.abs(math.dot(moved, right)) / Step);
            }

            // A ring is driven forwards in one go, so backing starts the window afresh.
            private void MeasureLoop(float distance, float turn, in LocalTransform transform)
            {
                var position = transform.Position.xz;
                if (math.dot(position - _position, transform.Forward().xz) < 0f)
                {
                    _window.Clear();
                    _windowMetres = 0f;
                    _windowTurn = 0f;
                    return;
                }

                _window.Enqueue(new float2(distance, turn));
                _windowMetres += distance;
                _windowTurn += turn;
                while (_windowMetres > LoopMetres && _window.Count > 1)
                {
                    var oldest = _window.Dequeue();
                    _windowMetres -= oldest.x;
                    _windowTurn -= oldest.y;
                }

                var loop = math.degrees(math.abs(_windowTurn));
                if (loop > MaxLoop)
                {
                    MaxLoop = loop;
                    MaxLoopAt = new float3(position, _seconds);
                }
            }

            private void CountStops(bool ordered, float speed)
            {
                if (speed >= StopSpeed)
                {
                    _rolling = true;
                    return;
                }

                if (_rolling && ordered)
                {
                    Stops++;
                }

                _rolling = false;
            }

            private void CountGiveUps(TestWorld world)
            {
                var arrived = world.IsEnabled<Arrival>(Unit);
                if (arrived && !_arrived && world.Get<Arrival>(Unit).Reason == ArrivalReason.Stalled)
                {
                    GiveUps++;
                }

                _arrived = arrived;
            }
        }
    }
}
