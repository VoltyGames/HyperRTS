using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Turning and oscillation cannot hide a move that makes no sustained progress.</summary>
    public class MoveProgressTests : SimulationFixture
    {
        [Test]
        public void CirclingNearTheGoal_ReplansAndEventuallyStalls()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            var points = World.Buffer<PathWaypoint>(unit);
            points.Add(new PathWaypoint { Position = float3.zero });
            var path = new PathState { Status = PathStatus.Ready };
            var progress = new MoveProgress();
            var replans = 0;
            var stalled = false;
            for (var time = 0f; time < 12f && !stalled; time += TestWorld.FrameTime)
            {
                var facing = new float2(math.cos(time), math.sin(time));
                var reading = new MoveProgress.Reading(facing * 4f, facing, 1f, float3.zero, points, path);
                stalled = progress.HasGivenUp(reading, points, ref path, MoveProgress.WallHold.None,
                    TestWorld.FrameTime);
                if (path.Status != PathStatus.Ready)
                {
                    replans++;
                    path.Status = PathStatus.Ready;
                }
            }

            Assert.Greater(replans, 0, "turning cannot suppress replanning forever");
            Assert.IsTrue(stalled, "repeated circles and replans cannot reset the stall clock");
        }

        [Test]
        public void TurningAway_DoesNotReplaceTheBestRemainingDistance()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            var points = World.Buffer<PathWaypoint>(unit);
            points.Add(new PathWaypoint { Position = float3.zero });
            var path = new PathState { Status = PathStatus.Ready };
            var progress = new MoveProgress();
            var start = new MoveProgress.Reading(new float2(10f, 0f), new float2(0f, 1f), 1f,
                float3.zero, points, path);
            progress.HasGivenUp(start, points, ref path, MoveProgress.WallHold.None, 0.1f);
            var away = new MoveProgress.Reading(new float2(12f, 0f), new float2(1f, 0f), 1f,
                float3.zero, points, path);
            progress.HasGivenUp(away, points, ref path, MoveProgress.WallHold.None, 0.1f);

            Assert.AreEqual(10f, progress.Best, "returning to the old distance must not count as progress");
            Assert.Greater(progress.Turning, 0f, "a legitimate turn still gets bounded time");
        }
    }
}
