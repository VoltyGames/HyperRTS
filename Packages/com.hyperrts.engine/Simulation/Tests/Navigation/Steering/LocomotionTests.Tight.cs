using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation.Steering
{
    public partial class LocomotionTests
    {
        private static readonly NavAgent TruckAgent = new() { Radius = TruckRadius, HullLength = 8f, Chassis = TruckChassis };

        // Forward 6 m up the z axis, then back 3 m: one gear change at (0, 6).
        private DynamicBuffer<PathWaypoint> ForwardThenBack()
        {
            var waypoints = _world.EntityManager.AddBuffer<PathWaypoint>(_world.EntityManager.CreateEntity());
            for (var z = 1.5f; z <= 6f; z += 1.5f)
            {
                waypoints.Add(new PathWaypoint { Position = new float3(0f, 0f, z), Tight = true });
            }

            waypoints.Add(new PathWaypoint { Position = new float3(0f, 0f, 4.5f), Reverse = true, Tight = true });
            waypoints.Add(new PathWaypoint { Position = new float3(0f, 0f, 3f), Reverse = true, Tight = true });
            return waypoints;
        }

        // Reversing while its path holds a planned reverse leg.
        private bool IsBackingOnAPlannedLeg(Entity unit)
        {
            if (_world.Get<LocomotionState>(unit).Speed > -0.1f)
            {
                return false;
            }

            foreach (var waypoint in _world.EntityManager.GetBuffer<PathWaypoint>(unit))
            {
                if (waypoint.Tight && waypoint.Reverse)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void TightLeg_AimsNoFurtherThanTheGearChange()
        {
            var waypoints = ForwardThenBack();
            var path = new PathState { LengthPastCorner = PathState.Measure(waypoints) };

            var leg = PathLeg.Along(float2.zero, new float2(0f, 3f), waypoints, path)
                .Manoeuvre(float2.zero, waypoints, 10f, TruckChassis);

            Assert.IsTrue(leg.Tight, "on the manoeuvre");
            Assert.IsFalse(leg.Reverse, "forwards first");
            Assert.AreEqual(6f, leg.ToGearChange, 1e-4f, "the gear change 6 m on");
            MathAssert.Near(new float2(0f, 6f), leg.Aim);
        }

        [Test]
        public void TightLeg_KeepsToItsSpeedCap_AndChangesGearStanding()
        {
            var access = _world.Access(TruckAgent);
            var waypoints = ForwardThenBack();
            var path = new PathState { Status = PathStatus.Ready, LengthPastCorner = PathState.Measure(waypoints) };
            var truck = Driving(Wheels, TruckChassis, TruckRadius, 8f, new float2(0f, 1f));
            var fastest = 0f;
            var slowest = 0f;
            var arrived = false;
            for (var t = 0f; t < 20f && !arrived; t += TestWorld.FrameTime)
            {
                path.SkipCorners(access, truck.Position, TruckRadius * 0.5f, 10f, waypoints);
                var leg = PathLeg.Along(truck.Position, new float2(0f, 3f), waypoints, path)
                    .Manoeuvre(truck.Position, waypoints, Locomotion.TightLookAhead(truck.State.Speed), TruckChassis);
                arrived = truck.HasArrived(leg);
                if (arrived)
                {
                    break;
                }

                var before = truck.State.Speed;
                truck.Position += truck.Drive(leg);
                Assert.IsFalse(before > 0f && truck.State.Speed < 0f, "stood before reversing");
                fastest = math.max(fastest, truck.State.Speed);
                slowest = math.min(slowest, truck.State.Speed);
            }

            Assert.IsTrue(arrived, "arrived");
            Assert.LessOrEqual(fastest, Locomotion.TightSpeed + 1e-4f, "forwards at no more than the cap");
            Assert.That(slowest, Is.InRange(-Wheels.ReverseSpeed - 1e-4f, -1f), "backwards at up to its reverse speed");
            // A goal under the hull counts as reached.
            Assert.Less(math.distance(truck.Position, new float2(0f, 3f)), TruckRadius + 0.1f, "at the goal");
        }
    }
}
