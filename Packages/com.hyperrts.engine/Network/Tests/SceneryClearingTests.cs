using HyperRTS.Network.Scenery;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Network.Tests
{
    /// <summary>A cleared building site rides the scenery change, and teams hear of it even after the destruction.</summary>
    public class SceneryClearingTests
    {
        [Test]
        public void SceneryChange_CarriesClearedThroughItsCode()
        {
            var change = new SceneryChange { Id = 7, State = SceneryChange.Destroyed, Fall = 3, Cleared = true };

            var back = SceneryChange.FromCode(change.Id, change.Code, 0, change.Fall);

            Assert.IsTrue(back.Cleared);
            Assert.AreEqual(SceneryChange.Destroyed, back.State);
            Assert.AreEqual(3, back.Fall);
            Assert.IsFalse(SceneryChange.FromCode(7, new SceneryChange { State = 120 }.Code, 0, 0).Cleared);
        }

        [Test]
        public void Tracker_TellsTeamsOfAClearing_EvenAfterTheyHeardOfTheDestruction()
        {
            using var tracker = new SceneryTracker(Allocator.Temp);
            var sightings = new NativeList<ScenerySighting>(Allocator.Temp);
            tracker.Register(9, float3.zero);
            tracker.Set(9, SceneryChange.Destroyed, 0, 0, 0b10, 1);
            tracker.Reveal(default(FogOfWar), false, 1, sightings);
            sightings.Clear();

            Assert.IsTrue(tracker.Clear(9, 0b10, 2));
            Assert.IsFalse(tracker.Clear(9, 0b10, 3), "a piece is cleared once");
            tracker.Reveal(default(FogOfWar), false, 2, sightings);
            var known = new NativeList<SceneryChange>(Allocator.Temp);
            tracker.Known(1, known);

            Assert.AreEqual(1, sightings.Length);
            Assert.IsTrue(sightings[0].Change.Cleared, "the team hears its remains were cleared");
            Assert.IsTrue(known[0].Cleared, "a joining client starts from the clearing");
        }
    }
}
