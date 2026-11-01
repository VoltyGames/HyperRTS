using HyperRTS.Network.Scenery;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Network.Tests
{
    /// <summary>A sectioned building's broken mask rides its scenery change, and a team learns it once it sees the spot.</summary>
    public class SceneryMaskTests
    {
        private const ulong Id = 11;
        private const byte Team = 1;

        [Test]
        public void MaskChange_ReachesATeamOnlyOnceItSeesTheSpot()
        {
            using var tracker = new SceneryTracker(Allocator.Temp);
            var fog = FogOfWar.Create(new GridFrame(new float2(-10f, -10f), 20f, new int2(1, 1)), Allocator.Temp);
            var sightings = new NativeList<ScenerySighting>(Allocator.Temp);
            tracker.Register(Id, float3.zero);

            Assert.IsTrue(tracker.Set(Id, SceneryChange.Intact, 0b101, 0, 1 << Team, 1), "the mask alone is a change");
            tracker.Reveal(fog, true, 1, sightings);
            Assert.IsEmpty(sightings.AsArray().ToArray(), "unseen, so unheard");

            fog.Set(fog.VisibleTall, int2.zero, Team);
            tracker.Reveal(fog, true, 2, sightings);
            fog.Dispose();

            Assert.AreEqual(1, sightings.Length);
            Assert.AreEqual(0b101ul, sightings[0].Change.Broken);
            Assert.IsFalse(sightings[0].Witnessed, "it broke before the team looked, so it shows the end state");
        }

        [Test]
        public void JoiningClient_StartsFromTheMaskItsTeamSaw()
        {
            using var tracker = new SceneryTracker(Allocator.Temp);
            var sightings = new NativeList<ScenerySighting>(Allocator.Temp);
            tracker.Register(Id, float3.zero);
            tracker.Set(Id, 200, 0b10, 0, 1 << Team, 1);
            tracker.Reveal(default, false, 1, sightings);

            var known = new NativeList<SceneryChange>(Allocator.Temp);
            tracker.Known(Team, known);

            Assert.AreEqual(1, known.Length);
            Assert.AreEqual(0b10ul, known[0].Broken);
            Assert.AreEqual(200, known[0].State);
        }
    }
}
