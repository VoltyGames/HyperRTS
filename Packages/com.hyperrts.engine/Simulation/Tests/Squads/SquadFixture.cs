using NUnit.Framework;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>Base for squad tests: after every tick, no living member stands in a cell closed to its squad.</summary>
    public abstract class SquadFixture : SimulationFixture
    {
        [SetUp]
        public void WatchFooting() => World.CheckEveryTick(new SquadFootingCheck(World).AssertOnOpenGround);
    }
}
