using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Match;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class PresentationMathTests
    {
        [Test]
        public void FogTexels_ClearWhenVisible_DimWhenExplored_DarkOtherwise()
        {
            const byte team = 2;
            var mask = (byte)(1 << team);
            using var visible = new NativeArray<byte>(new byte[] { mask, 0, 0, 1 << 1 }, Allocator.Temp);
            using var explored = new NativeArray<byte>(new byte[] { mask, mask, 0, 1 << 1 }, Allocator.Temp);
            using var output = new NativeArray<byte>(4, Allocator.Temp);

            FogTexels.Fill(visible, explored, team, 128, 217, output);

            CollectionAssert.AreEqual(new byte[] { 0, 128, 217, 217 }, output.ToArray());
        }

        [Test]
        public void Minimap_RoundTripsAndPutsNorthAtTop()
        {
            var min = new float2(-100f, -50f);
            var size = new float2(200f, 100f);

            var topLeft = MinimapMath.WorldToMinimap(new float2(-100f, 50f), min, size);
            var back = MinimapMath.MinimapToWorld(new Vector2(0.25f, 0.75f), min, size);

            Assert.AreEqual(Vector2.zero, topLeft);
            Assert.AreEqual(new Vector2(0.25f, 0.75f), MinimapMath.WorldToMinimap(back, min, size));
        }

        [Test]
        public void GroundPoint_HitsPlaneOrCapsAboveHorizon()
        {
            var down = MinimapMath.GroundPoint(new Ray(new Vector3(0f, 10f, 0f), new Vector3(0f, -1f, 1f)), 0f, 1000f);
            var up = MinimapMath.GroundPoint(new Ray(new Vector3(0f, 10f, 0f), Vector3.forward), 0f, 50f);

            Assert.That(Vector3.Distance(new Vector3(0f, 0f, 10f), down), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(new Vector3(0f, 0f, 50f), up), Is.LessThan(1e-3f));
        }

        [Test]
        public void TeamRelation_ClassifiesOwnAllyEnemyNeutral()
        {
            var relations = new FactionRelations();
            relations.Teams.Add(0);
            relations.Teams.Add(1);
            relations.Teams.Add(1);
            relations.Teams.Add(2);

            Assert.AreEqual(Relation.Own, TeamRelation.Of(1, 1, relations));
            Assert.AreEqual(Relation.Ally, TeamRelation.Of(1, 2, relations));
            Assert.AreEqual(Relation.Enemy, TeamRelation.Of(1, 3, relations));
            Assert.AreEqual(Relation.Neutral, TeamRelation.Of(1, Faction.Neutral, relations));
        }
    }
}
