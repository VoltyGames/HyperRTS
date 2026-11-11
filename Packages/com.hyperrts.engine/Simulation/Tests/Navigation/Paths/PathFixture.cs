using HyperRTS.Simulation.Navigation.Agents;
using NUnit.Framework;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests.Navigation.Paths
{
    /// <summary>Shared setup and helpers for the PathTests family.</summary>
    public abstract class PathFixture
    {
        protected TestWorld _world;

        [TearDown]
        public void TearDown() => _world.Dispose();

        protected void CreateMatch(float size)
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, size);
        }

        protected DynamicBuffer<PathWaypoint> Waypoints(Entity unit) =>
            _world.Buffer<PathWaypoint>(unit);
    }
}
