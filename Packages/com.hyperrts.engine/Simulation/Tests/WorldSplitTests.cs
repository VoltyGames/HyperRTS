using System;
using System.Linq;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Gameplay runs only where the state lives; selection and fog view run where a player looks.</summary>
    public class WorldSplitTests
    {
        [TestCase(typeof(UnitCommandSystem), true, true, false)]
        [TestCase(typeof(SelectionSystem), true, false, true)]
        [TestCase(typeof(FogOfWarSystem), true, true, true)]
        [TestCase(typeof(LocalFogViewSystem), true, false, true)]
        public void SystemRunsInExpectedWorlds(Type system, bool local, bool server, bool client)
        {
            Assert.AreEqual(local, RunsIn(system, WorldSystemFilterFlags.LocalSimulation), "local");
            Assert.AreEqual(server, RunsIn(system, WorldSystemFilterFlags.ServerSimulation), "server");
            Assert.AreEqual(client, RunsIn(system, WorldSystemFilterFlags.ClientSimulation), "client");
        }

        private static bool RunsIn(Type system, WorldSystemFilterFlags world) =>
            DefaultWorldInitialization.GetAllSystems(world).Contains(system);
    }
}
