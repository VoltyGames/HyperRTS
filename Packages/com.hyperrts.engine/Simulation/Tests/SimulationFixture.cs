using NUnit.Framework;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Base for tests that run in a <see cref="TestWorld"/> with a match already created.</summary>
    public abstract class SimulationFixture
    {
        protected TestWorld World { get; private set; }

        /// <summary>Team of each player, in order; players are factions 1, 2 and so on.</summary>
        protected virtual byte[] Teams => new byte[] { 1, 2 };

        [SetUp]
        public void CreateWorld()
        {
            World = new TestWorld();
            World.CreateMatch(Teams);
        }

        [TearDown]
        public void DisposeWorld() => World.Dispose();
    }
}
