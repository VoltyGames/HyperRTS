using System;
using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Designer;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>New Map dialog's starting spec: it passes the spec check and the layout, and refuses what can't work.</summary>
    public class MapSpecTemplateTests
    {
        private static readonly Vector2Int Size = new(400, 400);

        [TestCase("1v1", MapSymmetry.Rotate180)]
        [TestCase("2v2", MapSymmetry.MirrorXName)]
        [TestCase("4v4", MapSymmetry.MirrorZName)]
        [TestCase("ffa", "rotate-3")]
        [TestCase("ffa", "rotate-8")]
        public void NewSpecs_PassTheSpecCheckAndTheLayout(string mode, string symmetry)
        {
            var spec = MapSpecTemplate.Create("NewMap", mode, Size, symmetry, "Temperate", 7);
            var issues = new MapIssues();
            var rules = new MapRules();
            rules.modes.Add(new MapModeRules { mode = mode });

            MapSpecCheck.Check(spec, new MapClimate(), new MapPalette(), rules, issues);
            var layout = MapExpander.Expand(spec, issues);

            Assert.IsFalse(issues.HasErrors, issues.ToString());
            var parsed = MapSymmetry.Parse(symmetry);
            Assert.IsTrue(spec.spawns.All(spawn => parsed.InSector(spawn.pos.ToVector2())), "spawns in the sector");
            Assert.AreEqual(spec.spawns.Count * parsed.Copies.Count, layout.Spawns.Count);
        }

        [Test]
        public void Terrain_ScalesItsMarginAndResolutionWithTheMap()
        {
            var terrain = MapSpecTemplate.Terrain(Size);

            Assert.AreEqual(120f, terrain.margin);
            Assert.AreEqual(1025, terrain.resolution);
            Assert.IsTrue(Mathf.IsPowerOfTwo(MapSpecTemplate.Terrain(new Vector2Int(150, 100)).resolution - 1));
        }

        [Test]
        public void Spawns_FaceTheCentre()
        {
            var spec = MapSpecTemplate.Create("NewMap", "1v1", Size, MapSymmetry.Rotate180, "Temperate", 7);

            Assert.AreEqual(new Vector2(0f, -140f), spec.spawns[0].pos.ToVector2());
            Assert.AreEqual(0f, spec.spawns[0].yaw);
        }

        [TestCase("newMap", "1v1", MapSymmetry.Rotate180, "name")]
        [TestCase("New Map", "1v1", MapSymmetry.Rotate180, "name")]
        [TestCase("NewMap", "ffa", MapSymmetry.Rotate180, "FFA")]
        [TestCase("NewMap", "2v2", "rotate-4", "2v2")]
        [TestCase("NewMap", "5v5", MapSymmetry.Rotate180, "5v5")]
        public void Create_RefusesWhatCannotWork_NamingIt(string name, string mode, string symmetry, string named)
        {
            var error = Assert.Throws<ArgumentException>(() =>
                MapSpecTemplate.Create(name, mode, Size, symmetry, "Temperate", 7));

            StringAssert.Contains(named, error.Message);
        }
    }
}
