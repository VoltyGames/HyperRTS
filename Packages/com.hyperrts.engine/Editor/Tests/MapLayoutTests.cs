using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Map symmetry: copies, slots and teams, shared features and river continuation.</summary>
    public class MapLayoutTests
    {
        private static MapSpec Spec(string mode, string symmetry, params MapPoint[] spawns) => new()
        {
            name = "Test",
            climate = "Test",
            mode = mode,
            size = new MapPoint(200f, 200f),
            symmetry = symmetry,
            spawns = spawns.Select(p => new MapSpawnSpec { pos = p }).ToList(),
        };

        [Test]
        public void Rotate180_InterleavesSlots_SoSlotsOneAndTwoFaceEachOther()
        {
            var spec = Spec("2v2", MapSymmetry.Rotate180, new MapPoint(-40f, -80f), new MapPoint(40f, -80f));
            var issues = new MapIssues();

            var layout = MapExpander.Expand(spec, issues);

            Assert.IsFalse(issues.HasErrors, issues.ToString());
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, layout.Spawns.Select(s => s.Slot).ToArray());
            Assert.AreEqual(new Vector2(40f, 80f), layout.Spawns[1].Position, "slot 2 is slot 1 turned about the centre");
            Assert.AreEqual(new[] { 1, 2, 1, 2 }, layout.Spawns.Select(s => s.Team).ToArray());
        }

        [Test]
        public void FreeForAll_GivesEverySlotItsOwnTeam()
        {
            var spec = Spec(MapExpander.FreeForAll, "rotate-4", new MapPoint(70f, 20f));
            var layout = MapExpander.Expand(spec, new MapIssues());

            Assert.AreEqual(new[] { 1, 2, 3, 4 }, layout.Spawns.Select(s => s.Team).ToArray());
            Assert.AreEqual(-20f, layout.Spawns[1].Position.x, 1e-4f, "a quarter turn counter-clockwise");
            Assert.AreEqual(70f, layout.Spawns[1].Position.y, 1e-4f);
        }

        [Test]
        public void MirrorX_ReflectsYaw()
        {
            var spec = Spec("1v1", MapSymmetry.MirrorXName, new MapPoint(-60f, 0f));
            spec.spawns[0].yaw = 70f;
            var layout = MapExpander.Expand(spec, new MapIssues());

            Assert.AreEqual(290f, layout.Spawns[1].Yaw, 1e-3f);
        }

        [Test]
        public void SharedFeature_MustSitOnTheCentre_AndIsPlacedOnce()
        {
            var spec = Spec("1v1", MapSymmetry.Rotate180, new MapPoint(0f, -80f));
            spec.props.Add(new MapPropSpec { pos = new MapPoint(0f, 0f), kind = "Derrick", shared = true });
            spec.props.Add(new MapPropSpec { pos = new MapPoint(5f, -5f), kind = "Derrick", shared = true });
            var issues = new MapIssues();

            var layout = MapExpander.Expand(spec, issues);

            Assert.AreEqual(1, issues.Errors.Count(e => e.Contains("shared but not symmetric")), "only the off-centre one");
            Assert.AreEqual(2, layout.Props.Count, "each shared prop is placed once");
        }

        [Test]
        public void RiverMeetingItsCopyAtTheCentre_KeepsFlowingTheSameWay()
        {
            var spec = Spec("1v1", MapSymmetry.Rotate180, new MapPoint(0f, -80f));
            spec.rivers.Add(new MapRiverSpec
            {
                points = new List<MapPoint> { new(-100f, -20f), new(0f, 0f) },
                crossings = new List<MapCrossingSpec> { new() { along =0.25f, kind = MapCrossingSpec.Ford } },
            });

            var layout = MapExpander.Expand(spec, new MapIssues());

            var copy = layout.Rivers[1].Placed;
            Assert.AreEqual(new MapPoint(0f, 0f), copy.points[0], "the copy starts where the authored river ends");
            Assert.AreEqual(new MapPoint(100f, 20f), copy.points[1]);
            Assert.AreEqual(0.75f, copy.crossings[0].along, 1e-5f, "crossings stay at the same place on the reversed line");
        }
    }
}
