using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Base for tests that run in a <see cref="TestWorld"/> with a match already created.</summary>
    public abstract class SimulationFixture
    {
        protected TestWorld World { get; private set; }

        /// <summary>Team of each player, in order; players are owners 1, 2 and so on.</summary>
        protected virtual byte[] Teams => new byte[] { 1, 2 };

        [SetUp]
        public void CreateWorld()
        {
            World = new TestWorld();
            World.CreateMatch(Teams);
        }

        private readonly List<Func<string>> _dumps = new();

        /// <summary>Queues a <see cref="GridDump"/> rendered after the test only if it failed, so a passing run stays quiet.</summary>
        protected void DumpOnFailure(Func<string> dump) => _dumps.Add(dump);

        [TearDown]
        public void DisposeWorld()
        {
            if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
            {
                foreach (var dump in _dumps)
                {
                    GridDump.Write(dump());
                }
            }

            _dumps.Clear();
            World.Dispose();
        }
    }
}
