using System.Linq;
using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Surface decals marked wet: laid apart from the dry ones, copied by the same symmetry.</summary>
    public class MapWetDecalTests
    {
        [Test]
        public void WetEntries_AreLaidApart_FromDryOnes()
        {
            var context = MapRoadFixture.Context(MapRoadFixture.Spec(), Climate(), new MapIssues());
            context.Land.ClaimWhere(new Rect(-60f, -80f, 80f, 60f), context.Region(MapLandKind.Margin, "Muddy"), _ => true);

            new MapDecalPlanner(context).Plan();

            Assert.Greater(context.WetDecals.Count, 0);
            Assert.Greater(context.Decals.Count, 0);
            Assert.IsTrue(context.WetDecals.All(d => d.Kind.key == "Puddle"));
            Assert.IsTrue(context.Decals.All(d => d.Kind.key == "Litter"));
        }

        [Test]
        public void WetDecals_AreMirrored_LikeDryOnes()
        {
            var spec = MapRoadFixture.Spec();
            spec.symmetry = MapSymmetry.MirrorXName;
            spec.spawns[0].pos = new Maps.Spec.MapPoint(-100f, 0f);
            var context = MapRoadFixture.Context(spec, Climate(), new MapIssues());
            var area = new Rect(-90f, -40f, 60f, 80f);
            context.Land.ClaimWhere(area, context.Region(MapLandKind.Margin, "Muddy"), _ => true);

            new MapDecalPlanner(context).Plan();

            // Split by the mirror line: decals in edge cells may sit just outside the claimed rectangle.
            var authored = context.WetDecals.Where(d => d.Position.x < 0f).ToList();
            Assert.Greater(authored.Count, 10);
            Assert.AreEqual(authored.Count * 2, context.WetDecals.Count);
            var mirror = MapTransform.MirrorX;
            foreach (var decal in authored)
            {
                var twin = context.WetDecals.Single(d => (d.Position - mirror.Apply(decal.Position)).sqrMagnitude < 1e-6f);
                Assert.AreEqual(decal.Diameter, twin.Diameter);
                Assert.AreNotEqual(decal.Mirrored, twin.Mirrored);
            }
        }

        /// <summary>The road fixture's climate plus a Muddy surface laying dry Litter and wet Puddle decals.</summary>
        private static MapClimate Climate()
        {
            var climate = MapRoadFixture.Climate();
            climate.decals.Add(new MapClimateDecal { key = "Puddle", size = new Vector2(1f, 2f) });
            climate.decals.Add(new MapClimateDecal { key = "Litter", size = new Vector2(1f, 2f) });
            var muddy = MapClimateFixture.Surface("Muddy", "Meadow");
            muddy.decals.Add(new MapDecalDensity { decal = "Litter", perHectare = 40f });
            muddy.decals.Add(new MapDecalDensity { decal = "Puddle", perHectare = 80f, wet = true });
            climate.surfaces.Add(muddy);
            return climate;
        }
    }
}
