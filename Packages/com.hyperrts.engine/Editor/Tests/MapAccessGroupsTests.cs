using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Access-track groups: props share a track within the smaller accessGroupReach of the two.</summary>
    public class MapAccessGroupsTests
    {
        [Test]
        public void PropsWithinReach_ShareATrack_ThroughAChain()
        {
            var stack = Entry("Stack", 20f);
            var props = new List<MapPlacement> { At(stack, 0f), At(stack, 15f), At(stack, 30f), At(stack, 60f) };

            var groups = MapAccessGroups.Group(props);

            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual(3, groups[0].Count);
        }

        [Test]
        public void APropWithZeroReach_KeepsATrackOfItsOwn()
        {
            var props = new List<MapPlacement> { At(Entry("Stack", 20f), 0f), At(Entry("Derrick", 0f), 5f) };

            Assert.AreEqual(2, MapAccessGroups.Group(props).Count);
        }

        private static MapPaletteEntry Entry(string key, float reach) =>
            new() { key = key, access = "Dirt", accessGroupReach = reach };

        private static MapPlacement At(MapPaletteEntry entry, float x) => new(entry, new Vector2(x, 0f), 0f, 1f);
    }
}
